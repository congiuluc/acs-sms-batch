using System.Globalization;
using System.Text.RegularExpressions;
using BatchSMS.Models;
using CsvHelper;
using CsvHelper.Configuration;

namespace BatchSMS.Utilities;

public static class PhoneNumberValidator
{
    private static readonly Regex PhoneRegex = new(@"^\+[1-9]\d{1,14}$", RegexOptions.Compiled);
    
    /// <summary>
    /// Validates if a phone number is in valid international format
    /// </summary>
    /// <param name="phoneNumber">Phone number to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidPhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        var trimmed = phoneNumber.Trim();
        
        // Must start with + and contain only digits after that
        // Must be between 3 and 16 characters total (+ plus 2-15 digits)
        return PhoneRegex.IsMatch(trimmed);
    }

    /// <summary>
    /// Normalizes a phone number to standard international format
    /// If multiple numbers are provided (separated by ;, |, or ,), uses only the first one
    /// Automatically adds +39 prefix for Italian numbers that don't start with + or +39
    /// </summary>
    /// <param name="phoneNumber">Phone number to normalize</param>
    /// <returns>Normalized phone number or null if invalid</returns>
    public static string? NormalizePhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return null;

        // Handle multiple phone numbers separated by semicolon, comma, or pipe
        // Use only the first phone number
        var delimiters = new[] { ';', ',', '|' };
        var firstNumber = phoneNumber.Trim();
        
        foreach (var delimiter in delimiters)
        {
            if (phoneNumber.Contains(delimiter))
            {
                firstNumber = phoneNumber.Split(delimiter)[0].Trim();
                break;
            }
        }

        var cleaned = firstNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        
        // Handle different phone number formats
        if (cleaned.StartsWith('+'))
        {
            // Already has international prefix
            return IsValidPhoneNumber(cleaned) ? cleaned : null;
        }
        else if (cleaned.StartsWith("39") && cleaned.Length >= 12)
        {
            // Starts with 39 (Italy country code) but missing +
            cleaned = "+" + cleaned;
        }
        else if (cleaned.StartsWith("3") && cleaned.Length >= 10)
        {
            // Looks like an Italian mobile number (starts with 3) - add +39
            cleaned = "+39" + cleaned;
        }
        else if (cleaned.Length >= 9 && cleaned.Length <= 10 && !cleaned.StartsWith("0"))
        {
            // Likely Italian mobile number without country code - add +39
            cleaned = "+39" + cleaned;
        }
        else if (!cleaned.StartsWith('+'))
        {
            // Default: add + prefix and let validation determine if it's valid
            cleaned = "+" + cleaned;
        }

        return IsValidPhoneNumber(cleaned) ? cleaned : null;
    }

    /// <summary>
    /// Validates a CSV file and returns validation results
    /// </summary>
    /// <param name="csvFilePath">Path to the CSV file</param>
    /// <param name="phoneNumberColumn">Name of the phone number column. If null/empty, auto-detects or uses first column</param>
    /// <param name="displayNameColumn">Name of the display name column. If null/empty, auto-detects or generates names</param>
    /// <returns>Validation results</returns>
    public static CsvValidationResult ValidateCsvFile(string csvFilePath, string? phoneNumberColumn = null, string? displayNameColumn = null)
    {
        var result = new CsvValidationResult();

        try
        {
            if (!File.Exists(csvFilePath))
            {
                result.Errors.Add($"CSV file not found: {csvFilePath}");
                return result;
            }

            using var reader = new StringReader(File.ReadAllText(csvFilePath));
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            });

            // Read headers
            csv.Read();
            csv.ReadHeader();
            var headers = csv.HeaderRecord;

            if (headers == null)
            {
                result.Errors.Add("CSV file has no headers");
                return result;
            }

            // Check if required columns exist
            var actualPhoneColumn = DeterminePhoneNumberColumn(headers, phoneNumberColumn);
            var actualDisplayColumn = DetermineDisplayNameColumn(headers, displayNameColumn);

            // Store detected columns in result
            result.ActualPhoneColumn = actualPhoneColumn;
            result.ActualDisplayColumn = actualDisplayColumn;

            if (string.IsNullOrEmpty(actualPhoneColumn))
            {
                result.Errors.Add("No phone number column found or detected in CSV file");
                return result;
            }

            // If critical errors, return early
            if (result.Errors.Any())
            {
                return result;
            }

            // Validate records
            int rowNumber = 1; // Start from 1 for header
            while (csv.Read())
            {
                rowNumber++;
                result.TotalRecords++;

                try
                {
                    var phoneNumber = csv.GetField(actualPhoneColumn);
                    var displayName = !string.IsNullOrEmpty(actualDisplayColumn) 
                        ? csv.GetField(actualDisplayColumn) ?? $"Row {rowNumber}"
                        : $"Row {rowNumber}";

                    if (string.IsNullOrWhiteSpace(phoneNumber))
                    {
                        result.InvalidPhoneNumbers.Add(new InvalidPhoneNumberRecord
                        {
                            RowNumber = rowNumber,
                            PhoneNumber = phoneNumber ?? "",
                            DisplayName = displayName,
                            Error = "Phone number is empty"
                        });
                        continue;
                    }

                    if (!IsValidPhoneNumber(phoneNumber))
                    {
                        var normalized = NormalizePhoneNumber(phoneNumber);
                        if (normalized != null)
                        {
                            result.Warnings.Add($"Row {rowNumber}: Phone number '{phoneNumber}' was normalized to '{normalized}'");
                            result.ValidRecords++;
                        }
                        else
                        {
                            result.InvalidPhoneNumbers.Add(new InvalidPhoneNumberRecord
                            {
                                RowNumber = rowNumber,
                                PhoneNumber = phoneNumber,
                                DisplayName = displayName,
                                Error = "Invalid phone number format (must be international format: +countrycode number)"
                            });
                        }
                    }
                    else
                    {
                        result.ValidRecords++;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error reading row {rowNumber}: {ex.Message}");
                }
            }

            // Additional validation
            if (result.TotalRecords == 0)
            {
                result.Warnings.Add("CSV file contains no data records");
            }

            if (result.ValidRecords == 0 && result.TotalRecords > 0)
            {
                result.Errors.Add("No valid phone numbers found in CSV file");
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error reading CSV file: {ex.Message}");
        }

        return result;
    }

    private static string? DeterminePhoneNumberColumn(string[] headers, string? configuredColumn)
    {
        // If phone number column is configured and exists, use it
        if (!string.IsNullOrEmpty(configuredColumn))
        {
            var foundColumn = headers.FirstOrDefault(h => 
                h.Equals(configuredColumn, StringComparison.OrdinalIgnoreCase));
            if (foundColumn != null)
            {
                return foundColumn;
            }
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
                return foundColumn;
            }
        }

        // Default to first column if no specific phone column found
        return headers.Length > 0 ? headers[0] : null;
    }

    private static string? DetermineDisplayNameColumn(string[] headers, string? configuredColumn)
    {
        // If display name column is configured and exists, use it
        if (!string.IsNullOrEmpty(configuredColumn))
        {
            var foundColumn = headers.FirstOrDefault(h => 
                h.Equals(configuredColumn, StringComparison.OrdinalIgnoreCase));
            if (foundColumn != null)
            {
                return foundColumn;
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
                return foundColumn;
            }
        }

        // Return null if no display name column found
        return null;
    }
}

/// <summary>
/// Results of CSV validation
/// </summary>
public class CsvValidationResult
{
    public int TotalRecords { get; set; }
    public int ValidRecords { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<InvalidPhoneNumberRecord> InvalidPhoneNumbers { get; set; } = new();
    public string? ActualPhoneColumn { get; set; }
    public string? ActualDisplayColumn { get; set; }

    public bool IsValid => !Errors.Any() && ValidRecords > 0;
    public double SuccessRate => TotalRecords > 0 ? (double)ValidRecords / TotalRecords * 100 : 0;
}

/// <summary>
/// Information about an invalid phone number record
/// </summary>
public class InvalidPhoneNumberRecord
{
    public int RowNumber { get; set; }
    public string PhoneNumber { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Error { get; set; } = "";
}
