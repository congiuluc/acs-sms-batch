using BatchSMS.Models;
using CsvHelper;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace BatchSMS.Services;

public interface IReportingService
{
    Task GenerateReportAsync(BatchResult batchResult, string outputPath);
    Task GenerateFailedRecipientsReportAsync(BatchResult batchResult, string outputPath);
    Task GenerateDetailedResultsCsvAsync(BatchResult batchResult, string outputPath);
}

public class ReportingService : IReportingService
{
    private readonly ILogger<ReportingService> _logger;

    public ReportingService(ILogger<ReportingService> logger)
    {
        _logger = logger;
    }

    public async Task GenerateReportAsync(BatchResult batchResult, string outputPath)
    {
        try
        {
            var reportData = new
            {
                Summary = new
                {
                    batchResult.TotalRecords,
                    batchResult.SuccessfulSends,
                    batchResult.FailedSends,
                    batchResult.SkippedRecords,
                    batchResult.StartTime,
                    batchResult.EndTime,
                    TotalDurationMinutes = batchResult.TotalDuration?.TotalMinutes ?? 0,
                    SuccessRate = batchResult.TotalRecords > 0 ? (double)batchResult.SuccessfulSends / batchResult.TotalRecords * 100 : 0
                },
                Results = batchResult.Results.Select(r => new
                {
                    PhoneNumber = r.RecipientPhoneNumber ?? r.PhoneNumber,
                    DisplayName = r.DisplayName ?? "",
                    r.IsSuccess,
                    r.MessageId,
                    r.ErrorMessage,
                    r.SentAt
                })
            };

            using var writer = new StreamWriter(outputPath);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            
            // Write summary
            await writer.WriteLineAsync("=== BATCH SMS REPORT ===");
            await writer.WriteLineAsync($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            await writer.WriteLineAsync($"Total Records: {reportData.Summary.TotalRecords}");
            await writer.WriteLineAsync($"Successful Sends: {reportData.Summary.SuccessfulSends}");
            await writer.WriteLineAsync($"Failed Sends: {reportData.Summary.FailedSends}");
            await writer.WriteLineAsync($"Skipped Records: {reportData.Summary.SkippedRecords}");
            await writer.WriteLineAsync($"Success Rate: {reportData.Summary.SuccessRate:F2}%");
            await writer.WriteLineAsync($"Start Time: {reportData.Summary.StartTime:yyyy-MM-dd HH:mm:ss}");
            await writer.WriteLineAsync($"End Time: {reportData.Summary.EndTime:yyyy-MM-dd HH:mm:ss}");
            await writer.WriteLineAsync($"Total Duration: {reportData.Summary.TotalDurationMinutes:F2} minutes");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("=== DETAILED RESULTS ===");

            // Write detailed results
            csv.WriteHeader<dynamic>();
            await csv.NextRecordAsync();
            
            foreach (var result in reportData.Results)
            {
                csv.WriteRecord(result);
                await csv.NextRecordAsync();
            }

            _logger.LogInformation("Report generated successfully: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report: {OutputPath}", outputPath);
            throw;
        }
    }

    public async Task GenerateFailedRecipientsReportAsync(BatchResult batchResult, string outputPath)
    {
        try
        {
            var failedResults = batchResult.Results.Where(r => !r.IsSuccess).ToList();
            
            if (!failedResults.Any())
            {
                _logger.LogInformation("No failed recipients to report");
                return;
            }

            using var writer = new StreamWriter(outputPath);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            await writer.WriteLineAsync("=== FAILED RECIPIENTS REPORT ===");
            await writer.WriteLineAsync($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            await writer.WriteLineAsync($"Total Failed: {failedResults.Count}");
            await writer.WriteLineAsync();

            csv.WriteHeader<SmsResult>();
            await csv.NextRecordAsync();

            foreach (var result in failedResults)
            {
                csv.WriteRecord(result);
                await csv.NextRecordAsync();
            }

            _logger.LogInformation("Failed recipients report generated: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating failed recipients report: {OutputPath}", outputPath);
            throw;
        }
    }

    public async Task GenerateDetailedResultsCsvAsync(BatchResult batchResult, string outputPath)
    {
        try
        {
            _logger.LogInformation("Generating detailed SMS results CSV: {OutputPath}", outputPath);

            using var writer = new StreamWriter(outputPath);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            // Define the CSV structure
            var csvRecords = batchResult.Results.Select(r => new
            {
                MobileNumber = r.RecipientPhoneNumber ?? r.PhoneNumber ?? "",
                DisplayName = r.DisplayName ?? "",
                SendTime = r.SentAt.ToString("yyyy-MM-dd HH:mm:ss"),
                SendTimeUtc = r.SentAt.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                SendResult = r.IsSuccess ? "SUCCESS" : "FAILED",
                MessageId = r.MessageId ?? "",
                ErrorMessage = r.ErrorMessage ?? "",
                DurationMs = (DateTime.UtcNow - r.SentAt).TotalMilliseconds.ToString("F0")
            }).ToList();

            // Write headers
            if (csvRecords.Any())
            {
                csv.WriteHeader(csvRecords.First().GetType());
                await csv.NextRecordAsync();

                // Write data
                foreach (var record in csvRecords)
                {
                    csv.WriteRecord(record);
                    await csv.NextRecordAsync();
                }
            }

            await csv.FlushAsync();
            _logger.LogInformation("Detailed SMS results CSV generated successfully: {OutputPath} ({RecordCount} records)", 
                outputPath, csvRecords.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating detailed results CSV: {OutputPath}", outputPath);
            throw;
        }
    }
}
