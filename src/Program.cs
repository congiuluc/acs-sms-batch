using BatchSMS.Configuration;
using BatchSMS.Models;
using BatchSMS.Services;
using BatchSMS.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace BatchSMS;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Ensure logs directory exists
        var logsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        if (!Directory.Exists(logsDirectory))
        {
            Directory.CreateDirectory(logsDirectory);
        }

        // Configure Serilog programmatically to avoid assembly loading issues
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("Azure", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File("logs/batchsms-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: 7)
            .CreateLogger();

        try
        {
            Log.Information("Starting BatchSMS application");

            // Check if this is a validation request
            if (args.Length > 0 && args[0].Equals("validate", StringComparison.OrdinalIgnoreCase))
            {
                return await CsvValidatorTool.RunAsync(args.Skip(1).ToArray());
            }

            // Show help if requested
            if (args.Length > 0 && (args[0].Equals("--help", StringComparison.OrdinalIgnoreCase) || args[0].Equals("-h", StringComparison.OrdinalIgnoreCase)))
            {
                ShowHelp();
                return 0;
            }

            var host = CreateHostBuilder(args).Build();

            var batchSmsService = host.Services.GetRequiredService<BatchSmsApplication>();
            await batchSmsService.RunAsync(args);
            
            Log.Information("BatchSMS application completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("BatchSMS - Azure Communication Services Bulk SMS Sender");
        Console.WriteLine();
        Console.WriteLine("USAGE:");
        Console.WriteLine("  dotnet run                                    # Run with default configuration");
        Console.WriteLine("  dotnet run -- --csv <file> --output <dir>    # Run with custom CSV file and output directory");
        Console.WriteLine("  dotnet run validate <csv-file>               # Validate CSV file before sending");
        Console.WriteLine("  dotnet run --help                            # Show this help message");
        Console.WriteLine();
        Console.WriteLine("OPTIONS:");
        Console.WriteLine("  --csv, -c <file>       Path to CSV file containing recipients");
        Console.WriteLine("  --output, -o <dir>     Output directory for reports (default: Reports)");
        Console.WriteLine("  --help, -h             Show help information");
        Console.WriteLine();
        Console.WriteLine("EXAMPLES:");
        Console.WriteLine("  dotnet run -- --csv \"recipients.csv\" --output \"MyReports\"");
        Console.WriteLine("  dotnet run validate sample.csv");
        Console.WriteLine();
        Console.WriteLine("For more information, see README.md");
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog() // Use Serilog for logging
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddEnvironmentVariables();
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration
                services.Configure<AzureCommunicationServicesConfig>(
                    context.Configuration.GetSection("AzureCommunicationServices"));
                services.Configure<SmsConfig>(
                    context.Configuration.GetSection("SmsConfiguration"));
                services.Configure<RateLimitingConfig>(
                    context.Configuration.GetSection("RateLimiting"));
                services.Configure<CsvConfig>(
                    context.Configuration.GetSection("CsvConfiguration"));

                // Services
                services.AddSingleton<ICsvReaderService, CsvReaderService>();
                services.AddSingleton<IRateLimitingService, RateLimitingService>();
                services.AddSingleton<ISmsService, SmsService>();
                services.AddSingleton<IReportingService, ReportingService>();
                services.AddTransient<IRealTimeCsvWriter, RealTimeCsvWriter>();
                services.AddTransient<IProgressReporter, ConsoleProgressReporter>();
                services.AddSingleton<BatchSmsApplication>();
            });
}

public class BatchSmsApplication
{
    private readonly ICsvReaderService _csvReaderService;
    private readonly ISmsService _smsService;
    private readonly IReportingService _reportingService;
    private readonly IRealTimeCsvWriter _realTimeCsvWriter;
    private readonly IProgressReporter _progressReporter;
    private readonly ILogger<BatchSmsApplication> _logger;

    public BatchSmsApplication(
        ICsvReaderService csvReaderService,
        ISmsService smsService,
        IReportingService reportingService,
        IRealTimeCsvWriter realTimeCsvWriter,
        IProgressReporter progressReporter,
        ILogger<BatchSmsApplication> logger)
    {
        _csvReaderService = csvReaderService;
        _smsService = smsService;
        _reportingService = reportingService;
        _realTimeCsvWriter = realTimeCsvWriter;
        _progressReporter = progressReporter;
        _logger = logger;
    }

    /// <summary>
    /// Main application entry point that orchestrates the batch SMS sending process
    /// </summary>
    /// <param name="args">Command line arguments containing CSV file path and output directory</param>
    public async Task RunAsync(string[] args)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting BatchSMS application at {StartTime:yyyy-MM-dd HH:mm:ss} UTC", startTime);
        _logger.LogDebug("Command line arguments: {Args}", string.Join(" ", args));

        try
        {
            // Parse command line arguments
            _logger.LogDebug("Parsing command line arguments");
            var csvFilePath = GetCsvFilePath(args);
            var outputDirectory = GetOutputDirectory(args);
            
            _logger.LogInformation("Input CSV file: {CsvFilePath}", csvFilePath);
            _logger.LogInformation("Output directory: {OutputDirectory}", outputDirectory);

            // Ensure directories exist
            _logger.LogDebug("Ensuring output directory exists");
            var result = EnsureDirectoriesExist(outputDirectory);
            if (result.IsFailure)
            {
                _logger.LogError("Failed to create directories: {Error}", result.Error);
                return;
            }
            _logger.LogDebug("Output directory validated successfully");

            // Validate configuration
            _logger.LogDebug("Validating application configuration");
            var configResult = ValidateConfiguration();
            if (configResult.IsFailure)
            {
                _logger.LogError("Configuration validation failed: {Error}", configResult.Error);
                return;
            }

            // Read recipients from CSV
            var recipientsResult = await ReadRecipientsAsync(csvFilePath);
            if (recipientsResult.IsFailure)
            {
                _logger.LogError("Failed to read recipients: {Error}", recipientsResult.Error);
                return;
            }

            var recipients = recipientsResult.Value;
            if (!recipients.Any())
            {
                _logger.LogWarning("No recipients found in CSV file");
                return;
            }

            _logger.LogInformation("Found {Count} recipients to process", recipients.Count);

            // Setup file paths with timestamp
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var reportPath = Path.Combine(outputDirectory, $"sms_report_{timestamp}.csv");
            var failedReportPath = Path.Combine(outputDirectory, $"failed_recipients_{timestamp}.csv");
            var detailedResultsPath = Path.Combine(outputDirectory, $"sms_detailed_results_{timestamp}.csv");

            // Initialize real-time CSV writer
            var csvInitResult = await _realTimeCsvWriter.InitializeAsync(detailedResultsPath);
            if (csvInitResult.IsFailure)
            {
                _logger.LogError("Failed to initialize real-time CSV writer: {Error}", csvInitResult.Error);
                return;
            }

            _logger.LogInformation("Real-time CSV results will be written to: {DetailedResultsPath}", detailedResultsPath);

            try
            {
                // Send SMS messages with real-time CSV writing and progress reporting
                using var cts = new CancellationTokenSource();
                
                // Handle Ctrl+C gracefully
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                    _logger.LogWarning("Cancellation requested by user");
                };

                var batchResult = await _smsService.SendBatchSmsAsync(
                    recipients, 
                    _progressReporter, 
                    _realTimeCsvWriter, 
                    cts.Token);

                if (batchResult.IsFailure)
                {
                    _logger.LogError("Batch SMS sending failed: {Error}", batchResult.Error);
                    return;
                }

                var batch = batchResult.Value;

                // Generate remaining reports (summary and failed recipients)
                await _reportingService.GenerateReportAsync(batch, reportPath);
                await _reportingService.GenerateFailedRecipientsReportAsync(batch, failedReportPath);

                // Summary
                _logger.LogInformation("=== BATCH SMS SUMMARY ===");
                _logger.LogInformation("Total Recipients: {Total}", batch.TotalRecords);
                _logger.LogInformation("Successful Sends: {Success}", batch.SuccessfulSends);
                _logger.LogInformation("Failed Sends: {Failed}", batch.FailedSends);
                _logger.LogInformation("Skipped Records: {Skipped}", batch.SkippedRecords);
                _logger.LogInformation("Success Rate: {SuccessRate:F2}%", 
                    batch.TotalRecords > 0 ? (double)batch.SuccessfulSends / batch.TotalRecords * 100 : 0);
                _logger.LogInformation("Total Duration: {Duration}", batch.TotalDuration);
                _logger.LogInformation("Report saved to: {ReportPath}", reportPath);
                _logger.LogInformation("Detailed results saved to: {DetailedReportPath}", detailedResultsPath);
                
                if (batch.FailedSends > 0)
                {
                    _logger.LogInformation("Failed recipients report saved to: {FailedReportPath}", failedReportPath);
                }

                _logger.LogInformation("BatchSMS application completed successfully");
            }
            finally
            {
                // Ensure CSV writer is properly closed
                var closeResult = await _realTimeCsvWriter.FlushAndCloseAsync();
                if (closeResult.IsFailure)
                {
                    _logger.LogWarning("Failed to close CSV writer cleanly: {Error}", closeResult.Error);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in BatchSMS application");
            throw;
        }
    }

    /// <summary>
    /// Ensures all required directories exist for the application
    /// </summary>
    /// <param name="outputDirectory">The output directory for reports and CSV files</param>
    /// <returns>Result indicating success or failure of directory creation</returns>
    private Result EnsureDirectoriesExist(string outputDirectory)
    {
        try
        {
            _logger.LogDebug("Checking output directory existence: {OutputDirectory}", outputDirectory);
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                _logger.LogInformation("Created output directory: {OutputDirectory}", outputDirectory);
            }
            else
            {
                _logger.LogDebug("Output directory already exists: {OutputDirectory}", outputDirectory);
            }

            var logsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            _logger.LogDebug("Checking logs directory existence: {LogsDirectory}", logsDirectory);
            if (!Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
                _logger.LogInformation("Created logs directory: {LogsDirectory}", logsDirectory);
            }
            else
            {
                _logger.LogDebug("Logs directory already exists: {LogsDirectory}", logsDirectory);
            }

            _logger.LogDebug("All required directories validated successfully");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create required directories");
            return Result.Failure("Failed to create required directories", ex);
        }
    }

    /// <summary>
    /// Validates the application configuration including Azure Communication Services settings
    /// </summary>
    /// <returns>Result indicating success or failure of configuration validation</returns>
    private Result ValidateConfiguration()
    {
        try
        {
            _logger.LogDebug("Starting configuration validation");
            
            var validationResult = _smsService.ValidateConfiguration();
            if (validationResult.IsFailure)
            {
                _logger.LogError("SMS service configuration validation failed: {Error}", validationResult.Error);
                return validationResult;
            }

            _logger.LogDebug("SMS service configuration validated successfully");
            _logger.LogInformation("All configuration validation completed successfully");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during configuration validation");
            return Result.Failure("Configuration validation failed", ex);
        }
    }

    /// <summary>
    /// Reads SMS recipients from the specified CSV file
    /// </summary>
    /// <param name="csvFilePath">Path to the CSV file containing recipient data</param>
    /// <returns>Result containing the list of SMS recipients or failure information</returns>
    private async Task<Result<List<SmsRecipient>>> ReadRecipientsAsync(string? csvFilePath)
    {
        try
        {
            var filePath = csvFilePath ?? "default configuration";
            _logger.LogDebug("Starting CSV file reading process: {CsvFilePath}", filePath);
            
            var recipients = string.IsNullOrEmpty(csvFilePath) 
                ? await _csvReaderService.ReadRecipientsAsync()
                : await _csvReaderService.ReadRecipientsAsync(csvFilePath);

            _logger.LogDebug("Successfully read {RecipientCount} recipients from CSV", recipients.Count);
            _logger.LogInformation("CSV file processing completed successfully: {CsvFilePath}", filePath);
            
            return Result<List<SmsRecipient>>.Success(recipients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read recipients from CSV file: {CsvFilePath}", csvFilePath ?? "default");
            return Result<List<SmsRecipient>>.Failure("Failed to read recipients from CSV", ex);
        }
    }

    /// <summary>
    /// Extracts the CSV file path from command line arguments
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>CSV file path if specified, null otherwise</returns>
    private static string? GetCsvFilePath(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--csv", StringComparison.OrdinalIgnoreCase) || 
                args[i].Equals("-c", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }

    /// <summary>
    /// Extracts the output directory from command line arguments or returns default
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Output directory path</returns>
    private static string GetOutputDirectory(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--output", StringComparison.OrdinalIgnoreCase) || 
                args[i].Equals("-o", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return Path.Combine(Directory.GetCurrentDirectory(), "Reports");
    }
}
