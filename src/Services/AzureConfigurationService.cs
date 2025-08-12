using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using BatchSMS.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace BatchSMS.Services;

/// <summary>
/// Service responsible for retrieving Azure Communication Services configuration
/// from Azure Key Vault with environment variable fallback
/// </summary>
public interface IAzureConfigurationService
{
    /// <summary>
    /// Get the Azure Communication Services connection string
    /// </summary>
    Task<string> GetConnectionStringAsync();

    /// <summary>
    /// Get the from phone number for SMS sending
    /// </summary>
    Task<string> GetFromPhoneNumberAsync();

    /// <summary>
    /// Validate that all required configuration is available
    /// </summary>
    Task<bool> ValidateConfigurationAsync();
}

public class AzureConfigurationService : IAzureConfigurationService
{
    private readonly KeyVaultConfig _keyVaultConfig;
    private readonly AzureCommunicationServicesConfig _acsConfig;
    private readonly ILogger<AzureConfigurationService> _logger;
    private readonly SecretClient? _secretClient;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);
    private bool _keyVaultAvailable;
    private bool _initialized;

    // Cache for secrets to avoid repeated Key Vault calls
    private string? _cachedConnectionString;
    private string? _cachedFromPhoneNumber;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);
    private DateTime _connectionStringCacheTime;
    private DateTime _fromPhoneNumberCacheTime;

    public AzureConfigurationService(
        IOptions<KeyVaultConfig> keyVaultConfig,
        IOptions<AzureCommunicationServicesConfig> acsConfig,
        ILogger<AzureConfigurationService> logger)
    {
        _keyVaultConfig = keyVaultConfig.Value;
        _acsConfig = acsConfig.Value;
        _logger = logger;

        // Initialize Key Vault client if enabled and vault URI is provided
        if (_keyVaultConfig.Enabled && !string.IsNullOrWhiteSpace(_keyVaultConfig.VaultUri))
        {
            try
            {
                var vaultUri = new Uri(_keyVaultConfig.VaultUri);
                _secretClient = new SecretClient(vaultUri, new DefaultAzureCredential());
                _logger.LogInformation("Azure Key Vault client initialized with URI: {VaultUri}", _keyVaultConfig.VaultUri);
            }
            catch (UriFormatException ex)
            {
                _logger.LogError("Invalid Key Vault URI format '{VaultUri}': {ErrorMessage}. Please ensure the URI is in the format 'https://your-keyvault-name.vault.azure.net/'", 
                    _keyVaultConfig.VaultUri, ex.Message);
                _secretClient = null;
            }
            catch (Azure.Identity.AuthenticationFailedException ex)
            {
                _logger.LogWarning("Azure authentication failed during Key Vault client initialization: {ErrorMessage}. Will fall back to environment variables and configuration.", ex.Message);
                _secretClient = null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Key Vault client with URI '{VaultUri}': {ErrorMessage}. Will fall back to environment variables and configuration.", 
                    _keyVaultConfig.VaultUri, ex.Message);
                _secretClient = null;
            }
        }
        else
        {
            _logger.LogInformation("Key Vault is disabled or not configured. Using environment variables and configuration fallback.");
            _secretClient = null;
        }
    }

    public async Task<string> GetConnectionStringAsync()
    {
        await EnsureInitializedAsync();

        // Check cache first
        if (!string.IsNullOrEmpty(_cachedConnectionString) && 
            DateTime.UtcNow - _connectionStringCacheTime < _cacheExpiry)
        {
            _logger.LogDebug("Returning cached connection string");
            return _cachedConnectionString;
        }

        string connectionString;

        // Try Key Vault first if available
        if (_keyVaultAvailable && _secretClient != null)
        {
            connectionString = await GetSecretFromKeyVaultAsync(_keyVaultConfig.ConnectionStringSecretName);
            if (!string.IsNullOrEmpty(connectionString))
            {
                _logger.LogInformation("Successfully retrieved connection string from Key Vault");
                _cachedConnectionString = connectionString;
                _connectionStringCacheTime = DateTime.UtcNow;
                return connectionString;
            }
        }

        // Fallback to environment variable
        connectionString = Environment.GetEnvironmentVariable("ACS_CONNECTION_STRING") ?? string.Empty;
        if (!string.IsNullOrEmpty(connectionString))
        {
            _logger.LogInformation("Successfully retrieved connection string from environment variable ACS_CONNECTION_STRING");
            _cachedConnectionString = connectionString;
            _connectionStringCacheTime = DateTime.UtcNow;
            return connectionString;
        }

        // Final fallback to configuration
        connectionString = _acsConfig.ConnectionString;
        if (!string.IsNullOrEmpty(connectionString))
        {
            _logger.LogInformation("Using connection string from configuration (consider moving to Key Vault for production)");
            _cachedConnectionString = connectionString;
            _connectionStringCacheTime = DateTime.UtcNow;
            return connectionString;
        }

        _logger.LogError("No connection string found in Key Vault, environment variables, or configuration");
        throw new InvalidOperationException("Azure Communication Services connection string is not configured. Please set it in Key Vault, environment variable ACS_CONNECTION_STRING, or configuration.");
    }

    public async Task<string> GetFromPhoneNumberAsync()
    {
        await EnsureInitializedAsync();

        // Check cache first
        if (!string.IsNullOrEmpty(_cachedFromPhoneNumber) && 
            DateTime.UtcNow - _fromPhoneNumberCacheTime < _cacheExpiry)
        {
            _logger.LogDebug("Returning cached from phone number");
            return _cachedFromPhoneNumber;
        }

        string fromPhoneNumber;

        // Try Key Vault first if available
        if (_keyVaultAvailable && _secretClient != null)
        {
            fromPhoneNumber = await GetSecretFromKeyVaultAsync(_keyVaultConfig.FromPhoneNumberSecretName);
            if (!string.IsNullOrEmpty(fromPhoneNumber))
            {
                _logger.LogInformation("Successfully retrieved from phone number from Key Vault");
                _cachedFromPhoneNumber = fromPhoneNumber;
                _fromPhoneNumberCacheTime = DateTime.UtcNow;
                return fromPhoneNumber;
            }
        }

        // Fallback to environment variable
        fromPhoneNumber = Environment.GetEnvironmentVariable("ACS_FROM_PHONE_NUMBER") ?? string.Empty;
        if (!string.IsNullOrEmpty(fromPhoneNumber))
        {
            _logger.LogInformation("Successfully retrieved from phone number from environment variable ACS_FROM_PHONE_NUMBER");
            _cachedFromPhoneNumber = fromPhoneNumber;
            _fromPhoneNumberCacheTime = DateTime.UtcNow;
            return fromPhoneNumber;
        }

        // Final fallback to configuration
        fromPhoneNumber = _acsConfig.FromPhoneNumber;
        if (!string.IsNullOrEmpty(fromPhoneNumber))
        {
            _logger.LogInformation("Using from phone number from configuration (consider moving to Key Vault for production)");
            _cachedFromPhoneNumber = fromPhoneNumber;
            _fromPhoneNumberCacheTime = DateTime.UtcNow;
            return fromPhoneNumber;
        }

        _logger.LogError("No from phone number found in Key Vault, environment variables, or configuration");
        throw new InvalidOperationException("Azure Communication Services from phone number is not configured. Please set it in Key Vault, environment variable ACS_FROM_PHONE_NUMBER, or configuration.");
    }

    public async Task<bool> ValidateConfigurationAsync()
    {
        try
        {
            await EnsureInitializedAsync();

            var connectionString = await GetConnectionStringAsync();
            var fromPhoneNumber = await GetFromPhoneNumberAsync();

            var isValid = !string.IsNullOrWhiteSpace(connectionString) && 
                         !string.IsNullOrWhiteSpace(fromPhoneNumber);

            if (isValid)
            {
                _logger.LogInformation("Azure Communication Services configuration validation successful");
            }
            else
            {
                _logger.LogError("Azure Communication Services configuration validation failed");
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during configuration validation");
            return false;
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _initSemaphore.WaitAsync();
        try
        {
            if (_initialized) return;

            if (_secretClient != null)
            {
                _keyVaultAvailable = await TestKeyVaultAccessAsync();
            }
            else
            {
                _keyVaultAvailable = false;
            }

            _initialized = true;
            _logger.LogDebug("Azure Configuration Service initialized. Key Vault available: {KeyVaultAvailable}", _keyVaultAvailable);
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    private async Task<bool> TestKeyVaultAccessAsync()
    {
        if (_secretClient == null) return false;

        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Try to list secrets with a timeout to test access
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            var secretsEnum = _secretClient.GetPropertiesOfSecretsAsync(cts.Token);
            await foreach (var _ in secretsEnum.WithCancellation(cts.Token))
            {
                // Just checking if we can access the vault
                break;
            }
            
            stopwatch.Stop();
            _logger.LogInformation("Key Vault access test successful in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Key Vault access test timed out after 10 seconds");
            return false;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 401)
        {
            _logger.LogError("Key Vault access denied (401 Unauthorized). Please check your Azure credentials and ensure the application has proper permissions to access Key Vault '{VaultUri}'. Error: {ErrorMessage}", 
                _keyVaultConfig.VaultUri, ex.Message);
            return false;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 403)
        {
            _logger.LogError("Key Vault access forbidden (403 Forbidden). The authenticated identity does not have sufficient permissions to access Key Vault '{VaultUri}'. Required permissions: 'Get' and 'List' for secrets. Error: {ErrorMessage}", 
                _keyVaultConfig.VaultUri, ex.Message);
            return false;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogError("Key Vault '{VaultUri}' not found (404 Not Found). Please verify the Key Vault URI is correct and the vault exists. Error: {ErrorMessage}", 
                _keyVaultConfig.VaultUri, ex.Message);
            return false;
        }
        catch (System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == System.Net.Sockets.SocketError.HostNotFound)
        {
            _logger.LogError("Cannot resolve Key Vault hostname '{VaultUri}'. Please verify the Key Vault URI is correct and your network connection is working. Error: {ErrorMessage}", 
                _keyVaultConfig.VaultUri, ex.Message);
            return false;
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            _logger.LogError("Network error connecting to Key Vault '{VaultUri}'. Please check your internet connection and firewall settings. Error: {ErrorMessage}", 
                _keyVaultConfig.VaultUri, ex.Message);
            return false;
        }
        catch (Azure.Identity.AuthenticationFailedException ex)
        {
            _logger.LogError("Azure authentication failed when accessing Key Vault '{VaultUri}'. Please ensure you are logged in with 'az login' or that managed identity/service principal credentials are properly configured. Error: {ErrorMessage}", 
                _keyVaultConfig.VaultUri, ex.Message);
            return false;
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogWarning("Key Vault access test failed with status {Status}: {ErrorMessage}", ex.Status, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Key Vault access test failed with unexpected error: {ErrorMessage}", ex.Message);
            return false;
        }
    }

    private async Task<string> GetSecretFromKeyVaultAsync(string secretName)
    {
        if (_secretClient == null || !_keyVaultAvailable)
        {
            return string.Empty;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await _secretClient.GetSecretAsync(secretName, null, cts.Token);
            
            stopwatch.Stop();
            _logger.LogDebug("Retrieved secret '{SecretName}' from Key Vault in {ElapsedMs}ms", secretName, stopwatch.ElapsedMilliseconds);
            
            return response.Value?.Value ?? string.Empty;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Secret '{SecretName}' not found in Key Vault '{VaultUri}'. Please ensure the secret exists and the name is correct.", 
                secretName, _keyVaultConfig.VaultUri);
            return string.Empty;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 401)
        {
            _logger.LogError("Access denied retrieving secret '{SecretName}' from Key Vault '{VaultUri}' (401 Unauthorized). Please check your Azure credentials.", 
                secretName, _keyVaultConfig.VaultUri);
            return string.Empty;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 403)
        {
            _logger.LogError("Insufficient permissions to retrieve secret '{SecretName}' from Key Vault '{VaultUri}' (403 Forbidden). Required permission: 'Get' for secrets.", 
                secretName, _keyVaultConfig.VaultUri);
            return string.Empty;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Timeout (30s) retrieving secret '{SecretName}' from Key Vault '{VaultUri}'. This may indicate network issues or Key Vault performance problems.", 
                secretName, _keyVaultConfig.VaultUri);
            return string.Empty;
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            _logger.LogWarning("Network error retrieving secret '{SecretName}' from Key Vault '{VaultUri}': {ErrorMessage}. Please check your network connection.", 
                secretName, _keyVaultConfig.VaultUri, ex.Message);
            return string.Empty;
        }
        catch (Azure.Identity.AuthenticationFailedException ex)
        {
            _logger.LogError("Azure authentication failed retrieving secret '{SecretName}' from Key Vault '{VaultUri}': {ErrorMessage}. Please ensure you are properly authenticated.", 
                secretName, _keyVaultConfig.VaultUri, ex.Message);
            return string.Empty;
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogWarning("Azure request failed retrieving secret '{SecretName}' from Key Vault '{VaultUri}' with status {Status}: {ErrorMessage}", 
                secretName, _keyVaultConfig.VaultUri, ex.Status, ex.Message);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving secret '{SecretName}' from Key Vault '{VaultUri}': {ErrorMessage}", 
                secretName, _keyVaultConfig.VaultUri, ex.Message);
            return string.Empty;
        }
    }

    public void Dispose()
    {
        _initSemaphore?.Dispose();
    }
}
