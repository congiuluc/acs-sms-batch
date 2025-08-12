namespace BatchSMS.Configuration;

public class AzureCommunicationServicesConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public string FromPhoneNumber { get; set; } = string.Empty;
}

public class SmsConfig
{
    public bool EnableDeliveryReports { get; set; } = true;
    public string MessageTemplate { get; set; } = string.Empty;
}

public class RateLimitingConfig
{
    public int MaxConcurrentRequests { get; set; } = 10;
    public int RequestsPerMinute { get; set; } = 200;
    public int DelayBetweenBatchesMs { get; set; } = 1000;
    public int BatchSize { get; set; } = 50;
    public int RetryAttempts { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 5000;
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    public int CircuitBreakerTimeoutSeconds { get; set; } = 30;
}

public class CsvConfig
{
    public string FilePath { get; set; } = string.Empty;
    public bool HasHeaderRecord { get; set; } = true;
    /// <summary>
    /// Phone number column name. If empty or null, uses the first column in the CSV.
    /// </summary>
    public string? PhoneNumberColumn { get; set; } = null;
    /// <summary>
    /// Display name column name. If empty or null, uses "Row {number}" as display name.
    /// </summary>
    public string? DisplayNameColumn { get; set; } = null;
    public bool SkipEmptyPhoneNumbers { get; set; } = true;
    public Dictionary<string, string> CustomColumns { get; set; } = new();
}
