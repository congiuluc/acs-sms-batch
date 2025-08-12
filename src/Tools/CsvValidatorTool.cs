using BatchSMS.Configuration;
using BatchSMS.Utilities;
using Microsoft.Extensions.Configuration;

namespace BatchSMS.Tools;

public class CsvValidatorTool
{
    public static Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("CSV Validator Tool");
            Console.WriteLine("Usage: dotnet run validate <csv-file-path>");
            Console.WriteLine("Example: dotnet run validate sample.csv");
            return Task.FromResult(1);
        }

        var csvFilePath = args[0];
        
        Console.WriteLine($"Validating CSV file: {csvFilePath}");
        Console.WriteLine(new string('=', 50));

        // Load configuration to get column mappings
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var csvConfig = new CsvConfig();
        configuration.GetSection("CsvConfiguration").Bind(csvConfig);

        // Use configured column names
        var result = PhoneNumberValidator.ValidateCsvFile(
            csvFilePath, 
            csvConfig.PhoneNumberColumn, 
            csvConfig.DisplayNameColumn);

        // Display summary
        Console.WriteLine($"Total Records: {result.TotalRecords}");
        Console.WriteLine($"Valid Records: {result.ValidRecords}");
        Console.WriteLine($"Invalid Records: {result.InvalidPhoneNumbers.Count}");
        Console.WriteLine($"Success Rate: {result.SuccessRate:F1}%");
        Console.WriteLine();

        // Display configuration info
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("CONFIGURATION:");
        Console.WriteLine($"  Phone Number Column: {result.ActualPhoneColumn ?? "Auto-detect (defaults to first column)"}");
        Console.WriteLine($"  Display Name Column: {result.ActualDisplayColumn ?? "Auto-detect (generates if not found)"}");
        Console.ResetColor();
        Console.WriteLine();

        // Display errors
        if (result.Errors.Any())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERRORS:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"  ❌ {error}");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        // Display warnings
        if (result.Warnings.Any())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("WARNINGS:");
            foreach (var warning in result.Warnings)
            {
                Console.WriteLine($"  ⚠️  {warning}");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        // Display invalid phone numbers
        if (result.InvalidPhoneNumbers.Any())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("INVALID PHONE NUMBERS:");
            foreach (var invalid in result.InvalidPhoneNumbers.Take(10)) // Show first 10
            {
                Console.WriteLine($"  ❌ Row {invalid.RowNumber}: '{invalid.PhoneNumber}' ({invalid.DisplayName}) - {invalid.Error}");
            }
            
            if (result.InvalidPhoneNumbers.Count > 10)
            {
                Console.WriteLine($"  ... and {result.InvalidPhoneNumbers.Count - 10} more invalid phone numbers");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        // Final verdict
        if (result.IsValid)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ CSV file is valid and ready for SMS batch processing!");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ CSV file has issues that need to be resolved before SMS batch processing.");
        }
        Console.ResetColor();

        // Recommendations
        Console.WriteLine();
        Console.WriteLine("RECOMMENDATIONS:");
        if (result.InvalidPhoneNumbers.Any())
        {
            Console.WriteLine("  • Fix invalid phone numbers (ensure international format: +country_code_number)");
        }
        if (result.Warnings.Any())
        {
            Console.WriteLine("  • Review warnings for potential issues");
        }
        if (result.ValidRecords > 0)
        {
            Console.WriteLine($"  • {result.ValidRecords} records are ready for SMS sending");
        }

        return Task.FromResult(result.IsValid ? 0 : 1);
    }
}
