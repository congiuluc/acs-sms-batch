using Azure;
using Azure.Communication.Sms;
using BatchSMS.Configuration;
using BatchSMS.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace BatchSMS.Services;

/// <summary>
/// SMS service with comprehensive error handling and separation of concerns
/// </summary>
public interface ISmsService
{
    /// <summary>
    /// Send SMS to a single recipient
    /// </summary>
    Task<Result<SmsResult>> SendSmsAsync(SmsRecipient recipient, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send SMS to multiple recipients with progress reporting
    /// </summary>
    Task<Result<BatchResult>> SendBatchSmsAsync(
        IReadOnlyList<SmsRecipient> recipients, 
        IProgressReporter? progressReporter = null,
        IRealTimeCsvWriter? csvWriter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Format message template with recipient data
    /// </summary>
    Result<string> FormatMessage(string template, SmsRecipient recipient);

    /// <summary>
    /// Validate SMS configuration
    /// </summary>
    Result ValidateConfiguration();
}

public class SmsService : ISmsService
{
    private readonly IAzureConfigurationService _azureConfigService;
    private readonly SmsConfig _smsConfig;
    private readonly RateLimitingConfig _rateLimitConfig;
    private readonly IRateLimitingService _rateLimitingService;
    private readonly ILogger<SmsService> _logger;
    private SmsClient? _smsClient;
    private string? _fromPhoneNumber;

    private static readonly Regex PlaceholderRegex = new(@"\{(\w+)\}", RegexOptions.Compiled);

    public SmsService(
        IAzureConfigurationService azureConfigService,
        IOptions<SmsConfig> smsConfig,
        IOptions<RateLimitingConfig> rateLimitConfig,
        IRateLimitingService rateLimitingService,
        ILogger<SmsService> logger)
    {
        _azureConfigService = azureConfigService;
        _smsConfig = smsConfig.Value;
        _rateLimitConfig = rateLimitConfig.Value;
        _rateLimitingService = rateLimitingService;
        _logger = logger;
    }

    public Result ValidateConfiguration()
    {
        try
        {
            // Since this is a synchronous method, we'll validate asynchronously but wait for the result
            var validationTask = ValidateConfigurationAsync();
            validationTask.Wait();
            
            return validationTask.Result 
                ? Result.Success() 
                : Result.Failure("Azure Communication Services configuration validation failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration validation failed");
            return Result.Failure("Configuration validation failed", ex);
        }
    }

    private async Task<bool> ValidateConfigurationAsync()
    {
        try
        {
            // Ensure SMS client is initialized
            await EnsureSmsClientInitializedAsync();

            // Validate Azure Communication Services configuration
            var configValid = await _azureConfigService.ValidateConfigurationAsync();
            if (!configValid)
            {
                _logger.LogError("Azure Communication Services configuration is invalid");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_smsConfig.MessageTemplate))
            {
                _logger.LogError("SMS message template is not configured");
                return false;
            }

            // Test format message with empty recipient
            var testRecipient = new SmsRecipient { DisplayName = "Test" };
            var formatResult = FormatMessage(_smsConfig.MessageTemplate, testRecipient);
            if (formatResult.IsFailure)
            {
                _logger.LogError("Message template validation failed: {Error}", formatResult.Error);
                return false;
            }

            _logger.LogInformation("SMS service configuration validated successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration validation failed");
            return false;
        }
    }

    public Result<string> FormatMessage(string template, SmsRecipient recipient)
    {
        try
        {
            _logger.LogDebug("Formatting message template for recipient {PhoneNumber}. Available custom fields: {CustomFields}", 
                recipient.PhoneNumber, 
                recipient.CustomFields?.Keys.Count > 0 ? string.Join(", ", recipient.CustomFields.Keys) : "none");
            
            if (string.IsNullOrWhiteSpace(template))
            {
                _logger.LogWarning("Message template is empty for recipient {PhoneNumber}", recipient.PhoneNumber);
                return Result<string>.Failure("Message template is empty");
            }

            var result = PlaceholderRegex.Replace(template, match =>
            {
                var fieldName = match.Groups[1].Value;
                var fieldValue = GetRecipientFieldValue(recipient, fieldName);
                
                if (fieldValue != null)
                {
                    _logger.LogDebug("Replaced placeholder {{{Placeholder}}} with value '{Value}' for recipient {PhoneNumber}", 
                        fieldName, fieldValue, recipient.PhoneNumber);
                }
                else
                {
                    _logger.LogDebug("Placeholder {{{Placeholder}}} not found in recipient data for {PhoneNumber}, keeping original placeholder", 
                        fieldName, recipient.PhoneNumber);
                }
                
                return fieldValue ?? match.Value;
            });

            if (string.IsNullOrWhiteSpace(result))
            {
                _logger.LogWarning("Formatted message is empty for recipient {PhoneNumber}", recipient.PhoneNumber);
                return Result<string>.Failure("Formatted message is empty");
            }

            _logger.LogDebug("Message formatted successfully for recipient {PhoneNumber}. Length: {Length} characters", 
                recipient.PhoneNumber, result.Length);
            return Result<string>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error formatting message for recipient {PhoneNumber}", recipient.PhoneNumber);
            return Result<string>.Failure("Failed to format message", ex);
        }
    }

    private async Task EnsureSmsClientInitializedAsync()
    {
        if (_smsClient != null && !string.IsNullOrEmpty(_fromPhoneNumber))
        {
            return; // Already initialized
        }

        try
        {
            var connectionString = await _azureConfigService.GetConnectionStringAsync();
            _fromPhoneNumber = await _azureConfigService.GetFromPhoneNumberAsync();
            
            _smsClient = new SmsClient(connectionString);
            
            _logger.LogDebug("SMS client initialized successfully");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("connection string"))
        {
            _logger.LogError("Azure Communication Services connection string is not configured: {ErrorMessage}", ex.Message);
            throw new InvalidOperationException("Azure Communication Services connection string is not configured. Please check your Key Vault secrets, environment variables, or configuration settings.", ex);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("phone number"))
        {
            _logger.LogError("Azure Communication Services from phone number is not configured: {ErrorMessage}", ex.Message);
            throw new InvalidOperationException("Azure Communication Services from phone number is not configured. Please check your Key Vault secrets, environment variables, or configuration settings.", ex);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("connection string") || ex.Message.Contains("connectionString"))
        {
            _logger.LogError("Invalid Azure Communication Services connection string format: {ErrorMessage}. Please verify the connection string is correctly formatted.", ex.Message);
            throw new InvalidOperationException("Invalid Azure Communication Services connection string format. Please verify the connection string includes endpoint and access key.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SMS client: {ErrorMessage}", ex.Message);
            throw new InvalidOperationException($"Failed to initialize SMS client: {ex.Message}", ex);
        }
    }

    public async Task<Result<SmsResult>> SendSmsAsync(SmsRecipient recipient, string message, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new SmsResult
        {
            RecipientPhoneNumber = recipient.PhoneNumber,
            SentAt = startTime
        };

        _logger.LogInformation("Starting SMS send to {PhoneNumber} for {DisplayName}{DryRunIndicator}", 
            recipient.PhoneNumber, recipient.DisplayName ?? "[No Name]", _smsConfig.DryRun ? " [DRY RUN]" : "");

        try
        {
            // Ensure SMS client is initialized (even in dry run mode for validation)
            await EnsureSmsClientInitializedAsync();

            // Validate phone number
            if (string.IsNullOrWhiteSpace(recipient.PhoneNumber))
            {
                _logger.LogWarning("SMS send skipped: Phone number is empty for recipient {DisplayName}{DryRunIndicator}", 
                    recipient.DisplayName ?? "[No Name]", _smsConfig.DryRun ? " [DRY RUN]" : "");
                return Result<SmsResult>.Success(result with { 
                    ErrorMessage = "Phone number is empty", 
                    IsSuccess = false 
                });
            }

            var phoneNumber = recipient.PhoneNumber.Trim();
            _logger.LogDebug("Processing SMS for {PhoneNumber}. Message length: {MessageLength} characters", 
                phoneNumber, message.Length);

            // Check rate limiting
            var rateLimitResult = await CheckRateLimitingAsync(phoneNumber);
            if (rateLimitResult.IsFailure)
            {
                _logger.LogWarning("SMS send failed due to rate limiting: {PhoneNumber} - {Error}", 
                    phoneNumber, rateLimitResult.Error);
                return Result<SmsResult>.Success(result with { 
                    ErrorMessage = rateLimitResult.Error, 
                    IsSuccess = false 
                });
            }

            _rateLimitingService.RecordRequest();
            _logger.LogDebug("Rate limiting check passed for {PhoneNumber}", phoneNumber);

            var smsOptions = new SmsSendOptions(_smsConfig.EnableDeliveryReports);
            
            // Send SMS with retry logic or simulate in dry run mode
            if (_smsConfig.DryRun)
            {
                var dryRunResult = await SimulateSmsDeliveryAsync(phoneNumber, message, cancellationToken);
                
                var duration = DateTime.UtcNow - startTime;
                
                if (dryRunResult.IsSuccess)
                {
                    result = result with { 
                        IsSuccess = true, 
                        MessageId = $"dry-run-{Guid.NewGuid():N}" 
                    };
                    _rateLimitingService.RecordSuccess();
                    _logger.LogInformation("DRY RUN: SMS would be sent successfully to {PhoneNumber} in {Duration}ms. MessageId: {MessageId}", 
                        phoneNumber, duration.TotalMilliseconds, result.MessageId);
                }
                else
                {
                    result = result with { 
                        ErrorMessage = dryRunResult.Error ?? "DRY RUN: Simulated failure", 
                        IsSuccess = false 
                    };
                    _rateLimitingService.RecordFailure();
                    _logger.LogWarning("DRY RUN: SMS would fail for {PhoneNumber} in {Duration}ms: {ErrorMessage}", 
                        phoneNumber, duration.TotalMilliseconds, result.ErrorMessage);
                }
            }
            else
            {
                var sendResult = await SendWithRetryAsync(phoneNumber, message, smsOptions, cancellationToken);
                
                var duration = DateTime.UtcNow - startTime;
                
                if (sendResult.IsSuccess)
                {
                    var response = sendResult.Value;
                    if (response.Value.Successful)
                    {
                        result = result with { 
                            IsSuccess = true, 
                            MessageId = response.Value.MessageId 
                        };
                        _rateLimitingService.RecordSuccess();
                        _logger.LogInformation("SMS sent successfully to {PhoneNumber} in {Duration}ms. MessageId: {MessageId}", 
                            phoneNumber, duration.TotalMilliseconds, result.MessageId);
                    }
                    else
                    {
                        result = result with { 
                            ErrorMessage = $"SMS send failed: {response.Value.ErrorMessage}", 
                            IsSuccess = false 
                        };
                        _rateLimitingService.RecordFailure();
                        _logger.LogError("SMS send failed for {PhoneNumber} in {Duration}ms: {ErrorMessage}", 
                            phoneNumber, duration.TotalMilliseconds, response.Value.ErrorMessage);
                    }
                }
                else
                {
                    result = result with { 
                        ErrorMessage = sendResult.Error, 
                        IsSuccess = false 
                    };
                    _rateLimitingService.RecordFailure();
                    _logger.LogError("SMS send failed for {PhoneNumber} in {Duration}ms: {ErrorMessage}", 
                        phoneNumber, duration.TotalMilliseconds, sendResult.Error);
                }
            }

            return Result<SmsResult>.Success(result);
        }
        catch (OperationCanceledException)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogWarning("SMS send cancelled for {PhoneNumber} after {Duration}ms", 
                recipient.PhoneNumber, duration.TotalMilliseconds);
            return Result<SmsResult>.Success(result with { 
                ErrorMessage = "Operation was cancelled", 
                IsSuccess = false 
            });
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Unexpected error sending SMS to {PhoneNumber} after {Duration}ms", 
                recipient.PhoneNumber, duration.TotalMilliseconds);
            return Result<SmsResult>.Failure("Unexpected error occurred", ex);
        }
    }

    public async Task<Result<BatchResult>> SendBatchSmsAsync(
        IReadOnlyList<SmsRecipient> recipients, 
        IProgressReporter? progressReporter = null,
        IRealTimeCsvWriter? csvWriter = null,
        CancellationToken cancellationToken = default)
    {
        var batchResult = new BatchResult
        {
            TotalRecords = recipients.Count,
            StartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting batch SMS send for {Count} recipients", recipients.Count);

            var batches = CreateBatches(recipients);
            _logger.LogInformation("Processing {BatchCount} batches with batch size {BatchSize}", 
                batches.Count, _rateLimitConfig.BatchSize);

            var processedCount = 0;
            var successCount = 0;
            var failedCount = 0;

            foreach (var (batch, batchIndex) in batches.Select((b, i) => (b, i)))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Batch processing cancelled at batch {BatchIndex}/{TotalBatches}", 
                        batchIndex + 1, batches.Count);
                    break;
                }

                var batchStartTime = DateTime.UtcNow;
                _logger.LogDebug("Processing batch {BatchIndex}/{TotalBatches} with {RecipientCount} recipients", 
                    batchIndex + 1, batches.Count, batch.Count);

                var batchTasks = batch.Select(async recipient =>
                {
                    var formatResult = FormatMessage(_smsConfig.MessageTemplate, recipient);
                    if (formatResult.IsFailure)
                    {
                        var failedResult = new SmsResult
                        {
                            RecipientPhoneNumber = recipient.PhoneNumber,
                            SentAt = DateTime.UtcNow,
                            IsSuccess = false,
                            ErrorMessage = $"Message formatting failed: {formatResult.Error}"
                        };
                        return Result<SmsResult>.Success(failedResult);
                    }

                    return await SendSmsAsync(recipient, formatResult.Value, cancellationToken);
                });

                var batchResults = await Task.WhenAll(batchTasks);

                // Process batch results
                foreach (var (resultWrapper, recipient) in batchResults.Zip(batch, (r, p) => (r, p)))
                {
                    if (resultWrapper.IsSuccess)
                    {
                        var smsResult = resultWrapper.Value;
                        batchResult.Results.Add(smsResult);

                        if (smsResult.IsSuccess)
                        {
                            batchResult.SuccessfulSends++;
                            successCount++;
                        }
                        else
                        {
                            batchResult.FailedSends++;
                            failedCount++;
                        }

                        // Write to CSV if writer is provided
                        if (csvWriter != null)
                        {
                            var csvResult = await csvWriter.WriteResultAsync(smsResult, recipient);
                            if (csvResult.IsFailure)
                            {
                                _logger.LogWarning("Failed to write CSV result for {PhoneNumber}: {Error}", 
                                    recipient.PhoneNumber, csvResult.Error);
                            }
                        }
                    }
                    else
                    {
                        // Handle unexpected failures
                        batchResult.FailedSends++;
                        failedCount++;
                        _logger.LogError("Unexpected failure for {PhoneNumber}: {Error}", 
                            recipient.PhoneNumber, resultWrapper.Error);
                    }

                    processedCount++;

                    // Report progress
                    if (progressReporter != null)
                    {
                        var progress = new BatchProgress
                        {
                            TotalItems = recipients.Count,
                            ProcessedItems = processedCount,
                            SuccessfulItems = successCount,
                            FailedItems = failedCount,
                            ElapsedTime = DateTime.UtcNow - batchResult.StartTime,
                            CurrentItem = recipient.PhoneNumber
                        };
                        await progressReporter.ReportProgressAsync(progress);
                    }
                }

                // Delay between batches
                if (batchIndex < batches.Count - 1)
                {
                    await Task.Delay(_rateLimitConfig.DelayBetweenBatchesMs, cancellationToken);
                }
            }

            batchResult.EndTime = DateTime.UtcNow;
            batchResult.TotalDuration = batchResult.EndTime!.Value - batchResult.StartTime;

            _logger.LogInformation("Batch SMS sending completed. Success: {Success}, Failed: {Failed}, Duration: {Duration}",
                batchResult.SuccessfulSends, batchResult.FailedSends, batchResult.TotalDuration);

            return Result<BatchResult>.Success(batchResult);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Batch SMS sending was cancelled");
            batchResult.EndTime = DateTime.UtcNow;
            batchResult.TotalDuration = batchResult.EndTime!.Value - batchResult.StartTime;
            return Result<BatchResult>.Success(batchResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during batch SMS sending");
            batchResult.EndTime = DateTime.UtcNow;
            batchResult.TotalDuration = batchResult.EndTime!.Value - batchResult.StartTime;
            return Result<BatchResult>.Failure("Batch SMS sending failed", ex);
        }
    }

    private async Task<Result> CheckRateLimitingAsync(string phoneNumber)
    {
        if (!await _rateLimitingService.CanProceedAsync())
        {
            _logger.LogWarning("Rate limit exceeded for {PhoneNumber}, waiting 1 second", phoneNumber);
            await Task.Delay(1000);
            
            if (!await _rateLimitingService.CanProceedAsync())
            {
                return Result.Failure("Rate limit exceeded after retry");
            }
        }

        return Result.Success();
    }

    private async Task<Result<Response<SmsSendResult>>> SendWithRetryAsync(
        string phoneNumber, 
        string message, 
        SmsSendOptions options,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        // Handle dry run mode
        if (_smsConfig.DryRun)
        {
            var dryRunResult = await SimulateSmsDeliveryAsync(phoneNumber, message, cancellationToken);
            if (dryRunResult.IsSuccess)
            {
                // Create a mock successful response for dry run
                return Result<Response<SmsSendResult>>.Success(null!); // We'll handle this in the calling method
            }
            else
            {
                return Result<Response<SmsSendResult>>.Failure(dryRunResult.Error ?? "DRY RUN: Simulated failure");
            }
        }

        for (int attempt = 0; attempt <= _rateLimitConfig.RetryAttempts; attempt++)
        {
            try
            {
                // Ensure we have the SMS client and from phone number
                if (_smsClient == null || string.IsNullOrEmpty(_fromPhoneNumber))
                {
                    throw new InvalidOperationException("SMS client not properly initialized");
                }

                var response = await _smsClient.SendAsync(
                    from: _fromPhoneNumber,
                    to: phoneNumber,
                    message: message,
                    options: options,
                    cancellationToken: cancellationToken);

                return Result<Response<SmsSendResult>>.Success(response);
            }
            catch (RequestFailedException ex) when (IsRetryableError(ex))
            {
                lastException = ex;
                if (attempt < _rateLimitConfig.RetryAttempts)
                {
                    var delay = TimeSpan.FromMilliseconds(_rateLimitConfig.RetryDelayMs * Math.Pow(2, attempt));
                    _logger.LogDebug("Retry attempt {Attempt} for {PhoneNumber} after {Delay}ms due to: {Error}",
                        attempt + 1, phoneNumber, delay.TotalMilliseconds, ex.Message);
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 400)
            {
                _logger.LogError("Bad request sending SMS to {PhoneNumber} (400 Bad Request). This usually indicates an invalid phone number format or message content. Status: {Status}, Error: {ErrorMessage}", 
                    phoneNumber, ex.Status, ex.Message);
                return Result<Response<SmsSendResult>>.Failure(
                    $"Invalid request (400 Bad Request): Please check the phone number format and message content. Phone: {phoneNumber}. {ex.Message}", ex);
            }
            catch (RequestFailedException ex) when (ex.Status == 401)
            {
                _logger.LogError("Authentication failed sending SMS to {PhoneNumber} (401 Unauthorized). Please check your Azure Communication Services connection string and credentials. Status: {Status}, Error: {ErrorMessage}", 
                    phoneNumber, ex.Status, ex.Message);
                return Result<Response<SmsSendResult>>.Failure(
                    $"Authentication failed (401 Unauthorized): Please verify your Azure Communication Services connection string and credentials. {ex.Message}", ex);
            }
            catch (RequestFailedException ex) when (ex.Status == 403)
            {
                _logger.LogError("Access forbidden sending SMS to {PhoneNumber} (403 Forbidden). The phone number may not be verified or SMS sending may not be enabled for your Azure Communication Services resource. Status: {Status}, Error: {ErrorMessage}", 
                    phoneNumber, ex.Status, ex.Message);
                return Result<Response<SmsSendResult>>.Failure(
                    $"Access forbidden (403 Forbidden): Please ensure the from phone number is verified and SMS is enabled for your Azure Communication Services resource. {ex.Message}", ex);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogError("Resource not found sending SMS to {PhoneNumber} (404 Not Found). This may indicate the Azure Communication Services resource or phone number is not properly configured. Status: {Status}, Error: {ErrorMessage}", 
                    phoneNumber, ex.Status, ex.Message);
                return Result<Response<SmsSendResult>>.Failure(
                    $"Resource not found (404 Not Found): Please verify your Azure Communication Services resource configuration. {ex.Message}", ex);
            }
            catch (RequestFailedException ex) when (ex.Status == 429)
            {
                _logger.LogWarning("Rate limit exceeded sending SMS to {PhoneNumber} (429 Too Many Requests). Status: {Status}, Error: {ErrorMessage}", 
                    phoneNumber, ex.Status, ex.Message);
                lastException = ex;
                if (attempt < _rateLimitConfig.RetryAttempts)
                {
                    var delay = TimeSpan.FromMilliseconds(_rateLimitConfig.RetryDelayMs * Math.Pow(2, attempt + 2)); // Longer delay for rate limiting
                    _logger.LogDebug("Rate limit retry attempt {Attempt} for {PhoneNumber} after {Delay}ms",
                        attempt + 1, phoneNumber, delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (RequestFailedException ex) when (ex.Status >= 400 && ex.Status < 500)
            {
                _logger.LogError("Client error sending SMS to {PhoneNumber}. Status: {Status}, Error: {ErrorMessage}", 
                    phoneNumber, ex.Status, ex.Message);
                return Result<Response<SmsSendResult>>.Failure(
                    $"SMS send failed with client error (Status {ex.Status}): {ex.Message}", ex);
            }
            catch (RequestFailedException ex) when (ex.Status >= 500)
            {
                _logger.LogWarning("Server error sending SMS to {PhoneNumber} (retryable). Status: {Status}, Error: {ErrorMessage}", 
                    phoneNumber, ex.Status, ex.Message);
                lastException = ex;
                if (attempt < _rateLimitConfig.RetryAttempts)
                {
                    var delay = TimeSpan.FromMilliseconds(_rateLimitConfig.RetryDelayMs * Math.Pow(2, attempt));
                    _logger.LogDebug("Server error retry attempt {Attempt} for {PhoneNumber} after {Delay}ms",
                        attempt + 1, phoneNumber, delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                _logger.LogWarning("Network error sending SMS to {PhoneNumber}: {ErrorMessage}. This may be a temporary network issue.", 
                    phoneNumber, ex.Message);
                lastException = ex;
                if (attempt < _rateLimitConfig.RetryAttempts)
                {
                    var delay = TimeSpan.FromMilliseconds(_rateLimitConfig.RetryDelayMs * Math.Pow(2, attempt));
                    _logger.LogDebug("Network error retry attempt {Attempt} for {PhoneNumber} after {Delay}ms",
                        attempt + 1, phoneNumber, delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);
                }
                else
                {
                    return Result<Response<SmsSendResult>>.Failure(
                        $"Network error after {_rateLimitConfig.RetryAttempts + 1} attempts: {ex.Message}", ex);
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogWarning("Timeout sending SMS to {PhoneNumber}: {ErrorMessage}", phoneNumber, ex.Message);
                lastException = ex;
                if (attempt < _rateLimitConfig.RetryAttempts)
                {
                    var delay = TimeSpan.FromMilliseconds(_rateLimitConfig.RetryDelayMs * Math.Pow(2, attempt));
                    _logger.LogDebug("Timeout retry attempt {Attempt} for {PhoneNumber} after {Delay}ms",
                        attempt + 1, phoneNumber, delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);
                }
                else
                {
                    return Result<Response<SmsSendResult>>.Failure(
                        $"Request timeout after {_rateLimitConfig.RetryAttempts + 1} attempts: {ex.Message}", ex);
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError("Invalid operation sending SMS to {PhoneNumber}: {ErrorMessage}. This usually indicates a configuration problem.", 
                    phoneNumber, ex.Message);
                return Result<Response<SmsSendResult>>.Failure(
                    $"Invalid operation: {ex.Message}. Please check your SMS client configuration.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending SMS to {PhoneNumber}: {ErrorMessage}", phoneNumber, ex.Message);
                return Result<Response<SmsSendResult>>.Failure($"SMS send failed with unexpected error: {ex.Message}", ex);
            }
        }

        return lastException != null 
            ? Result<Response<SmsSendResult>>.Failure(
                $"SMS send failed after {_rateLimitConfig.RetryAttempts + 1} attempts: {lastException.Message}",
                lastException)
            : Result<Response<SmsSendResult>>.Failure(
                $"SMS send failed after {_rateLimitConfig.RetryAttempts + 1} attempts");
    }

    private async Task<Result> SimulateSmsDeliveryAsync(
        string phoneNumber, 
        string message, 
        CancellationToken cancellationToken)
    {
        // Simulate processing time (realistic delay)
        var simulatedDelay = Random.Shared.Next(100, 800); // 100ms to 800ms
        await Task.Delay(simulatedDelay, cancellationToken);

        _logger.LogDebug("DRY RUN: Simulated SMS delivery to {PhoneNumber} (took {Delay}ms). Message: {Message}", 
            phoneNumber, simulatedDelay, message.Length > 50 ? message[..50] + "..." : message);

        // Simulate occasional failures for testing (5% failure rate)
        var shouldSimulateFailure = Random.Shared.Next(1, 21) == 1; // 1 in 20 chance

        if (shouldSimulateFailure)
        {
            _logger.LogDebug("DRY RUN: Simulating delivery failure for {PhoneNumber}", phoneNumber);
            return Result.Failure("DRY RUN: Simulated delivery failure");
        }

        _logger.LogDebug("DRY RUN: Simulating successful delivery for {PhoneNumber}", phoneNumber);
        return Result.Success();
    }

    private static bool IsRetryableError(RequestFailedException ex)
    {
        return ex.Status == 429 || ex.Status >= 500;
    }

    private List<List<SmsRecipient>> CreateBatches(IReadOnlyList<SmsRecipient> recipients)
    {
        return recipients
            .Select((recipient, index) => new { recipient, index })
            .GroupBy(x => x.index / _rateLimitConfig.BatchSize)
            .Select(g => g.Select(x => x.recipient).ToList())
            .ToList();
    }

    private static string? GetRecipientFieldValue(SmsRecipient recipient, string fieldName)
    {
        return fieldName.ToLowerInvariant() switch
        {
            "displayname" or "name" => recipient.DisplayName,
            "phonenumber" or "mobile" or "phone" => recipient.PhoneNumber,
            _ => GetCustomFieldValue(recipient.CustomFields, fieldName)
        };
    }

    private static string? GetCustomFieldValue(Dictionary<string, string>? customFields, string fieldName)
    {
        if (customFields == null || customFields.Count == 0)
            return null;

        // Try exact match first
        if (customFields.TryGetValue(fieldName, out var exactValue))
            return exactValue;

        // Try case-insensitive match
        var kvp = customFields.FirstOrDefault(x => 
            string.Equals(x.Key, fieldName, StringComparison.OrdinalIgnoreCase));
        
        return kvp.Key != null ? kvp.Value : null;
    }
}
