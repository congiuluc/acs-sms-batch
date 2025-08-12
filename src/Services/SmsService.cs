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
    private readonly SmsClient _smsClient;
    private readonly AzureCommunicationServicesConfig _acsConfig;
    private readonly SmsConfig _smsConfig;
    private readonly RateLimitingConfig _rateLimitConfig;
    private readonly IRateLimitingService _rateLimitingService;
    private readonly ILogger<SmsService> _logger;

    private static readonly Regex PlaceholderRegex = new(@"\{(\w+)\}", RegexOptions.Compiled);

    public SmsService(
        IOptions<AzureCommunicationServicesConfig> acsConfig,
        IOptions<SmsConfig> smsConfig,
        IOptions<RateLimitingConfig> rateLimitConfig,
        IRateLimitingService rateLimitingService,
        ILogger<SmsService> logger)
    {
        _acsConfig = acsConfig.Value;
        _smsConfig = smsConfig.Value;
        _rateLimitConfig = rateLimitConfig.Value;
        _rateLimitingService = rateLimitingService;
        _logger = logger;

        _smsClient = new SmsClient(_acsConfig.ConnectionString);
    }

    public Result ValidateConfiguration()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_acsConfig.ConnectionString))
                return Result.Failure("Azure Communication Services connection string is not configured");

            if (string.IsNullOrWhiteSpace(_acsConfig.FromPhoneNumber))
                return Result.Failure("From phone number is not configured");

            if (string.IsNullOrWhiteSpace(_smsConfig.MessageTemplate))
                return Result.Failure("SMS message template is not configured");

            // Test format message with empty recipient
            var testRecipient = new SmsRecipient { DisplayName = "Test" };
            var formatResult = FormatMessage(_smsConfig.MessageTemplate, testRecipient);
            if (formatResult.IsFailure)
                return Result.Failure($"Message template validation failed: {formatResult.Error}");

            _logger.LogInformation("SMS service configuration validated successfully");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration validation failed");
            return Result.Failure("Configuration validation failed", ex);
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

    public async Task<Result<SmsResult>> SendSmsAsync(SmsRecipient recipient, string message, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new SmsResult
        {
            RecipientPhoneNumber = recipient.PhoneNumber,
            SentAt = startTime
        };

        _logger.LogInformation("Starting SMS send to {PhoneNumber} for {DisplayName}", 
            recipient.PhoneNumber, recipient.DisplayName ?? "[No Name]");

        try
        {
            // Validate phone number
            if (string.IsNullOrWhiteSpace(recipient.PhoneNumber))
            {
                _logger.LogWarning("SMS send skipped: Phone number is empty for recipient {DisplayName}", 
                    recipient.DisplayName ?? "[No Name]");
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
            
            // Send SMS with retry logic
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

        for (int attempt = 0; attempt <= _rateLimitConfig.RetryAttempts; attempt++)
        {
            try
            {
                var response = await _smsClient.SendAsync(
                    from: _acsConfig.FromPhoneNumber,
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Non-retryable error for {PhoneNumber}: {Error}", phoneNumber, ex.Message);
                return Result<Response<SmsSendResult>>.Failure($"SMS send failed: {ex.Message}", ex);
            }
        }

        return lastException != null 
            ? Result<Response<SmsSendResult>>.Failure(
                $"SMS send failed after {_rateLimitConfig.RetryAttempts + 1} attempts: {lastException.Message}",
                lastException)
            : Result<Response<SmsSendResult>>.Failure(
                $"SMS send failed after {_rateLimitConfig.RetryAttempts + 1} attempts");
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
