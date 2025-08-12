using BatchSMS.Configuration;
using BatchSMS.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace BatchSMS.Services;

public interface IRateLimitingService
{
    Task<bool> CanProceedAsync();
    void RecordRequest();
    void RecordSuccess();
    void RecordFailure();
    Task DelayBetweenBatchesAsync();
}

public class RateLimitingService : IRateLimitingService
{
    private readonly RateLimitingConfig _config;
    private readonly ILogger<RateLimitingService> _logger;
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentQueue<DateTime> _requestTimes;
    private int _consecutiveFailures;
    private DateTime _circuitBreakerOpenTime;
    private bool _circuitBreakerOpen;

    public RateLimitingService(IOptions<RateLimitingConfig> config, ILogger<RateLimitingService> logger)
    {
        _config = config.Value;
        _logger = logger;
        _semaphore = new SemaphoreSlim(_config.MaxConcurrentRequests, _config.MaxConcurrentRequests);
        _requestTimes = new ConcurrentQueue<DateTime>();
        _consecutiveFailures = 0;
        _circuitBreakerOpen = false;
    }

    public async Task<bool> CanProceedAsync()
    {
        // Check circuit breaker
        if (_circuitBreakerOpen)
        {
            if (DateTime.UtcNow - _circuitBreakerOpenTime > TimeSpan.FromSeconds(_config.CircuitBreakerTimeoutSeconds))
            {
                _logger.LogInformation("Circuit breaker timeout expired, attempting to close");
                _circuitBreakerOpen = false;
                _consecutiveFailures = 0;
            }
            else
            {
                _logger.LogWarning("Circuit breaker is open, blocking request");
                return false;
            }
        }

        // Wait for available slot
        await _semaphore.WaitAsync();

        // Check rate limit
        CleanOldRequests();
        
        if (_requestTimes.Count >= _config.RequestsPerMinute)
        {
            _logger.LogWarning("Rate limit exceeded, waiting...");
            _semaphore.Release();
            
            // Calculate wait time until oldest request expires
            var oldestRequest = _requestTimes.TryPeek(out var oldest) ? oldest : DateTime.UtcNow;
            var waitTime = TimeSpan.FromMinutes(1) - (DateTime.UtcNow - oldestRequest);
            
            if (waitTime > TimeSpan.Zero)
            {
                await Task.Delay(waitTime);
            }
            
            return await CanProceedAsync();
        }

        return true;
    }

    public void RecordRequest()
    {
        _requestTimes.Enqueue(DateTime.UtcNow);
        CleanOldRequests();
    }

    public void RecordSuccess()
    {
        _consecutiveFailures = 0;
        _semaphore.Release();
    }

    public void RecordFailure()
    {
        _consecutiveFailures++;
        _semaphore.Release();

        if (_consecutiveFailures >= _config.CircuitBreakerFailureThreshold)
        {
            _logger.LogWarning("Circuit breaker opened due to {Failures} consecutive failures", _consecutiveFailures);
            _circuitBreakerOpen = true;
            _circuitBreakerOpenTime = DateTime.UtcNow;
        }
    }

    public async Task DelayBetweenBatchesAsync()
    {
        if (_config.DelayBetweenBatchesMs > 0)
        {
            _logger.LogDebug("Delaying {DelayMs}ms between batches", _config.DelayBetweenBatchesMs);
            await Task.Delay(_config.DelayBetweenBatchesMs);
        }
    }

    private void CleanOldRequests()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-1);
        while (_requestTimes.TryPeek(out var requestTime) && requestTime < cutoff)
        {
            _requestTimes.TryDequeue(out _);
        }
    }
}
