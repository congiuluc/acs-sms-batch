# Testing Azure Key Vault Integration

This file provides instructions for testing the Azure Key Vault integration with Azure Communication Services.

## Test Scenarios

### 1. Test with Key Vault Configuration

1. Update `appsettings.json`:
```json
{
  "KeyVault": {
    "Enabled": true,
    "VaultUri": "https://your-keyvault-name.vault.azure.net/",
    "ConnectionStringSecretName": "acs-connection-string",
    "FromPhoneNumberSecretName": "acs-from-phone-number"
  }
}
```

2. Ensure you're authenticated to Azure:
```bash
az login
az account set --subscription <your-subscription-id>
```

3. Test configuration validation (this won't send SMS):
```bash
dotnet run validate sample.csv
```

### 2. Test with Environment Variables Fallback

1. Disable Key Vault in `appsettings.json`:
```json
{
  "KeyVault": {
    "Enabled": false,
    "VaultUri": ""
  }
}
```

2. Set environment variables:
```powershell
# Windows PowerShell
$env:ACS_CONNECTION_STRING = "endpoint=https://your-acs-resource.communication.azure.com/;accesskey=your-access-key"
$env:ACS_FROM_PHONE_NUMBER = "+1234567890"
```

3. Test configuration validation:
```bash
dotnet run validate sample.csv
```

### 3. Test Configuration Priority

The application tests the following priority order:

1. **Azure Key Vault** (if enabled and accessible)
2. **Environment Variables** (`ACS_CONNECTION_STRING`, `ACS_FROM_PHONE_NUMBER`)
3. **Configuration File** (`appsettings.json`)

### 4. Test Authentication Methods

The application supports multiple Azure authentication methods:

1. **Azure CLI**: `az login`
2. **Visual Studio**: Sign in to Visual Studio with Azure account
3. **Service Principal**: Set `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`, `AZURE_TENANT_ID` environment variables
4. **Managed Identity**: Works automatically in Azure environments

### 5. Debugging Configuration Issues

Enable debug logging in `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "BatchSMS.Services.AzureConfigurationService": "Debug"
    }
  }
}
```

Look for log messages like:
- "Azure Key Vault client initialized with URI: ..."
- "Key Vault access test successful in Xms"
- "Successfully retrieved connection string from Key Vault"
- "Successfully retrieved connection string from environment variable"
- "Using connection string from configuration"

### 6. Common Configuration Test Results

#### ✅ Key Vault Success
```
info: Azure Key Vault client initialized with URI: https://your-kv.vault.azure.net/
info: Key Vault access test successful in 245ms
info: Successfully retrieved connection string from Key Vault
info: Successfully retrieved from phone number from Key Vault
info: Azure Communication Services configuration validation successful
```

#### ⚠️ Key Vault Fallback to Environment Variables
```
warn: Key Vault access test failed: Unauthorized
info: Successfully retrieved connection string from environment variable ACS_CONNECTION_STRING
info: Successfully retrieved from phone number from environment variable ACS_FROM_PHONE_NUMBER
info: Azure Communication Services configuration validation successful
```

#### ⚠️ Fallback to Configuration File
```
warn: Key Vault is disabled or not configured. Using environment variables and configuration fallback.
info: Using connection string from configuration (consider moving to Key Vault for production)
info: Using from phone number from configuration (consider moving to Key Vault for production)
info: Azure Communication Services configuration validation successful
```

#### ❌ Configuration Error
```
error: No connection string found in Key Vault, environment variables, or configuration
error: Azure Communication Services configuration validation failed
```

## Security Testing

### Test Access Permissions

1. Test with insufficient permissions:
   - Remove "Key Vault Secrets User" role
   - Application should fall back to environment variables

2. Test with network issues:
   - Disable internet connection temporarily
   - Application should fall back gracefully

3. Test with invalid Key Vault URI:
   - Use incorrect Key Vault URI
   - Application should fall back to environment variables

### Test Secret Names

1. Test with non-existent secrets:
   - Use incorrect secret names in configuration
   - Application should fall back to environment variables

2. Test with empty secrets:
   - Create empty secrets in Key Vault
   - Application should fall back to environment variables

## Performance Testing

### Key Vault Response Times

Monitor log messages for Key Vault response times:
- Initial access test: Should complete in < 1 second
- Secret retrieval: Should complete in < 500ms
- Cached retrieval: Should complete immediately

### Caching Behavior

The application caches secrets for 5 minutes:
1. First access: Retrieves from Key Vault
2. Subsequent access within 5 minutes: Uses cached value
3. After 5 minutes: Retrieves from Key Vault again

Test by running the application multiple times within 5 minutes and checking for "Returning cached" log messages.
