using Microsoft.Extensions.Logging;

namespace BatchSMS.Services;

/// <summary>
/// Progress information for batch operations
/// </summary>
public class BatchProgress
{
    public int TotalItems { get; init; }
    public int ProcessedItems { get; init; }
    public int SuccessfulItems { get; init; }
    public int FailedItems { get; init; }
    public int SkippedItems { get; init; }
    public TimeSpan ElapsedTime { get; init; }
    public string? CurrentItem { get; init; }
    public double ProgressPercentage => TotalItems > 0 ? (double)ProcessedItems / TotalItems * 100 : 0;
    public double SuccessRate => ProcessedItems > 0 ? (double)SuccessfulItems / ProcessedItems * 100 : 0;
}

/// <summary>
/// Interface for reporting progress of batch operations
/// </summary>
public interface IProgressReporter
{
    /// <summary>
    /// Report progress of the current batch operation
    /// </summary>
    Task ReportProgressAsync(BatchProgress progress);
}

/// <summary>
/// Console-based progress reporter
/// </summary>
public class ConsoleProgressReporter : IProgressReporter
{
    private readonly ILogger<ConsoleProgressReporter> _logger;
    private DateTime _lastReportTime = DateTime.MinValue;
    private readonly TimeSpan _reportInterval = TimeSpan.FromSeconds(1); // Report at most once per second

    public ConsoleProgressReporter(ILogger<ConsoleProgressReporter> logger)
    {
        _logger = logger;
    }

    public Task ReportProgressAsync(BatchProgress progress)
    {
        var now = DateTime.UtcNow;
        var shouldReport = now - _lastReportTime >= _reportInterval || progress.ProcessedItems >= progress.TotalItems;
        
        if (!shouldReport && progress.ProcessedItems < progress.TotalItems)
            return Task.CompletedTask;

        _lastReportTime = now;

        var progressBar = CreateProgressBar(progress.ProgressPercentage);
        var eta = EstimateTimeRemaining(progress);
        var throughput = CalculateThroughput(progress);

        // Enhanced console output with throughput
        Console.Write($"\r{progressBar} {progress.ProcessedItems}/{progress.TotalItems} " +
                     $"({progress.ProgressPercentage:F1}%) " +
                     $"✅{progress.SuccessfulItems} ❌{progress.FailedItems} " +
                     $"⏱️{progress.ElapsedTime:mm\\:ss} " +
                     $"📈{throughput:F1}/min " +
                     $"{(eta.HasValue ? $"ETA: {eta.Value:mm\\:ss}" : "")}");

        // Log progress milestones
        if (progress.ProcessedItems % 100 == 0 || progress.ProcessedItems >= progress.TotalItems)
        {
            _logger.LogInformation("Progress update: {Processed}/{Total} ({Percentage:F1}%) - " +
                "Success: {Success}, Failed: {Failed}, Throughput: {Throughput:F1}/min, Elapsed: {Elapsed}",
                progress.ProcessedItems, progress.TotalItems, progress.ProgressPercentage,
                progress.SuccessfulItems, progress.FailedItems, throughput, progress.ElapsedTime);
        }

        if (progress.ProcessedItems >= progress.TotalItems)
        {
            Console.WriteLine(); // New line when complete
            _logger.LogInformation("Batch processing completed: {Processed}/{Total} items in {ElapsedTime}. " +
                "Final success rate: {SuccessRate:F1}%, Average throughput: {Throughput:F1}/min",
                progress.ProcessedItems, progress.TotalItems, progress.ElapsedTime, 
                progress.SuccessRate, throughput);
        }

        return Task.CompletedTask;
    }

    private static double CalculateThroughput(BatchProgress progress)
    {
        if (progress.ElapsedTime.TotalMinutes <= 0)
            return 0;
            
        return progress.ProcessedItems / progress.ElapsedTime.TotalMinutes;
    }

    private static string CreateProgressBar(double percentage)
    {
        const int barLength = 20;
        var filledLength = (int)(barLength * percentage / 100);
        var bar = new string('█', filledLength) + new string('░', barLength - filledLength);
        return $"[{bar}]";
    }

    private static TimeSpan? EstimateTimeRemaining(BatchProgress progress)
    {
        if (progress.ProcessedItems == 0 || progress.ElapsedTime.TotalSeconds < 1)
            return null;

        var remainingItems = progress.TotalItems - progress.ProcessedItems;
        var itemsPerSecond = progress.ProcessedItems / progress.ElapsedTime.TotalSeconds;
        
        if (itemsPerSecond <= 0)
            return null;

        var remainingSeconds = remainingItems / itemsPerSecond;
        return TimeSpan.FromSeconds(remainingSeconds);
    }
}
