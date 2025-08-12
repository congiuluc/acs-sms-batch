# Azure Key Vault Integration for Azure Communication Services

This document explains how to configure Azure Key Vault for securely storing Azure Communication Services credentials, with environment variable fallback options.

## Configuration Priority

The application follows this priority order for retrieving Azure Communication Services credentials:

1. **Azure Key Vault** (recommended for production)
2. **Environment Variables** (good for development/testing)
3. **Configuration Files** (fallback, not recommended for production)

## Azure Key Vault Setup

### Prerequisites

- Azure subscription with appropriate permissions
- Azure Key Vault instance
- Azure Communication Services resource

### Step 1: Create Azure Key Vault

```bash
# Create resource group if needed
az group create --name MyResourceGroup --location eastus

# Create Key Vault
az keyvault create \
  --name MyKeyVaultName \
  --resource-group MyResourceGroup \
  --location eastus \
  --enable-rbac-authorization true
```

### Step 2: Store Secrets in Key Vault

```bash
# Store ACS connection string
az keyvault secret set \
  --vault-name MyKeyVaultName \
  --name "acs-connection-string" \
  --value "endpoint=https://your-acs-resource.communication.azure.com/;accesskey=your-access-key"

# Store from phone number
az keyvault secret set \
  --vault-name MyKeyVaultName \
  --name "acs-from-phone-number" \
  --value "+1234567890"
```

### Step 3: Configure Access Permissions

#### Option A: Using Azure RBAC (Recommended)

```bash
# Get your user principal ID
az ad signed-in-user show --query id -o tsv

# Assign Key Vault Secrets User role
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee <your-user-principal-id> \
  --scope /subscriptions/<subscription-id>/resourceGroups/MyResourceGroup/providers/Microsoft.KeyVault/vaults/MyKeyVaultName
```

#### Option B: Using Access Policies (Legacy)

```bash
# Get your user principal name
az ad signed-in-user show --query userPrincipalName -o tsv

# Set access policy
az keyvault set-policy \
  --name MyKeyVaultName \
  --upn <your-user-principal-name> \
  --secret-permissions get list
```

### Step 4: Configure Application

Update `appsettings.json`:

```json
{
  "KeyVault": {
    "Enabled": true,
    "VaultUri": "https://MyKeyVaultName.vault.azure.net/",
    "ConnectionStringSecretName": "acs-connection-string",
    "FromPhoneNumberSecretName": "acs-from-phone-number"
  }
}
```

## Environment Variables Setup

### Windows (PowerShell)

```powershell
# Set environment variables for current session
$env:ACS_CONNECTION_STRING = "endpoint=https://your-acs-resource.communication.azure.com/;accesskey=your-access-key"
$env:ACS_FROM_PHONE_NUMBER = "+1234567890"

# Set environment variables permanently (requires restart)
[System.Environment]::SetEnvironmentVariable("ACS_CONNECTION_STRING", "endpoint=https://your-acs-resource.communication.azure.com/;accesskey=your-access-key", [System.EnvironmentVariableTarget]::User)
[System.Environment]::SetEnvironmentVariable("ACS_FROM_PHONE_NUMBER", "+1234567890", [System.EnvironmentVariableTarget]::User)
```

### Windows (Command Prompt)

```cmd
rem Set environment variables for current session
set ACS_CONNECTION_STRING=endpoint=https://your-acs-resource.communication.azure.com/;accesskey=your-access-key
set ACS_FROM_PHONE_NUMBER=+1234567890

rem Set environment variables permanently (requires restart)
setx ACS_CONNECTION_STRING "endpoint=https://your-acs-resource.communication.azure.com/;accesskey=your-access-key"
setx ACS_FROM_PHONE_NUMBER "+1234567890"
```

### Linux/macOS

```bash
# Set environment variables for current session
export ACS_CONNECTION_STRING="endpoint=https://your-acs-resource.communication.azure.com/;accesskey=your-access-key"
export ACS_FROM_PHONE_NUMBER="+1234567890"

# Add to ~/.bashrc or ~/.zshrc for persistence
echo 'export ACS_CONNECTION_STRING="endpoint=https://your-acs-resource.communication.azure.com/;accesskey=your-access-key"' >> ~/.bashrc
echo 'export ACS_FROM_PHONE_NUMBER="+1234567890"' >> ~/.bashrc
```

## Authentication Methods

### 1. Default Azure Credential (Recommended)

The application uses `DefaultAzureCredential` which tries multiple authentication methods in order:

1. Environment variables (if `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`, `AZURE_TENANT_ID` are set)
2. Managed Identity (in Azure environments)
3. Visual Studio authentication
4. Azure CLI authentication
5. Azure PowerShell authentication
6. Interactive browser authentication

### 2. Azure CLI Authentication

```bash
# Login to Azure CLI
az login

# Set specific subscription if needed
az account set --subscription <subscription-id>
```

### 3. Service Principal (for CI/CD)

```bash
# Create service principal
az ad sp create-for-rbac --name BatchSMSApp --role "Key Vault Secrets User" --scopes /subscriptions/<subscription-id>/resourceGroups/MyResourceGroup/providers/Microsoft.KeyVault/vaults/MyKeyVaultName

# Set environment variables (in CI/CD pipeline)
export AZURE_CLIENT_ID=<client-id>
export AZURE_CLIENT_SECRET=<client-secret>
export AZURE_TENANT_ID=<tenant-id>
```

## Configuration Examples

### Production Configuration (Key Vault Only)

```json
{
  "KeyVault": {
    "Enabled": true,
    "VaultUri": "https://prod-batchsms-kv.vault.azure.net/",
    "ConnectionStringSecretName": "acs-connection-string",
    "FromPhoneNumberSecretName": "acs-from-phone-number"
  },
  "AzureCommunicationServices": {
    "ConnectionString": "",
    "FromPhoneNumber": ""
  }
}
```

### Development Configuration (Environment Variables)

```json
{
  "KeyVault": {
    "Enabled": false,
    "VaultUri": "",
    "ConnectionStringSecretName": "acs-connection-string",
    "FromPhoneNumberSecretName": "acs-from-phone-number"
  },
  "AzureCommunicationServices": {
    "ConnectionString": "",
    "FromPhoneNumber": ""
  }
}
```

## Troubleshooting

### Common Issues

1. **Authentication Failed**
   - Ensure you're logged in: `az login`
   - Check permissions: Verify you have "Key Vault Secrets User" role
   - Verify Key Vault URI is correct

2. **Secret Not Found**
   - Check secret names match configuration
   - Verify secrets exist in Key Vault: `az keyvault secret list --vault-name MyKeyVaultName`

3. **Network Issues**
   - Ensure Key Vault allows public access or configure private endpoints
   - Check firewall rules

### Debugging

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

### Testing Configuration

The application validates configuration on startup and provides detailed error messages. Check the logs for specific issues.

## Security Best Practices

1. **Never store secrets in source code or configuration files**
2. **Use Azure Key Vault for production environments**
3. **Rotate secrets regularly**
4. **Use managed identities when possible**
5. **Limit access using RBAC or access policies**
6. **Monitor Key Vault access logs**
7. **Use separate Key Vaults for different environments**

## Azure DevOps Pipeline Example

```yaml
trigger:
- main

pool:
  vmImage: 'windows-latest'

variables:
- group: BatchSMS-KeyVault # Variable group linked to Key Vault

steps:
- task: DotNetCoreCLI@2
  displayName: 'Restore packages'
  inputs:
    command: 'restore'
    projects: '**/*.csproj'

- task: DotNetCoreCLI@2
  displayName: 'Build application'
  inputs:
    command: 'build'
    projects: '**/*.csproj'

- task: AzureKeyVault@2
  inputs:
    azureSubscription: 'BatchSMS-ServiceConnection'
    KeyVaultName: 'prod-batchsms-kv'
    SecretsFilter: 'acs-connection-string,acs-from-phone-number'

- task: DotNetCoreCLI@2
  displayName: 'Run application'
  inputs:
    command: 'run'
    projects: '**/*.csproj'
  env:
    ACS_CONNECTION_STRING: $(acs-connection-string)
    ACS_FROM_PHONE_NUMBER: $(acs-from-phone-number)
```
