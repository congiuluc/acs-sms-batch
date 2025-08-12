# BatchSMS - Azure Communication Services Bulk SMS Sender

> **Version:** 2.0.0 | **Last Updated:** August 2025 | **Target Framework:** .NET 8.0

A high-performance .NET 8.0 console application for sending bulk SMS messages using Azure Communication Services with enterprise-level features including real-time progress tracking, comprehensive logging, dynamic CSV processing, and fault-tolerant architecture.

## 📋 Table of Contents

- [🔐 Azure Key Vault Configuration (Recommended for Production)](#-azure-key-vault-configuration-recommended-for-production)
- [🧪 Dry Run Mode - Test Without Sending SMS](#-dry-run-mode---test-without-sending-sms)
- [�🚀 Quick Start (5 Minutes)](#-quick-start-5-minutes)
- [⭐ Key Features](#-key-features)
- [📊 Azure Communication Services Rate Limits](#-azure-communication-services-rate-limits)
- [🛠️ Prerequisites](#️-prerequisites)
- [⚙️ Configuration](#️-configuration)
- [📄 CSV Sample Files with Message Template Examples](#-csv-sample-files-with-message-template-examples)
- [⬇️ Download & Run from GitHub Release](#️-download--run-from-github-release)
- [▶️ How to Execute Batch SMS](#️-how-to-execute-batch-sms)
- [📧 Message Templates and Personalization](#-message-templates-and-personalization)
- [🏃‍♂️ Quick Start Examples](#️-quick-start-examples)
- [📑 Reports and Output](#-reports-and-output)
- [🔬 Advanced Configuration](#-advanced-configuration)
- [🔧 Troubleshooting & Common Issues](#-troubleshooting--common-issues)
- [📁 Project Structure](#-project-structure)
- [🔍 Comprehensive Logging and Monitoring](#-comprehensive-logging-and-monitoring)
- [🆘 Support and Resources](#-support-and-resources)

## ⭐ Key Features

### Core Functionality
- ✅ **Enterprise-grade bulk SMS sending** from CSV files with flexible format support
- ✅ **Dry run mode** - test your CSV files and templates without sending SMS or incurring costs
- ✅ **Azure Key Vault integration** - secure credential storage for production environments
- ✅ **Dynamic CSV column detection** - automatically maps any CSV structure to phone numbers
- ✅ **Real-time CSV result writing** - results written immediately as SMS are sent
- ✅ **Italian phone number normalization** - automatically adds +39 prefix for Italian numbers
- ✅ **Multiple phone number handling** - uses first number when multiple are provided
- ✅ **Message templating** with dynamic variable substitution from CSV columns

### Performance & Reliability
- ✅ **Rate limiting with backoff** - intelligent throttling to respect Azure limits
- ✅ **Circuit breaker pattern** for fault tolerance and service protection
- ✅ **Retry logic** with exponential backoff for transient failures
- ✅ **Batch processing** with configurable batch sizes and parallel execution
- ✅ **Graceful shutdown** handling with Ctrl+C support and proper resource cleanup

### Monitoring & Reporting
- ✅ **Comprehensive structured logging** with Serilog (Debug/Info/Warning/Error levels)
- ✅ **Real-time progress tracking** with visual progress bars and ETA estimation
- ✅ **Throughput metrics** - items per minute calculation and milestone logging
- ✅ **Detailed CSV reports** - success/failure tracking with timestamps and error details
- ✅ **Summary reporting** - batch statistics and performance metrics
- ✅ **Failed recipient tracking** - separate reports for troubleshooting

### Architecture & Code Quality
- ✅ **Result pattern implementation** - functional error handling without exceptions
- ✅ **Dependency injection** with Microsoft.Extensions framework
- ✅ **SOLID principles** - clean, maintainable, and testable code architecture
- ✅ **Comprehensive XML documentation** - fully documented APIs and models
- ✅ **Single-file executable** - portable deployment with embedded dependencies
- ✅ **Configuration-driven design** - flexible settings without code changes

## 🔐 Azure Key Vault Configuration (Recommended for Production)

**IMPORTANT**: For production environments, use Azure Key Vault to securely store your Azure Communication Services credentials instead of storing them in configuration files.

### Quick Azure Key Vault Setup

1. **Create Key Vault and store secrets:**
```bash
# Create Key Vault
az keyvault create --name MyKeyVaultName --resource-group MyResourceGroup --location eastus

# Store secrets
az keyvault secret set --vault-name MyKeyVaultName --name "acs-connection-string" --value "endpoint=https://your-acs-resource.communication.azure.com/;accesskey=your-access-key"
az keyvault secret set --vault-name MyKeyVaultName --name "acs-from-phone-number" --value "+1234567890"

# Grant access
az role assignment create --role "Key Vault Secrets User" --assignee <your-user-id> --scope /subscriptions/<subscription-id>/resourceGroups/MyResourceGroup/providers/Microsoft.KeyVault/vaults/MyKeyVaultName
```

2. **Enable Key Vault in appsettings.json:**
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

3. **Authenticate with Azure:**
```bash
az login
```

📋 **For complete Azure Key Vault setup instructions, see:** [AZURE_KEYVAULT_SETUP.md](AZURE_KEYVAULT_SETUP.md)

## 🧪 Dry Run Mode - Test Without Sending SMS

**NEW FEATURE**: Test your CSV files and message templates without actually sending SMS messages or incurring costs.

### Enable Dry Run Mode

**Option 1: Configuration File (Persistent)**

In your `appsettings.json`, set:
```json
{
  "SmsConfiguration": {
    "DryRun": true,
    "MessageTemplate": "Hello {DisplayName}, your order #{OrderID} for {ProductName} is ready!"
  }
}
```

**Option 2: Command Line Flag (One-time)**

Use the `--dry-run` command line flag to enable dry run mode for a single execution:

```bash
# Standard build - one-time dry run
dotnet run -- --dry-run --csv your-file.csv

# Single-file executable - one-time dry run
BatchSMS.exe --dry-run --csv your-file.csv

# Combine with other options
BatchSMS.exe --dry-run --csv recipients.csv --output reports-folder
```

**Benefits of Command Line Flag:**
- ✅ **No configuration changes** - keeps your settings for production
- ✅ **Quick testing** - test any CSV file instantly
- ✅ **Overrides configuration** - works even if `DryRun: false` in appsettings.json
- ✅ **Perfect for validation** - test before switching to production mode

### What Dry Run Does

✅ **Validates** all your CSV data and phone numbers  
✅ **Tests** message template substitution with real data  
✅ **Simulates** SMS delivery timing and success/failure scenarios  
✅ **Generates** complete reports showing what would happen  
✅ **No SMS sent** - no Azure Communication Services charges  
✅ **No Azure credentials required** for testing  

### Dry Run Example Output

```
info: DRY RUN: SMS would be sent successfully to +393200000001 in 245ms. MessageId: dry-run-a1b2c3d4
info: DRY RUN: SMS would be sent successfully to +393200000002 in 189ms. MessageId: dry-run-e5f6g7h8
info: === BATCH SMS SUMMARY ===
info: DRY RUN MODE: No actual SMS messages were sent
info: Total Recipients: 50
info: Successful Sends: 47 (94.00%)
info: Failed Sends: 3 (6.00%)
```

### Dry Run Best Practices

1. **Always test first**: Run in dry mode before live sending
2. **Validate large files**: Test with your complete CSV before production
3. **Template testing**: Verify message templates work with all your data
4. **No credentials needed**: Test without valid Azure Communication Services setup


## 🚀 Quick Start (5 Minutes)

### 1. **Download & Configure**
```bash
# Clone or download the project
cd BatchSMS

# For testing: Enable dry run mode in appsettings.json
{
  "SmsConfiguration": {
    "DryRun": true,
    "MessageTemplate": "Hello {DisplayName}, welcome to our service!"
  }
}

# For production: Configure Azure credentials (see Azure Key Vault section above)
{
  "AzureCommunicationServices": {
    "ConnectionString": "endpoint=https://your-acs-resource.communication.azure.com/;accesskey=your-key",
    "FromPhoneNumber": "+1234567890"
  }
}
```

### 2. **Prepare Your CSV**
Create a simple CSV file with phone numbers and names:
```csv
PhoneNumber,DisplayName
+393200000001,Mario Rossi
+393200000002,Anna Bianchi
```

### 3. **Test with Dry Run (No SMS sent)**
```bash
# Validate your CSV file
dotnet run validate your-file.csv

# Option A: Quick test with command line flag (no config changes needed)
dotnet run -- --dry-run --csv your-file.csv

# Option B: Test with configuration file setting
# (First set "DryRun": true in appsettings.json)
dotnet run -- --csv your-file.csv
```

### 4. **Send Real SMS (Production)**
```bash
# Option A: Production run (ensures dry run is disabled)
dotnet run -- --csv your-file.csv

# Option B: If using configuration file, set "DryRun": false in appsettings.json
# Configure real Azure Communication Services credentials
# Send SMS messages
dotnet run -- --csv your-file.csv
```

**That's it!** 🎉 Your SMS messages will be sent and reports generated in the `Reports/` folder.

---

## 📊 Azure Communication Services Rate Limits

This application intelligently respects the following ACS SMS rate limits:

| Number Type | Scope | Time Frame | Limit | Application Handling |
|-------------|-------|------------|--------|---------------------|
| Toll-free | Per number | 60 seconds | 200 requests | Adaptive rate limiting |
| Short code | Per number | 60 seconds | 6,000 requests | Burst handling |
| Alphanumeric | Per resource | 60 seconds | 600 requests | Circuit breaker protection |

## 🛠️ Prerequisites

> **📋 Quick Requirements Checklist:**
> - ✅ .NET 8.0 SDK or Runtime installed
> - ✅ Azure Communication Services resource created
> - ✅ SMS-enabled phone number provisioned in ACS
> - ✅ CSV file with phone numbers and recipient names
> - ✅ Valid Azure connection string and from phone number

**Detailed Requirements:**
- .NET 8.0 SDK or Runtime
- Azure Communication Services resource with SMS capability
- Phone number provisioned in ACS (toll-free, short code, or alphanumeric sender ID)
- Windows/Linux/macOS (cross-platform support)

## ⚙️ Configuration

### 1. Update `appsettings.json`

```json
{
  "AzureCommunicationServices": {
    "ConnectionString": "endpoint=https://your-acs-resource.communication.azure.com/;accesskey=your-access-key",
    "FromPhoneNumber": "+12345678901"
  },
  "SmsConfiguration": {
    "EnableDeliveryReports": true,
    "MessageTemplate": "Hello {DisplayName}, this is a personalized message from our service. Thank you for joining us!",
    "DryRun": false
  },
  "RateLimiting": {
    "MaxConcurrentRequests": 10,
    "RequestsPerMinute": 200,
    "DelayBetweenBatchesMs": 1000,
    "BatchSize": 50,
    "RetryAttempts": 3,
    "RetryDelayMs": 5000,
    "CircuitBreakerFailureThreshold": 5,
    "CircuitBreakerTimeoutSeconds": 30
  },
  "CsvConfiguration": {
    "FilePath": "sample.csv",
    "HasHeaderRecord": true,
    "PhoneNumberColumn": "AuthMethodMobileNumbers",
    "DisplayNameColumn": "DisplayName",
    "SkipEmptyPhoneNumbers": true
  }
}
```

### 2. Configuration Options Explained

#### Azure Communication Services
- `ConnectionString`: Your ACS resource connection string
- `FromPhoneNumber`: The phone number to send SMS from

#### SMS Configuration
- `EnableDeliveryReports`: Enable delivery status reports
- `MessageTemplate`: SMS message template with placeholders
- `DryRun`: Enable dry run mode (test without sending SMS, default: false)

#### Rate Limiting
- `MaxConcurrentRequests`: Maximum concurrent SMS requests (default: 10)
- `RequestsPerMinute`: Maximum requests per minute (default: 200 for toll-free)
- `DelayBetweenBatchesMs`: Delay between processing batches
- `BatchSize`: Number of recipients per batch
- `RetryAttempts`: Number of retry attempts for failed requests
- `CircuitBreakerFailureThreshold`: Failures before opening circuit breaker

#### CSV Configuration
- `FilePath`: Default CSV file path
- `PhoneNumberColumn`: Column name containing phone numbers
- `DisplayNameColumn`: Column name containing display names
- `SkipEmptyPhoneNumbers`: Skip recipients with empty phone numbers

## ⬇️ **Download & Run from GitHub Release**

You can run BatchSMS without building from source by downloading the latest release from the [GitHub Releases page](https://github.com/congiuluc/acs-sms-batch/releases):

### 1. Download the Latest Release

1. Go to the [Releases page](https://github.com/congiuluc/acs-sms-batch/releases) of this repository.
2. Download the `BatchSMS.exe` (Windows) or the appropriate single-file executable for your OS from the latest release.
3. Download the sample `appsettings.json` and place it in the same folder as the executable.
4. (Optional) Download a sample CSV file or use your own.

### 2. Configure

Edit `appsettings.json` with your Azure Communication Services credentials and settings as described in the [Configuration](#️-configuration) section.

### 3. Run the Application

Open a terminal in the folder where you downloaded `BatchSMS.exe` and run:

```powershell
# Basic execution
./BatchSMS.exe

# With dry run mode (no SMS sent, no costs)
./BatchSMS.exe --dry-run

# With a custom CSV file
./BatchSMS.exe --csv "your-recipients.csv"

# With dry run and custom CSV file
./BatchSMS.exe --dry-run --csv "your-recipients.csv"

# With a custom output directory
./BatchSMS.exe --output "reports-folder"

# With dry run, custom CSV, and output directory
./BatchSMS.exe --dry-run --csv "your-recipients.csv" --output "reports-folder"

# Validate a CSV file before sending
./BatchSMS.exe validate your-recipients.csv
```

For help and all available options:

```powershell
./BatchSMS.exe --help
```

**No .NET installation is required** – the single-file executable includes all dependencies.

---

## 📄 **CSV Sample Files with Message Template Examples**

### 🎯 **Sample CSV for Current MessageTemplate**

Based on your current `MessageTemplate` in `appsettings.json`:
```
"Hello {DisplayName}, your order #{OrderID} for {ProductName} is ready!"
```

#### **Complete Sample CSV (sample.csv):**
```csv
PhoneNumber,DisplayName,OrderID,ProductName,Email,Department
+393200000001,Mario Rossi,ORD-12345,Premium Widget,mario.rossi@company.com,Sales
+393200000002,Anna Bianchi,ORD-12346,Deluxe Kit,anna.bianchi@company.com,Marketing
+393200000003,Giovanni Verdi,ORD-12347,Standard Package,giovanni.verdi@company.com,IT
+393200000004,Maria Neri,ORD-12348,Pro Bundle,maria.neri@company.com,HR
+393200000005,Luca Ferrari,ORD-12349,Enterprise Suite,luca.ferrari@company.com,Operations
```

#### **Resulting SMS Messages:**
- "Hello Mario Rossi, your order #ORD-12345 for Premium Widget is ready!"
- "Hello Anna Bianchi, your order #ORD-12346 for Deluxe Kit is ready!"
- "Hello Giovanni Verdi, your order #ORD-12347 for Standard Package is ready!"
- "Hello Maria Neri, your order #ORD-12348 for Pro Bundle is ready!"
- "Hello Luca Ferrari, your order #ORD-12349 for Enterprise Suite is ready!"

### 🔧 **Alternative CSV Formats That Work**

#### **Minimal CSV (only required fields):**
```csv
Mobile,Name,OrderID,ProductName
+393200000001,Mario Rossi,12345,Widget
+393200000002,Anna Bianchi,12346,Kit
```

#### **Azure Export Format:**
```csv
DisplayName,AuthMethodMobileNumbers,OrderID,ProductName,UserPrincipalName
Mario Rossi,+39 3200000001,ORD-12345,Premium Widget,mario.rossi@company.com
Anna Bianchi,+39 3200000002,ORD-12346,Deluxe Kit,anna.bianchi@company.com
```

#### **E-commerce Platform Export:**
```csv
CustomerName,Phone,OrderNumber,ProductName,OrderDate,CustomerEmail
Mario Rossi,+393200000001,ORD-12345,Premium Widget,2024-01-15,mario.rossi@email.com
Anna Bianchi,+393200000002,ORD-12346,Deluxe Kit,2024-01-15,anna.bianchi@email.com
```

### 📝 **Creating Your Own CSV**

#### **Required Columns for Current Template:**
1. **Phone Number** (any column name: PhoneNumber, Mobile, Phone, etc.)
2. **DisplayName** (any column name: DisplayName, Name, CustomerName, etc.)
3. **OrderID** (exact name: OrderID)
4. **ProductName** (exact name: ProductName)

#### **Quick Test CSV Creation:**
```powershell
# PowerShell: Create a test CSV file
@"
PhoneNumber,DisplayName,OrderID,ProductName
+393200000001,Test User 1,TEST-001,Sample Product
+393200000002,Test User 2,TEST-002,Demo Item
"@ | Out-File -FilePath "test-orders.csv" -Encoding UTF8
```

### 🎨 **Template Customization Examples**

#### **Business Notification Template:**
```json
"MessageTemplate": "Dear {DisplayName}, your order {OrderID} for {ProductName} from {Department} is confirmed. Contact {Email} for updates."
```

**Required CSV columns:** DisplayName, OrderID, ProductName, Department, Email

#### **Simple Order Update Template:**
```json
"MessageTemplate": "Hi {DisplayName}! Order #{OrderID} is ready for pickup. Product: {ProductName}"
```

**Required CSV columns:** DisplayName, OrderID, ProductName

#### **Customer Service Template:**
```json
"MessageTemplate": "Hello {DisplayName}, your {ProductName} order #{OrderID} has been shipped. Track at company.com/track"
```

**Required CSV columns:** DisplayName, ProductName, OrderID

## 📋 CSV File Format & Flexible Column Detection

> ⚠️ **IMPORTANT**: The minimum required values in your CSV file are:
> - **PhoneNumber**: Must contain valid phone numbers (preferably in international format like +393200000001)
> - **DisplayName**: Used for message personalization (if not found, the application will generate "User 1", "User 2", etc.)
> - **Custom Fields**: Any additional columns needed for your MessageTemplate (e.g., OrderID, ProductName)

The application intelligently detects CSV columns and supports multiple formats:

### 🔍 **Auto-Detection Features**
- **Phone Number Column**: Automatically detects columns containing "mobile", "phone", "cell", "telephone", "AuthMethodMobileNumbers", etc.
- **Display Name Column**: Automatically detects columns containing "name", "displayname", "user", etc.
- **First Column Default**: If no phone column is detected, uses the first column
- **Case Insensitive**: Works regardless of column name capitalization

### 📋 **Supported CSV Formats**

#### Format 1: Simple CSV (Phone numbers in first column)
```csv
MobileNumber,Name,Email
+393200000001,John Doe,john@example.com
+393200000002,Jane Smith,jane@example.com
```

#### Format 2: Azure AD Export Format
```csv
DisplayName,UserPrincipalName,Email,AuthMethodMobileNumbers
User001,user001@example.com,user001@example.com,+39 3200000001
User002,user002@example.com,user002@example.com,+39 3200000002
```

#### Format 3: Custom Business Format
```csv
EmployeeName,Department,Phone,Company
Alice Brown,IT,+393200000003,TechCorp
Bob Wilson,HR,+393200000004,TechCorp
```

### 📂 **CSV Configuration Options**

#### Flexible Column Mapping (Recommended)
```json
"CsvConfiguration": {
  "FilePath": "sample.csv",
  "HasHeaderRecord": true,
  "PhoneNumberColumn": null,        // Auto-detect phone column
  "DisplayNameColumn": null,        // Auto-detect name column
  "SkipEmptyPhoneNumbers": true
}
```

#### Explicit Column Mapping (For specific requirements)
```json
"CsvConfiguration": {
  "FilePath": "sample.csv", 
  "HasHeaderRecord": true,
  "PhoneNumberColumn": "PhoneNumber",
  "DisplayNameColumn": "DisplayName",
  "SkipEmptyPhoneNumbers": true
}
```

## ▶️ **How to Execute Batch SMS**

### Step 1: Build the Application

#### Option A: Standard Build
```bash
cd BatchSMS
dotnet build
```

#### Option B: Single-File Executable (Recommended for Distribution)

**Windows:**
```batch
# Run the build script
build-single-file.bat

# Or manually
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish-final
```

**Linux/macOS:**
```bash
# Run the build script
./build-single-file.sh

# Or manually for Linux
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish-final

# Or manually for macOS
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -o publish-final
```

**Single-File Benefits:**
- ✅ **Self-contained**: No .NET runtime installation required
- ✅ **Portable**: Copy `BatchSMS.exe` to any Windows machine and run
- ✅ **Simple deployment**: Single executable file with all dependencies
- ✅ **Faster startup**: ReadyToRun optimization included

### Step 2: Validate Your CSV File (Recommended)
Before sending SMS messages, validate your CSV file:

> ⚠️ **CSV Requirements**: Your CSV file must contain at minimum:
> - **Phone numbers** (in any column - will be auto-detected or use first column)
> - **Display names** (optional - if not found, generates "User 1", "User 2", etc.)
> - Valid phone number formats (preferably international format like +393200000001)

```bash
# Validate any CSV file format
dotnet run validate your-file.csv

# Examples
dotnet run validate sample.csv
dotnet run validate recipients.csv
dotnet run validate azure-export.csv
```

**Sample Validation Output:**
```
Validating CSV file: simple-test.csv
==================================================
Total Records: 5
Valid Records: 5
Invalid Records: 0
Success Rate: 100.0%

CONFIGURATION:
  Phone Number Column: MobileNumber
  Display Name Column: Name

✅ CSV file is valid and ready for SMS batch processing!

RECOMMENDATIONS:
  • 5 records are ready for SMS sending
```

### Step 2A: Test with Dry Run Mode (Optional but Recommended)
Before sending real SMS messages, test your setup with dry run mode:

**Option A: Command Line Flag (Recommended for quick testing)**
```bash
# Test your complete workflow without sending SMS - no config changes needed
dotnet run -- --dry-run --csv your-file.csv

# Using single-file executable
BatchSMS.exe --dry-run --csv your-file.csv
```

**Option B: Configuration File Setting**
```bash
# Enable dry run in appsettings.json
{
  "SmsConfiguration": {
    "DryRun": true,
    "MessageTemplate": "Hello {DisplayName}, your order #{OrderID} for {ProductName} is ready!"
  }
}

# Test your complete workflow without sending SMS
dotnet run -- --csv your-file.csv
```

**Dry Run Benefits:**
- ✅ No SMS sent, no Azure charges
- ✅ Validates all phone numbers and data
- ✅ Tests message template substitution
- ✅ Generates complete reports
- ✅ No Azure credentials required

### Step 3: Configure Azure Communication Services
Update `appsettings.json` with your ACS credentials:

```json
{
  "AzureCommunicationServices": {
    "ConnectionString": "endpoint=https://your-acs-resource.communication.azure.com/;accesskey=your-access-key",
    "FromPhoneNumber": "+12345678901"
  }
}
```

#### 🔐 **Alternative: Environment Variables or Azure Key Vault**
You can configure credentials using:

1. **Azure Key Vault (Recommended for Production)**
   - Store secrets securely in Azure Key Vault
   - Uses current user credentials for authentication
   - Automatic fallback to environment variables if Key Vault unavailable
   - See [Azure Key Vault Setup Guide](AZURE_KEYVAULT_SETUP.md) for detailed instructions

2. **Environment Variables (Good for Development/Testing)**

**Windows (PowerShell):**
```powershell
$env:ACS_CONNECTION_STRING = "endpoint=https://your-acs-resource.communication.azure.com/;accesskey=your-access-key"
$env:ACS_FROM_PHONE_NUMBER = "+12345678901"
```

**Linux/macOS (Bash):**
```bash
export ACS_CONNECTION_STRING="endpoint=https://your-acs-resource.communication.azure.com/;accesskey=your-access-key"
export ACS_FROM_PHONE_NUMBER="+12345678901"
```

**Supported Environment Variables:**
- **Connection String**: `ACS_CONNECTION_STRING`, `AZURE_COMMUNICATION_SERVICES_CONNECTION_STRING`, or `ConnectionStrings__AzureCommunicationServices`
- **From Phone Number**: `ACS_FROM_PHONE_NUMBER`, `AZURE_COMMUNICATION_SERVICES_FROM_PHONE_NUMBER`, or `FROM_PHONE_NUMBER`

> 💡 **Configuration Priority**: 
> 1. Azure Key Vault (if enabled)
> 2. Environment Variables
> 3. Configuration in `appsettings.json`

### Step 4: Execute Batch SMS

#### 🎯 **Using Standard Build**
```bash
# Basic execution (uses default configuration)
dotnet run

# Execute with dry run mode (no SMS sent, no costs)
dotnet run -- --dry-run

# Execute with custom CSV file
dotnet run -- --csv "path/to/your/recipients.csv"

# Execute with dry run and custom CSV file
dotnet run -- --dry-run --csv "path/to/your/recipients.csv"

# Execute with custom output directory
dotnet run -- --output "custom-reports"

# Execute with both custom CSV and output
dotnet run -- --csv "recipients.csv" --output "reports"

# Execute with dry run, custom CSV, and output
dotnet run -- --dry-run --csv "recipients.csv" --output "reports"
```

#### 🎯 **Using Single-File Executable**
```bash
# Navigate to the publish directory
cd publish-final

# Basic execution
BatchSMS.exe

# Execute with dry run mode (no SMS sent, no costs)
BatchSMS.exe --dry-run

# Execute with custom CSV file
BatchSMS.exe --csv "path/to/your/recipients.csv"

# Execute with dry run and custom CSV file
BatchSMS.exe --dry-run --csv "path/to/your/recipients.csv"

# Execute with custom output directory  
BatchSMS.exe --output "custom-reports"

# Execute with both custom CSV and output
BatchSMS.exe --csv "recipients.csv" --output "reports"

# Execute with dry run, custom CSV, and output
BatchSMS.exe --dry-run --csv "recipients.csv" --output "reports"
```

#### 🎯 **Validation Commands**
```bash
# Standard build
dotnet run validate your-file.csv

# Single-file executable
BatchSMS.exe validate your-file.csv
```

#### 🎯 **Get Help and Available Options**
```bash
# Standard build
dotnet run --help

# Single-file executable
BatchSMS.exe --help
```

### Step 5: Monitor Execution
The application provides real-time feedback:

```
info: Starting BatchSMS application
info: Reading recipients from CSV file: simple-test.csv
info: CSV headers found: MobileNumber, Name, Email
info: Auto-detected phone number column: 'MobileNumber'
info: Auto-detected display name column: 'Name'
info: Successfully read 5 recipients from CSV file
info: Found 5 recipients to process
info: Starting batch SMS send for 5 recipients
info: Processing 1 batches with batch size 50
info: Completed batch 1/1. Success: 5, Failed: 0
info: === BATCH SMS SUMMARY ===
info: Total Recipients: 5
info: Successful Sends: 5
info: Failed Sends: 0
info: Success Rate: 100.00%
```

## 📧 **Message Templates and Personalization**

The application supports dynamic message templating with CSV data:

### Template Variables
Use any CSV column as a template variable:

```json
{
  "SmsConfiguration": {
    "MessageTemplate": "Hello {DisplayName}, your account {UserId} in {Company} department {Department} is ready. Contact us at {UserPrincipalName} if you need assistance."
  }
}
```

### Supported Placeholders
- `{DisplayName}`: Recipient's display name
- `{Any CSV Column}`: Any column from your CSV file

### Template Examples

#### Simple Welcome Message
```
"Hello {DisplayName}, welcome to our service!"
```

#### Business Notification
```
"Hi {DisplayName}, your account for {Company} is ready. Login at https://portal.company.com with {UserPrincipalName}"
```

#### Department-specific Message
```
"Hello {DisplayName} from {Department}, your {JobTitle} access has been activated."
```

## 🏃‍♂️ **Quick Start Examples**

### Example 1: Simple Contact List
**CSV File (contacts.csv):**
```csv
Phone,Name
+393200000001,Mario Rossi
+393200000002,Anna Bianchi
```

**Execute:**
```bash
dotnet run validate contacts.csv
dotnet run -- --csv contacts.csv
```

### Example 2: Azure AD Export
**CSV File (azure-users.csv):**
```csv
DisplayName,UserPrincipalName,AuthMethodMobileNumbers,Department
Giovanni Verdi,g.verdi@company.com,+393200000003,IT
Maria Neri,m.neri@company.com,+393200000004,HR
```

**Execute:**
```bash
dotnet run validate azure-users.csv
dotnet run -- --csv azure-users.csv --output "azure-reports"
```

### Example 3: Employee Directory
**CSV File (employees.csv):**
```csv
EmployeeName,Mobile,Department,Company
Luca Ferrari,+393200000005,Sales,TechCorp
Sofia Romano,+393200000006,Marketing,TechCorp
```

**Execute:**
```bash
dotnet run validate employees.csv
dotnet run -- --csv employees.csv
```

## 📑 **Reports and Output**

The application generates comprehensive reports in the output directory:

### 1. Summary Report (`sms_report_YYYYMMDD_HHMMSS.csv`)
Contains complete batch operation details:
- Individual message status for each recipient
- Phone numbers and display names
- Success/failure status with error messages
- Timestamps and delivery information
- Success rates and timing metrics

### 2. Failed Recipients Report (`failed_recipients_YYYYMMDD_HHMMSS.csv`)
Contains only failed deliveries for easy retry:
- Recipients where SMS delivery failed
- Detailed error messages for troubleshooting
- Original phone numbers for investigation
- Perfect for retry operations

**Sample Report Location:**
```
Reports/
├── sms_report_20250811_143022.csv
└── failed_recipients_20250811_143022.csv
```

## 🔬 **Advanced Configuration**

### Rate Limiting Configuration
Adjust based on your Azure Communication Services phone number type:

#### For Toll-free Numbers (Default)
```json
"RateLimiting": {
  "RequestsPerMinute": 200,
  "BatchSize": 50,
  "MaxConcurrentRequests": 10
}
```

#### For Short Codes
```json
"RateLimiting": {
  "RequestsPerMinute": 6000,
  "BatchSize": 100,
  "MaxConcurrentRequests": 20
}
```

#### For Alphanumeric Sender IDs
```json
"RateLimiting": {
  "RequestsPerMinute": 600,
  "BatchSize": 60,
  "MaxConcurrentRequests": 15
}
```

## ⚠️ Error Handling

The application includes comprehensive error handling:

### Rate Limiting
- Automatic throttling based on ACS limits
- Circuit breaker pattern to prevent cascading failures
- Configurable retry logic with exponential backoff

### Common Error Scenarios
- **429 Too Many Requests**: Automatic retry with backoff
- **Invalid phone numbers**: Logged and skipped
- **Network timeouts**: Retried with exponential backoff
- **Service unavailable**: Circuit breaker activation

## 📝 Logging

The application provides detailed logging at multiple levels:

- **Information**: Progress updates and summaries
- **Debug**: Detailed request/response information
- **Warning**: Rate limiting and recoverable errors
- **Error**: Failed operations and exceptions

## 📦 **Deployment and Distribution**

### Single-File Executable Details
- **File Size**: ~83MB (includes all .NET runtime and dependencies)
- **Target Platform**: Windows x64
- **No Dependencies**: Works on any Windows machine without .NET installation
- **Portable**: Copy and run anywhere

### Deployment Options

#### Option 1: Copy Single Executable
```bash
# Copy the entire publish-final folder or just the essential files:
publish/
├── BatchSMS.exe          # Main executable (required)
├── appsettings.json      # Configuration file (required)
├── sample.csv           # Sample data (optional)
```

#### Option 2: Minimal Deployment
For production deployment, you only need:
```bash
production-deployment/
├── BatchSMS.exe
├── appsettings.json      # With your real ACS credentials
└── your-recipients.csv   # Your actual data
```

#### Option 3: Enterprise Distribution
```bash
# Create deployment package
mkdir "BatchSMS-Distribution"
copy "publish\BatchSMS.exe" "BatchSMS-Distribution\"
copy "production-appsettings.json" "BatchSMS-Distribution\appsettings.json"
copy "README.md" "BatchSMS-Distribution\"

# Distribute as ZIP file
```

### Configuration for Different Environments

#### Development (appsettings.json)
```json
{
  "AzureCommunicationServices": {
    "ConnectionString": "endpoint=https://dev-acs.communication.azure.com/;accesskey=dev-key",
    "FromPhoneNumber": "+1234567890"
  }
}
```

#### Production (appsettings.production.json)
```json
{
  "AzureCommunicationServices": {
    "ConnectionString": "endpoint=https://prod-acs.communication.azure.com/;accesskey=prod-key",
    "FromPhoneNumber": "+1987654321"
  }
}
```

### Running in Different Environments
```bash
# Use specific configuration file
BatchSMS.exe --configuration appsettings.production.json

# Or set environment variable
set ASPNETCORE_ENVIRONMENT=Production
BatchSMS.exe
```

## 📁 **Project Structure**

```
BatchSMS/
├── .vscode/                      # VS Code configuration files
├── samples/
│   └── sample.csv               # Sample CSV data for testing
├── src/                         # Main source code directory
│   ├── Configuration/
│   │   └── AppConfig.cs         # Configuration classes and models
│   ├── Models/
│   │   ├── Result.cs            # Result pattern for functional error handling
│   │   └── SmsModels.cs         # SMS-related data models and DTOs
│   ├── Services/
│   │   ├── CsvReaderService.cs  # CSV processing with intelligent auto-detection
│   │   ├── EnhancedSmsService.cs # Enhanced SMS service with advanced features
│   │   ├── SmsService.cs        # Core Azure Communication Services integration
│   │   ├── RateLimitingService.cs # Rate limiting and circuit breaker implementation
│   │   ├── RealTimeCsvWriter.cs # Thread-safe real-time CSV result writing
│   │   ├── IProgressReporter.cs # Progress reporting interfaces and implementations
│   │   └── ReportingService.cs  # Report generation and output management
│   ├── Tools/
│   │   └── CsvValidatorTool.cs  # CSV validation utility and command-line tool
│   ├── Utilities/
│   │   └── PhoneNumberValidator.cs # Phone number validation with Italian support
│   ├── Program.cs               # Main application entry point and DI configuration
│   ├── BatchSMS.csproj          # Project file with single-file publishing config
│   └── appsettings.json         # Application configuration file
├── .env.example                 # Environment variables template
├── .gitignore                   # Git ignore file
├── appsettings.Production.json.template # Production configuration template
├── BatchSMS.sln                 # Visual Studio solution file
├── build-single-file.bat        # Windows build script for single-file executable
├── build-single-file.sh         # Linux/macOS build script for single-file executable
├── secrets.json.example         # User secrets configuration example
├── setup.ps1                    # PowerShell setup script
└── README.md                    # This comprehensive documentation


```

### 🏗️ **Architecture Overview**

#### **Clean Architecture Principles**
- **📁 Separation of Concerns**: Each folder has a specific responsibility
- **🔄 Dependency Injection**: Services are loosely coupled through interfaces
- **📋 Result Pattern**: Functional error handling without exceptions
- **🛡️ SOLID Principles**: Maintainable and testable code structure

#### **Key Components**

| Component | Purpose | Key Features |
|-----------|---------|--------------|
| **Configuration/** | App settings and options | Strongly-typed configuration classes |
| **Models/** | Data structures | Result pattern, SMS models, validation |
| **Services/** | Business logic | SMS sending, CSV processing, reporting |
| **Tools/** | Utilities | CSV validation, command-line tools |
| **Utilities/** | Helper functions | Phone validation, formatters |

#### **Service Dependencies**
```
Program.cs
├── BatchSmsApplication (Main orchestrator)
    ├── ICsvReaderService (CSV processing)
    ├── ISmsService (SMS operations)
    ├── IReportingService (Report generation)
    ├── IRealTimeCsvWriter (Real-time results)
    └── IProgressReporter (Progress tracking)
```

### Secure Configuration Management
```bash
# For development - use user secrets
dotnet user-secrets init
dotnet user-secrets set "AzureCommunicationServices:ConnectionString" "your-connection-string"

# For production - use Azure Key Vault or environment variables
export ACS_CONNECTION_STRING="your-connection-string"
```

### Data Protection Guidelines
- 📱 **Phone numbers**: Store securely, comply with GDPR/local regulations
- 🔐 **Connection strings**: Never commit to source control
- 📋 **CSV files**: Handle according to company data policies
- 📊 **Reports**: Protect output files containing recipient data

### Compliance Features
- ✅ **Audit logging**: All operations are logged with timestamps
- ✅ **Error tracking**: Failed attempts are recorded for investigation
- ✅ **Data validation**: Phone number format validation
- ✅ **Rate limiting**: Respects Azure Communication Services limits

## 📈 **Performance and Scalability**

### Recommended Batch Sizes by Volume

#### Small Scale (< 1,000 recipients)
```json
"BatchSize": 50,
"MaxConcurrentRequests": 10
```

#### Medium Scale (1,000 - 10,000 recipients)
```json
"BatchSize": 100,
"MaxConcurrentRequests": 15
```

#### Large Scale (10,000+ recipients)
```json
"BatchSize": 200,
"MaxConcurrentRequests": 20
```

### Memory Usage Optimization
- Application uses streaming CSV reading for large files
- Memory usage remains constant regardless of file size
- Efficient batch processing prevents memory leaks

## 🔍 **Comprehensive Logging and Monitoring**

### Logging Architecture
The application implements enterprise-level structured logging using **Serilog** with multiple output targets and detailed operational tracking.

#### Log Levels and Usage
- **🔍 Debug**: Detailed execution flow, parameter values, internal state changes
- **ℹ️ Info**: High-level operations, configuration validation, batch progress
- **⚠️ Warning**: Non-critical issues, retries, fallback scenarios
- **❌ Error**: Failures, exceptions, configuration problems

#### Log Output Targets
1. **Console Logging**
   - Real-time progress updates
   - Color-coded log levels
   - Immediate feedback during execution
   
2. **File Logging**
   - Detailed structured logs in `logs/` directory
   - Automatic log rotation and retention
   - Persistent audit trail for troubleshooting

#### Sample Log Output
```
2024-01-15 08:44:07 [INF] Starting BatchSMS application at 2024-01-15 08:44:07 UTC
2024-01-15 08:44:07 [DBG] Command line arguments: --csv sample.csv --output Reports
2024-01-15 08:44:07 [DBG] Parsing command line arguments
2024-01-15 08:44:07 [INF] Input CSV file: D:\BatchSMS\sample.csv
2024-01-15 08:44:07 [INF] Output directory: D:\BatchSMS\Reports
2024-01-15 08:44:07 [DBG] Ensuring output directory exists
2024-01-15 08:44:07 [DBG] Output directory already exists: D:\BatchSMS\Reports
2024-01-15 08:44:07 [DBG] Starting configuration validation
2024-01-15 08:44:07 [DBG] Enhanced SMS service configuration validated successfully
2024-01-15 08:44:07 [INF] All configuration validation completed successfully
2024-01-15 08:44:07 [DBG] Starting CSV file reading process: sample.csv
2024-01-15 08:44:07 [DBG] Successfully read 5 recipients from CSV
2024-01-15 08:44:07 [INF] Successfully loaded 5 recipients from CSV
2024-01-15 08:44:07 [INF] Starting SMS batch processing for 5 recipients
2024-01-15 08:44:08 [DBG] Processing message template with 2 placeholders
2024-01-15 08:44:08 [DBG] Placeholder replaced: {DisplayName} -> John Doe
2024-01-15 08:44:08 [DBG] SMS send started for +393200000001 at 2024-01-15 08:44:08
2024-01-15 08:44:09 [INF] SMS sent successfully to +393200000001 in 1.2 seconds
2024-01-15 08:44:09 [DBG] Queuing result for real-time CSV writing
```


#### Performance Metrics Logged
- **Batch Processing**: Total duration, throughput (items/minute)
- **SMS Operations**: Individual send times, success/failure rates
- **CSV Operations**: Reading time, validation results, writing performance
- **Rate Limiting**: Throttling events, backoff durations
- **Memory Usage**: Efficient processing without memory leaks

#### Troubleshooting with Logs
1. **Check Console Output**: Real-time feedback and immediate error identification
2. **Review Log Files**: Detailed execution traces in `logs/` directory
3. **Filter by Level**: Use log levels to focus on specific issues
4. **Structured Data**: JSON-like structured logging for easy parsing
5. **Correlation IDs**: Track individual SMS operations through the pipeline

## 🆘 **Support and Resources**

### Azure Communication Services Resources
- 📚 [Azure Communication Services Documentation](https://docs.microsoft.com/azure/communication-services/)
- 📊 [SMS Rate Limits](https://docs.microsoft.com/azure/communication-services/concepts/service-limits)
- 🔧 [Troubleshooting Guide](https://docs.microsoft.com/azure/communication-services/concepts/troubleshooting-codes)
- 💰 [Pricing Calculator](https://azure.microsoft.com/pricing/calculator/)

### Application Features
- 🔍 **CSV Validation**: Always validate files before processing
- 📊 **Comprehensive Reports**: Track success rates and errors
- ⚡ **Auto-detection**: Works with any CSV format
- 🛡️ **Error Handling**: Robust retry and circuit breaker patterns
- 📝 **Detailed Logging**: Monitor progress and troubleshoot issues

### Getting Help
1. **Check logs** first for detailed error information
2. **Validate CSV** files using the built-in validator
3. **Test with small batches** before large deployments
4. **Monitor Azure portal** for service health and usage

## 🔧 **Troubleshooting & Common Issues**

### CSV File Requirements

> ⚠️ **CRITICAL**: Your CSV file MUST contain these minimum fields:
> 
> | Field | Requirement | Auto-Detection | Example |
> |-------|-------------|----------------|---------|
> | **Phone Number** | ✅ **REQUIRED** | Yes (mobile, phone, cell, etc.) | +393200000001 |
> | **Display Name** | ⚠️ **Recommended** | Yes (name, displayname, user, etc.) | John Doe |
> 
> - **Phone numbers** can be in any column - the app auto-detects common column names
> - **Display names** are used for message personalization (generates "User 1", "User 2" if missing)
> - **Phone format**: Preferably international format (+393200000001), but app normalizes Italian numbers

### CSV Validation Issues

#### Issue: "No phone number column found"
```bash
❌ No phone number column found or detected in CSV file
```
**Solution:** 
- Ensure your CSV has a column with phone numbers
- Use column names like "phone", "mobile", "phonenumber", etc.
- Or specify exact column name in `appsettings.json`

#### Issue: Invalid phone number format
```bash
❌ Row 2: '3200000001' (Mario Rossi) - Invalid phone number format
```
**Solution:** 
- Use international format: `+393200000001`
- The app auto-normalizes some formats but prefers `+` prefix

### Azure Communication Services Issues

#### Issue: Connection String Invalid
```bash
Error: The input is not a valid Base-64 string
```
**Solution:** 
- Verify your ACS connection string in `appsettings.json`
- Ensure no extra spaces or characters
- Check Azure portal for correct connection string
- **Alternative**: Use environment variables:
  ```bash
  export ACS_CONNECTION_STRING="endpoint=https://your-acs-resource.communication.azure.com/;accesskey=your-access-key"
  ```

#### Issue: Connection String Not Configured
```bash
Error: Azure Communication Services connection string is not configured
```
**Solution:** 
- Set in `appsettings.json` **OR** use environment variables:
  - `ACS_CONNECTION_STRING`
  - `AZURE_COMMUNICATION_SERVICES_CONNECTION_STRING`
  - `ConnectionStrings__AzureCommunicationServices`

#### Issue: From Phone Number Not Configured
```bash
Error: From phone number is not configured
```
**Solution:** 
- Set in `appsettings.json` **OR** use environment variables:
  - `ACS_FROM_PHONE_NUMBER`
  - `AZURE_COMMUNICATION_SERVICES_FROM_PHONE_NUMBER`
  - `FROM_PHONE_NUMBER`

#### Issue: Rate Limiting
```bash
Warning: Rate limit exceeded, waiting...
```
**Solution:** 
- This is normal behavior - the app handles it automatically
- Adjust `RequestsPerMinute` in configuration if needed

#### Issue: Phone Number Not Provisioned
```bash
Error: From phone number +1234567890 is not owned by the resource
```
**Solution:** 
- Verify the `FromPhoneNumber` is provisioned in your ACS resource
- Check Azure portal for available phone numbers

### File and Directory Issues

#### Issue: Reports directory not found
```bash
Error: Could not find a part of the path 'Reports\sms_report_...'
```
**Solution:** 
- The app creates directories automatically in current versions
- Ensure write permissions in the output directory
- Use `--output` parameter to specify custom directory

### Performance and Memory Issues

#### Issue: Large CSV files causing memory issues
**Solution:**
- Process files in smaller batches
- Reduce `BatchSize` in configuration
- Split large CSV files into smaller chunks

### Command Line Issues

#### Issue: CSV file not found
```bash
Error: CSV file not found: myfile.csv
```
**Solution:**
- Use absolute paths: `"C:\Data\myfile.csv"`
- Or relative paths from the application directory
- Verify file exists with `dir` or `ls` command

## 🎯 **Best Practices for Batch Execution**

### 1. Always Validate First
```bash
# Always run validation before actual SMS sending
dotnet run validate your-file.csv
```

### 2. Test with Small Batches
```bash
# Start with a small test file (5-10 numbers)
dotnet run -- --csv "test-batch.csv"
```

### 3. Monitor Rate Limits
- Start with default settings
- Adjust based on your phone number type
- Monitor Azure portal for usage metrics

### 4. Handle Large Files
```bash
# For files with 1000+ recipients
# Split into smaller files or adjust batch size
```

### 5. Backup and Logging
- Keep backup copies of CSV files
- Save output reports for compliance
- Monitor logs for any issues

## ⚖️ License

This project is licensed under the MIT License.

## 💬 Support

For Azure Communication Services specific issues, refer to:
- [Azure Communication Services Documentation](https://docs.microsoft.com/azure/communication-services/)
- [SMS Rate Limits](https://docs.microsoft.com/azure/communication-services/concepts/service-limits)
- [Troubleshooting Guide](https://docs.microsoft.com/azure/communication-services/concepts/troubleshooting-codes)

