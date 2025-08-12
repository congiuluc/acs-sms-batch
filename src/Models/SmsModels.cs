namespace BatchSMS.Models;

/// <summary>
/// Represents an SMS recipient with contact information and custom fields
/// </summary>
public class SmsRecipient
{
    /// <summary>
    /// Display name of the recipient
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Phone number in international format (e.g., +393901234567)
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Additional custom fields that can be used in message templates
    /// </summary>
    public Dictionary<string, string> CustomFields { get; set; } = new();
}

/// <summary>
/// Represents a CSV record with dynamic fields
/// </summary>
public class CsvRecord
{
    /// <summary>
    /// Dictionary of field names and values from the CSV
    /// </summary>
    public Dictionary<string, string> Fields { get; set; } = new();
}

/// <summary>
/// Represents the result of an SMS send operation
/// </summary>
public record SmsResult
{
    /// <summary>
    /// The recipient's phone number
    /// </summary>
    public string? RecipientPhoneNumber { get; set; }

    /// <summary>
    /// Backward compatibility property for phone number
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Backward compatibility property for display name
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Azure Communication Services message ID if SMS was sent successfully
    /// </summary>
    public string? MessageId { get; set; }

    /// <summary>
    /// Indicates whether the SMS was sent successfully
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Error message if the SMS send failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// UTC timestamp when the SMS send was initiated
    /// </summary>
    public DateTime SentAt { get; set; }
}

/// <summary>
/// Represents the aggregate results of a batch SMS operation
/// </summary>
public class BatchResult
{
    /// <summary>
    /// Total number of records processed
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    /// Number of SMS messages sent successfully
    /// </summary>
    public int SuccessfulSends { get; set; }

    /// <summary>
    /// Number of SMS messages that failed to send
    /// </summary>
    public int FailedSends { get; set; }

    /// <summary>
    /// Number of records that were skipped due to validation errors
    /// </summary>
    public int SkippedRecords { get; set; }

    /// <summary>
    /// Detailed results for each SMS send attempt
    /// </summary>
    public List<SmsResult> Results { get; set; } = new();

    /// <summary>
    /// Total duration of the batch operation
    /// </summary>
    public TimeSpan? TotalDuration { get; set; }

    /// <summary>
    /// UTC timestamp when the batch operation started
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// UTC timestamp when the batch operation ended
    /// </summary>
    public DateTime? EndTime { get; set; }
}
