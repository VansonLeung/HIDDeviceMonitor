using System;
using System.Collections.Generic;
using System.Linq;
using HidSharp;
using HidSharp.Reports;

namespace HIDDeviceMonitor;

/// <summary>
/// Classifies HID devices into different controller types
/// </summary>
public static class DeviceClassifier
{
    /// <summary>
    /// Classifies a HID device based on its characteristics
    /// </summary>
    public static DeviceType ClassifyDevice(HidDevice device, DeviceCapabilities capabilities)
    {
        // Classify based on HID usage specification
        return ClassifyByHIDSpecification(capabilities);
    }

    /// <summary>
    /// Classifies device based on HID Usage Page and Usage ID from specification
    /// </summary>
    private static DeviceType ClassifyByHIDSpecification(DeviceCapabilities capabilities)
    {
        int usagePage = capabilities.UsagePage;
        int usageId = capabilities.UsageId;

        switch (usagePage)
        {
            case 0x01: // Generic Desktop Controls
                return ClassifyGenericDesktopUsage(usageId);
            case 0x02: // Simulation Controls
                return ClassifySimulationUsage(usageId);
            case 0x05: // Game Controls
                return ClassifyGameUsage(usageId);
            default:
                return DeviceType.Unknown;
        }
    }

    /// <summary>
    /// Classifies Generic Desktop Controls (Usage Page 0x01)
    /// </summary>
    private static DeviceType ClassifyGenericDesktopUsage(int usageId)
    {
        switch (usageId)
        {
            case 0x04: // Joystick
                return DeviceType.Joystick;
            case 0x05: // Game Pad
                return DeviceType.Gamepad;
            case 0x08: // Multi-axis Controller
                return DeviceType.Joystick; // Multi-axis controllers are typically joysticks
            default:
                return DeviceType.Unknown;
        }
    }

    /// <summary>
    /// Classifies Simulation Controls (Usage Page 0x02)
    /// </summary>
    private static DeviceType ClassifySimulationUsage(int usageId)
    {
        switch (usageId)
        {
            case 0x01: // Flight Simulation Device
                return DeviceType.Joystick;
            case 0x02: // Automobile Simulation Device
                return DeviceType.SteeringWheel;
            case 0x03: // Tank Simulation Device
                return DeviceType.Joystick;
            default:
                return DeviceType.Joystick; // Other simulation devices are typically joysticks
        }
    }

    /// <summary>
    /// Classifies Game Controls (Usage Page 0x05)
    /// </summary>
    private static DeviceType ClassifyGameUsage(int usageId)
    {
        return DeviceType.Gamepad; // Game controls are typically gamepads
    }

    /// <summary>
    /// Parses device capabilities from HID report descriptor without opening the device
    /// </summary>
    public static DeviceCapabilities ParseDeviceCapabilities(HidDevice device)
    {
        try
        {
            var reportDescriptor = device.GetReportDescriptor();
            var deviceItems = reportDescriptor.DeviceItems.FirstOrDefault();

            int usagePage = 0;
            int usageId = 0;

            if (deviceItems != null && deviceItems.Usages.GetAllValues().Any())
            {
                var usage = deviceItems.Usages.GetAllValues().First();
                usagePage = (int)(usage >> 16);
                usageId = (int)(usage & 0xFFFF);
            }

            var axes = new List<AxisCapability>();
            var buttons = new List<ButtonCapability>();

            // Parse axes and buttons from report descriptor  
            var inputReport = deviceItems?.InputReports.FirstOrDefault();
            if (inputReport != null)
            {
                // Check if report uses Report ID (if so, descriptor offsets need +1 byte adjustment)
                bool hasReportId = inputReport.ReportID != 0;
                int reportIdOffset = hasReportId ? 8 : 0; // Add 8 bits (1 byte) if Report ID exists
                
                int currentBitOffset = 0;
                int axisIndex = 0;
                int buttonIndex = 0;
                
                foreach (var element in inputReport.DataItems)
                {
                    // Check if this is likely a button (1-bit values) or axis (multi-bit values)
                    if (element.ElementBits == 1)
                    {
                        // These are buttons
                        for (int i = 0; i < element.ElementCount; i++)
                        {
                            int bitPos = reportIdOffset + currentBitOffset + i;
                            buttons.Add(new ButtonCapability
                            {
                                Index = buttonIndex,
                                Name = $"Button {buttonIndex + 1}",
                                BitIndex = bitPos % 8,
                                ByteIndex = bitPos / 8
                            });
                            buttonIndex++;
                        }
                    }
                    else if (element.ElementBits > 1)
                    {
                        // This is likely an axis
                        string axisName = "Axis";
                        
                        // Try to get a nice name from usages
                        var usages = element.Usages.GetAllValues().ToList();
                        if (usages.Any())
                        {
                            axisName = usages.First().ToString()
                                .Replace("GenericDesktop.", "")
                                .Replace("Simulation.", "")
                                .Replace("Game.", "");
                        }
                        else
                        {
                            axisName = $"Axis-{axisIndex}";
                        }

                        for (int i = 0; i < element.ElementCount; i++)
                        {
                            int bitPos = reportIdOffset + currentBitOffset + (i * element.ElementBits);
                            axes.Add(new AxisCapability
                            {
                                Index = axisIndex,
                                Name = element.ElementCount > 1 ? $"{axisName} {i + 1}" : axisName,
                                UsageName = axisName,
                                LogicalMin = element.LogicalMinimum,
                                LogicalMax = element.LogicalMaximum,
                                BitSize = element.ElementBits,
                                BitOffset = bitPos % 8,
                                ByteIndex = bitPos / 8
                            });
                            axisIndex++;
                        }
                    }
                    
                    currentBitOffset += element.ElementCount * element.ElementBits;
                }
            }

            var capabilities = new DeviceCapabilities
            {
                TotalButtons = buttons.Count,
                TotalAxes = axes.Count,
                Axes = axes,
                Buttons = buttons,
                AdditionalInfo = $"VID: 0x{device.VendorID:X4}, PID: 0x{device.ProductID:X4}",
                Manufacturer = device.GetManufacturer() ?? "Unknown",
                ProductName = device.GetProductName() ?? "Unknown",
                VendorId = device.VendorID,
                ProductId = device.ProductID,
                UsagePage = usagePage,
                UsageId = usageId
            };

            // Classify the device type
            capabilities.DeviceType = ClassifyDevice(device, capabilities);

            return capabilities;
        }
        catch
        {
            // If parsing fails, return basic info
            return new DeviceCapabilities
            {
                TotalButtons = 0,
                TotalAxes = 0,
                AdditionalInfo = $"VID: 0x{device.VendorID:X4}, PID: 0x{device.ProductID:X4}",
                DeviceType = DeviceType.Unknown,
                Manufacturer = device.GetManufacturer() ?? "Unknown",
                ProductName = device.GetProductName() ?? "Unknown",
                VendorId = device.VendorID,
                ProductId = device.ProductID,
                UsagePage = 0,
                UsageId = 0
            };
        }
    }



    /// <summary>
    /// Gets a human-readable description of the device type
    /// </summary>
    public static string GetDeviceTypeDescription(DeviceType deviceType)
    {
        return deviceType switch
        {
            DeviceType.SteeringWheel => "Steering Wheel",
            DeviceType.PedalController => "Pedal Controller",
            DeviceType.WheelAndPedals => "Wheel + Pedals",
            DeviceType.Gamepad => "Gamepad",
            DeviceType.Joystick => "Joystick",
            DeviceType.Other => "Other Controller",
            _ => "Unknown Device"
        };
    }
}