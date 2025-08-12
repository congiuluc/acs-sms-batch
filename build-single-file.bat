@echo off
echo Building BatchSMS as a single-file executable...
echo.

REM Clean previous builds
if exist "publish" rmdir /s /q "publish"

REM Build single-file executable
dotnet publish src/BatchSMS.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ✅ Build successful!
    echo.
    
    REM Create Reports folder if it doesn't exist
    if not exist "publish\Reports" (
        mkdir "publish\Reports"
        echo 📁 Created Reports folder in publish directory
    )
    
    REM Create logs folder if it doesn't exist
    if not exist "publish\logs" (
        mkdir "publish\logs"
        echo 📁 Created logs folder in publish directory
    )
    
    echo.
    echo Single-file executable created at: publish\BatchSMS.exe
    echo File size: 
    dir "publish\BatchSMS.exe" | findstr "BatchSMS.exe"
    echo.
    echo You can now copy BatchSMS.exe to any Windows machine and run it without installing .NET
    echo.
    echo Test the executable:
    echo   cd publish
    echo   BatchSMS.exe --help
    echo   BatchSMS.exe validate sample.csv
    echo.
) else (
    echo.
    echo ❌ Build failed!
    echo Check the output above for errors.
)

pause
