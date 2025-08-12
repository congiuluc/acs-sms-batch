#!/bin/bash

echo "Building BatchSMS as a single-file executable..."
echo

# Clean previous builds
rm -rf publish-final
rm -rf publish

# Determine the runtime identifier based on the OS
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    RID="linux-x64"
elif [[ "$OSTYPE" == "darwin"* ]]; then
    RID="osx-x64"
else
    echo "Unsupported OS. This script supports Linux and macOS."
    exit 1
fi

echo "Building for $RID..."

# Build single-file executable
dotnet publish -c Release -r $RID --self-contained true -p:PublishSingleFile=true -o publish-final

if [ $? -eq 0 ]; then
    echo
    echo "✅ Build successful!"
    echo
    
    # Create Reports folder if it doesn't exist
    if [ ! -d "publish-final/Reports" ]; then
        mkdir -p "publish-final/Reports"
        echo "📁 Created Reports folder in publish-final directory"
    fi
    
    # Create logs folder if it doesn't exist
    if [ ! -d "publish-final/logs" ]; then
        mkdir -p "publish-final/logs"
        echo "📁 Created logs folder in publish-final directory"
    fi
    
    echo
    echo "Single-file executable created at: publish-final/BatchSMS"
    echo "File size:"
    ls -lh publish-final/BatchSMS
    echo
    echo "You can now copy BatchSMS to any $RID machine and run it without installing .NET"
    echo
    echo "Test the executable:"
    echo "  cd publish-final"
    echo "  ./BatchSMS --help"
    echo "  ./BatchSMS validate sample.csv"
    echo
else
    echo
    echo "❌ Build failed!"
    echo "Check the output above for errors."
    exit 1
fi
