using BatchSMS.Configuration;
using BatchSMS.Models;
using BatchSMS.Utilities;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace BatchSMS.Services;

public interface ICsvReaderService
{
    Task<List<SmsRecipient>> ReadRecipientsAsync();
    Task<List<SmsRecipient>> ReadRecipientsAsync(string filePath);
}

public class CsvReaderService : ICsvReaderService
{
    private readonly CsvConfig _config;
    private readonly ILogger<CsvReaderService> _logger;

    public CsvReaderService(IOptions<CsvConfig> config, ILogger<CsvReaderService> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public async Task<List<SmsRecipient>> ReadRecipientsAsync()
    {
        return await ReadRecipientsAsync(_config.FilePath);
    }

    public async Task<List<SmsRecipient>> ReadRecipientsAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogError("CSV file not found: {FilePath}", filePath);
            throw new FileNotFoundException($"CSV file not found: {filePath}");
        }

        var recipients = new List<SmsRecipient>();

        try
        {
            using var reader = new StreamReader(filePath);
            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = _config.HasHeaderRecord,
                MissingFieldFound = null,
                HeaderValidated = null
            };

            using var csv = new CsvReader(reader, csvConfig);

            // Read header to get column names
            await csv.ReadAsync();
            csv.ReadHeader();
            var headers = csv.HeaderRecord ?? Array.Empty<string>();

            _logger.LogInformation("CSV headers found: {Headers}", string.Join(", ", headers));

            // Determine phone number column
            var phoneNumberColumn = DeterminePhoneNumberColumn(headers);
            var displayNameColumn = DetermineDisplayNameColumn(headers);

            _logger.LogInformation("Using phone number column: '{PhoneColumn}', display name column: '{DisplayColumn}'", 
                phoneNumberColumn, displayNameColumn ?? "Generated");

            var recordCount = 0;
            while (await csv.ReadAsync())
            {
                recordCount++;
                var csvRecord = new CsvRecord();
                
                // Read all fields into dictionary
                foreach (var header in headers)
                {
                    if (csv.TryGetField(header, out string? value))
                    {
                        csvRecord.Fields[header] = value ?? string.Empty;
                    }
                }

                // Map to SmsRecipient
                var recipient = MapCsvRecordToRecipient(csvRecord, phoneNumberColumn, displayNameColumn, recordCount);

                // Skip records with empty phone numbers if configured
                if (_config.SkipEmptyPhoneNumbers && string.IsNullOrWhiteSpace(recipient.PhoneNumber))
                {
                    _logger.LogDebug("Skipping record {RecordNumber} with empty phone number for user: {DisplayName}", 
                        recordCount, recipient.DisplayName);
                    continue;
                }

                // Clean phone number format using improved normalization
                if (!string.IsNullOrWhiteSpace(recipient.PhoneNumber))
                {
                    var normalizedPhone = PhoneNumberValidator.NormalizePhoneNumber(recipient.PhoneNumber);
                    recipient.PhoneNumber = normalizedPhone ?? string.Empty;
                }

                recipients.Add(recipient);
            }

            _logger.LogInformation("Successfully read {Count} recipients from CSV file (total rows: {TotalRows})", 
                recipients.Count, recordCount);
            return recipients;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading CSV file: {FilePath}", filePath);
            throw;
        }
    }

    private SmsRecipient MapCsvRecordToRecipient(CsvRecord csvRecord, string phoneNumberColumn, string? displayNameColumn, int recordNumber)
    {
        var recipient = new SmsRecipient();

        // Map phone number (required)
        recipient.PhoneNumber = GetFieldValue(csvRecord, phoneNumberColumn);
        
        // Map display name (optional, generate if not available)
        recipient.DisplayName = !string.IsNullOrEmpty(displayNameColumn) 
            ? GetFieldValue(csvRecord, displayNameColumn)
            : $"User {recordNumber}";


        // Map custom columns
        foreach (var customColumn in _config.CustomColumns)
        {
            var value = GetFieldValue(csvRecord, customColumn.Value);
            if (!string.IsNullOrEmpty(value))
            {
                recipient.CustomFields[customColumn.Key] = value;
            }
        }

        // Add all remaining fields as custom fields
        var configuredColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            phoneNumberColumn,
            displayNameColumn ?? string.Empty,
        };

        foreach (var field in csvRecord.Fields)
        {
            if (!configuredColumns.Contains(field.Key) && 
                !_config.CustomColumns.ContainsValue(field.Key) &&
                !string.IsNullOrEmpty(field.Value))
            {
                recipient.CustomFields[field.Key] = field.Value;
            }
        }

        return recipient;
    }

    private string DeterminePhoneNumberColumn(string[] headers)
    {
        // If phone number column is configured and exists, use it
        if (!string.IsNullOrEmpty(_config.PhoneNumberColumn))
        {
            var configuredColumn = headers.FirstOrDefault(h => 
                h.Equals(_config.PhoneNumberColumn, StringComparison.OrdinalIgnoreCase));
            if (configuredColumn != null)
            {
                return configuredColumn;
            }
            
            _logger.LogWarning("Configured phone number column '{Column}' not found in CSV headers", _config.PhoneNumberColumn);
        }

        // Try to find common phone number column names
        var commonPhoneColumns = new[] 
        { 
            "authmethodmobilenumbers", "profilemobilephone", // Azure AD specific
            "mobilenumber", "mobile", "phone", "phonenumber", 
            "cellphone", "cell", "telephone", "tel"
        };

        foreach (var commonName in commonPhoneColumns)
        {
            var foundColumn = headers.FirstOrDefault(h => 
                h.Equals(commonName, StringComparison.OrdinalIgnoreCase) ||
                h.IndexOf(commonName, StringComparison.OrdinalIgnoreCase) >= 0);
            if (foundColumn != null)
            {
                _logger.LogInformation("Auto-detected phone number column: '{Column}'", foundColumn);
                return foundColumn;
            }
        }

        // Default to first column if no specific phone column found
        if (headers.Length > 0)
        {
            _logger.LogInformation("Using first column '{Column}' as phone number column", headers[0]);
            return headers[0];
        }

        throw new InvalidOperationException("No columns found in CSV file");
    }

    private string? DetermineDisplayNameColumn(string[] headers)
    {
        // If display name column is configured and exists, use it
        if (!string.IsNullOrEmpty(_config.DisplayNameColumn))
        {
            var configuredColumn = headers.FirstOrDefault(h => 
                h.Equals(_config.DisplayNameColumn, StringComparison.OrdinalIgnoreCase));
            if (configuredColumn != null)
            {
                return configuredColumn;
            }
        }

        // Try to find common display name column names
        var commonNameColumns = new[] 
        { 
            "name", "displayname", "fullname", "username", "user", "firstname", "lastname"
        };

        foreach (var commonName in commonNameColumns)
        {
            var foundColumn = headers.FirstOrDefault(h => 
                h.Equals(commonName, StringComparison.OrdinalIgnoreCase) ||
                h.Contains(commonName, StringComparison.OrdinalIgnoreCase));
            if (foundColumn != null)
            {
                _logger.LogInformation("Auto-detected display name column: '{Column}'", foundColumn);
                return foundColumn;
            }
        }

        // Return null if no display name column found (will generate names)
        return null;
    }

    private static string GetFieldValue(CsvRecord csvRecord, string? columnName)
    {
        if (string.IsNullOrEmpty(columnName))
            return string.Empty;

        return csvRecord.Fields.TryGetValue(columnName, out var value) ? value : string.Empty;
    }
}
