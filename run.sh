#!/bin/bash

# HID Device Monitor - Quick Start Script

echo "Building HID Device Monitor..."
dotnet build

if [ $? -eq 0 ]; then
    echo ""
    echo "Starting HID Device Monitor..."
    echo "Note: On macOS, you may need to grant Input Monitoring permissions."
    echo ""
    dotnet run
else
    echo "Build failed. Please check the errors above."
    exit 1
fi
