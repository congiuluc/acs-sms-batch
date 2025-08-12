using BatchSMS.Models;
using CsvHelper;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Threading.Channels;

namespace BatchSMS.Services;

/// <summary>
/// Service for writing SMS results to CSV in real-time
/// </summary>
public interface IRealTimeCsvWriter : IDisposable
{
    /// <summary>
    /// Initialize the CSV file with headers
    /// </summary>
    Task<Result> InitializeAsync(string filePath);

    /// <summary>
    /// Write a result to the CSV file asynchronously
    /// </summary>
    Task<Result> WriteResultAsync(SmsResult result, SmsRecipient recipient);

    /// <summary>
    /// Flush any pending writes and close the writer
    /// </summary>
    Task<Result> FlushAndCloseAsync();
}

/// <summary>
/// Thread-safe real-time CSV writer that queues writes to avoid blocking SMS operations
/// </summary>
public class RealTimeCsvWriter : IRealTimeCsvWriter, IDisposable
{
    private readonly ILogger<RealTimeCsvWriter> _logger;
    private readonly Channel<CsvRecord> _writeChannel;
    private readonly ChannelWriter<CsvRecord> _writer;
    private readonly ChannelReader<CsvRecord> _reader;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _writerTask;
    private StreamWriter? _fileWriter;
    private CsvWriter? _csvWriter;
    private bool _disposed;
    private bool _closed;
    private string? _filePath;

    private record CsvRecord(
        string MobileNumber,
        string DisplayName,
        DateTime SendTime,
        DateTime SendTimeUtc,
        string SendResult,
        string MessageId,
        string ErrorMessage,
        double DurationMs
    );

    public RealTimeCsvWriter(ILogger<RealTimeCsvWriter> logger)
    {
        _logger = logger;
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Create a bounded channel to prevent memory issues with large batches
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };

        _writeChannel = Channel.CreateBounded<CsvRecord>(options);
        _writer = _writeChannel.Writer;
        _reader = _writeChannel.Reader;

        // Start the background writer task
        _writerTask = Task.Run(ProcessWriteQueueAsync, _cancellationTokenSource.Token);
    }

    public async Task<Result> InitializeAsync(string filePath)
    {
        try
        {
            _filePath = filePath;

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _fileWriter = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
            _csvWriter = new CsvWriter(_fileWriter, CultureInfo.InvariantCulture);

            // Write headers
            _csvWriter.WriteField("MobileNumber");
            _csvWriter.WriteField("DisplayName");
            _csvWriter.WriteField("SendTime");
            _csvWriter.WriteField("SendTimeUtc");
            _csvWriter.WriteField("SendResult");
            _csvWriter.WriteField("MessageId");
            _csvWriter.WriteField("ErrorMessage");
            _csvWriter.WriteField("DurationMs");
            await _csvWriter.NextRecordAsync().ConfigureAwait(false);
            await _csvWriter.FlushAsync().ConfigureAwait(false);

            _logger.LogInformation("Real-time CSV writer initialized: {FilePath}", filePath);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize real-time CSV writer: {FilePath}", filePath);
            return Result.Failure("Failed to initialize CSV writer", ex);
        }
    }

    public async Task<Result> WriteResultAsync(SmsResult result, SmsRecipient recipient)
    {
        if (_disposed || _closed)
        {
            _logger.LogWarning("Attempted to write to disposed or closed CSV writer for {PhoneNumber}", recipient.PhoneNumber);
            return Result.Failure("CSV writer has been disposed or closed");
        }

        try
        {
            var duration = (DateTime.UtcNow - result.SentAt).TotalMilliseconds;
            var record = new CsvRecord(
                recipient.PhoneNumber ?? "",
                recipient.DisplayName ?? "",
                result.SentAt.ToLocalTime(),
                result.SentAt,
                result.IsSuccess ? "SUCCESS" : "FAILED",
                result.MessageId ?? "",
                result.ErrorMessage ?? "",
                Math.Round(duration, 2)
            );

            _logger.LogDebug("Queuing CSV write for {PhoneNumber} - Result: {Result}, Duration: {Duration}ms", 
                recipient.PhoneNumber, result.IsSuccess ? "SUCCESS" : "FAILED", duration);

            await _writer.WriteAsync(record, _cancellationTokenSource.Token).ConfigureAwait(false);
            
            _logger.LogTrace("CSV record queued successfully for {PhoneNumber}", recipient.PhoneNumber);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("CSV write operation was cancelled for {PhoneNumber}", recipient.PhoneNumber);
            return Result.Failure("Write operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue CSV write for {PhoneNumber}", recipient.PhoneNumber);
            return Result.Failure("Failed to queue CSV write", ex);
        }
    }

    public async Task<Result> FlushAndCloseAsync()
    {
        if (_closed || _disposed)
        {
            _logger.LogDebug("CSV writer already closed or disposed, skipping flush and close");
            return Result.Success();
        }

        try
        {
            _closed = true;

            // Signal that no more writes will be queued
            _writer.Complete();

            // Wait for all queued writes to complete with timeout
            try
            {
                await _writerTask.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("CSV writer task did not complete within 10 seconds during close");
                _cancellationTokenSource.Cancel();
            }

            // Close file resources safely
            await CloseFileResourcesAsync().ConfigureAwait(false);

            _logger.LogInformation("Real-time CSV writer closed successfully: {FilePath}", _filePath);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing real-time CSV writer: {FilePath}", _filePath);
            return Result.Failure("Failed to close CSV writer", ex);
        }
    }

    private async Task CloseFileResourcesAsync()
    {
        // Close CSV writer first
        var csvWriter = _csvWriter;
        if (csvWriter != null)
        {
            _csvWriter = null; // Clear reference first to prevent race conditions
            try
            {
                await csvWriter.FlushAsync().ConfigureAwait(false);
                await csvWriter.DisposeAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
                _logger.LogDebug("CSV writer was already disposed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing CSV writer");
            }
        }

        // Close file writer
        var fileWriter = _fileWriter;
        if (fileWriter != null)
        {
            _fileWriter = null; // Clear reference first to prevent race conditions
            try
            {
                await fileWriter.FlushAsync().ConfigureAwait(false);
                await fileWriter.DisposeAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
                _logger.LogDebug("File writer was already disposed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing file writer");
            }
        }
    }

    private async Task ProcessWriteQueueAsync()
    {
        try
        {
            await foreach (var record in _reader.ReadAllAsync(_cancellationTokenSource.Token).ConfigureAwait(false))
            {
                var csvWriter = _csvWriter; // Get local reference to avoid race conditions
                if (csvWriter == null)
                {
                    _logger.LogWarning("CSV writer not initialized, skipping record for {PhoneNumber}", record.MobileNumber);
                    continue;
                }

                try
                {
                    csvWriter.WriteField(record.MobileNumber);
                    csvWriter.WriteField(record.DisplayName);
                    csvWriter.WriteField(record.SendTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    csvWriter.WriteField(record.SendTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"));
                    csvWriter.WriteField(record.SendResult);
                    csvWriter.WriteField(record.MessageId);
                    csvWriter.WriteField(record.ErrorMessage);
                    csvWriter.WriteField(record.DurationMs);
                    await csvWriter.NextRecordAsync().ConfigureAwait(false);
                    await csvWriter.FlushAsync().ConfigureAwait(false);

                    _logger.LogDebug("CSV record written for {PhoneNumber}", record.MobileNumber);
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogDebug("CSV writer was disposed while writing record for {PhoneNumber}", record.MobileNumber);
                    break; // Stop processing if writer is disposed
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("closed") || ex.Message.Contains("disposed"))
                {
                    _logger.LogDebug("CSV writer was closed while writing record for {PhoneNumber}: {Message}", record.MobileNumber, ex.Message);
                    break; // Stop processing if writer is closed
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error writing CSV record for {PhoneNumber}", record.MobileNumber);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("CSV write queue processing cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CSV write queue processing");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            // Signal cancellation to stop background processing
            _cancellationTokenSource.Cancel();
            
            // Only complete the writer if it hasn't been completed already
            if (!_closed)
            {
                _writer.TryComplete();
            }
            
            // Give the writer task a chance to complete gracefully
            try
            {
                if (!_writerTask.Wait(TimeSpan.FromSeconds(5)))
                {
                    _logger.LogWarning("CSV writer task did not complete within timeout during disposal");
                }
            }
            catch (AggregateException ex)
            {
                _logger.LogDebug(ex, "Writer task completed with exceptions during disposal");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CSV writer disposal");
        }
        finally
        {
            // Dispose resources synchronously as a fallback
            try
            {
                var csvWriter = _csvWriter;
                if (csvWriter != null)
                {
                    _csvWriter = null;
                    csvWriter.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error disposing CSV writer during cleanup");
            }

            try
            {
                var fileWriter = _fileWriter;
                if (fileWriter != null)
                {
                    _fileWriter = null;
                    fileWriter.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error disposing file writer during cleanup");
            }

            try
            {
                _cancellationTokenSource.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error disposing cancellation token source");
            }
        }
    }
}
