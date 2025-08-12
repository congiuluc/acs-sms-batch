# BatchSMS Setup Script
# This script helps configure the BatchSMS application

param(
    [Parameter(Mandatory=$true)]
    [string]$ConnectionString,
    
    [Parameter(Mandatory=$true)]
    [string]$FromPhoneNumber,
    
    [string]$MessageTemplate = "Hello {DisplayName}, this is a message from your organization.",
    
    [int]$RequestsPerMinute = 200,
    
    [int]$BatchSize = 50,
    
    [string]$CsvFilePath = "sample.csv"
)

Write-Host "Setting up BatchSMS Application..." -ForegroundColor Green

# Validate parameters
if (-not $ConnectionString.StartsWith("endpoint=")) {
    Write-Error "Invalid connection string format. Should start with 'endpoint='"
    exit 1
}

if (-not $FromPhoneNumber.StartsWith("+")) {
    Write-Error "Phone number should be in international format starting with '+'"
    exit 1
}

# Create appsettings.json with provided values
$config = @{
    "AzureCommunicationServices" = @{
        "ConnectionString" = $ConnectionString
        "FromPhoneNumber" = $FromPhoneNumber
    }
    "SmsConfiguration" = @{
        "EnableDeliveryReports" = $true
        "MessageTemplate" = $MessageTemplate
    }
    "RateLimiting" = @{
        "MaxConcurrentRequests" = 10
        "RequestsPerMinute" = $RequestsPerMinute
        "DelayBetweenBatchesMs" = 1000
        "BatchSize" = $BatchSize
        "RetryAttempts" = 3
        "RetryDelayMs" = 5000
        "CircuitBreakerFailureThreshold" = 5
        "CircuitBreakerTimeoutSeconds" = 30
    }
    "CsvConfiguration" = @{
        "FilePath" = $CsvFilePath
        "HasHeaderRecord" = $true
        "PhoneNumberColumn" = "AuthMethodMobileNumbers"
        "DisplayNameColumn" = "DisplayName"
        "SkipEmptyPhoneNumbers" = $true
    }
    "Logging" = @{
        "LogLevel" = @{
            "Default" = "Information"
            "Microsoft" = "Warning"
            "Microsoft.Hosting.Lifetime" = "Information"
        }
    }
}

# Convert to JSON and save
$configJson = $config | ConvertTo-Json -Depth 5
$configJson | Out-File -FilePath "appsettings.json" -Encoding UTF8

Write-Host "Configuration saved to appsettings.json" -ForegroundColor Green

# Create Reports directory
if (-not (Test-Path "Reports")) {
    New-Item -ItemType Directory -Path "Reports"
    Write-Host "Created Reports directory" -ForegroundColor Green
}

# Verify CSV file exists
if (-not (Test-Path $CsvFilePath)) {
    Write-Warning "CSV file not found: $CsvFilePath"
    Write-Host "Make sure to create your CSV file with the following required columns:" -ForegroundColor Yellow
    Write-Host "- DisplayName" -ForegroundColor Yellow
    Write-Host "- AuthMethodMobileNumbers (phone numbers in international format)" -ForegroundColor Yellow
}

Write-Host "`nSetup completed successfully!" -ForegroundColor Green
Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "1. Ensure your CSV file exists and has the correct format" -ForegroundColor White
Write-Host "2. Build the application: dotnet build" -ForegroundColor White
Write-Host "3. Run the application: dotnet run" -ForegroundColor White
Write-Host "`nFor help, see README.md or run: dotnet run -- --help" -ForegroundColor White
