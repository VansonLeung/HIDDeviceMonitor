using HidSharp;
using System.Collections.Generic;

namespace HIDDeviceMonitor;

/// <summary>
/// Handles device discovery and management operations
/// </summary>
static class DeviceManager
{
    public static HidDevice[] GetAvailableDevices()
    {
        return DeviceList.Local.GetHidDevices().ToArray();
    }

    public static HidDevice? FindSimagicDevice(HidDevice[] devices)
    {
        return devices.FirstOrDefault(d => d.VendorID == 0x0483 && d.ProductID == 0x0522);
    }

    public static List<InputSource> InitializeDevices(HidDevice[] devices)
    {
        var sources = new List<InputSource>();
        for (int i = 0; i < devices.Length; i++)
        {
            var hidSource = new HidInputSource(devices[i], i);
            if (hidSource.InitializeAsync().Result)
            {
                sources.Add(hidSource);
            }
        }
        return sources;
    }

    public static void DisplayDeviceList(HidDevice[] devices)
    {
        Console.WriteLine("\n--- Available HID Devices ---");
        if (devices.Length == 0)
        {
            Console.WriteLine("No HID devices found.");
            return;
        }

        for (int i = 0; i < devices.Length; i++)
        {
            var device = devices[i];
            var manufacturer = device.GetManufacturer() ?? "Unknown";
            var productName = device.GetProductName() ?? "Unknown";

            Console.WriteLine($"[{i}] {manufacturer} - {productName}");

            // Try to classify the device type without opening it
            try
            {
                var capabilities = DeviceClassifier.ParseDeviceCapabilities(device);
                var deviceTypeDesc = DeviceClassifier.GetDeviceTypeDescription(capabilities.DeviceType);
                Console.WriteLine($"    Type: {deviceTypeDesc} ({capabilities.TotalAxes} axes, {capabilities.TotalButtons} buttons)");
            }
            catch
            {
                Console.WriteLine($"    Type: Unable to determine");
            }


            Console.WriteLine($"    VID: 0x{device.VendorID:X4}, PID: 0x{device.ProductID:X4}");
            Console.WriteLine($"    Path: {device.DevicePath}");
            Console.WriteLine($"    Max Input: {device.GetMaxInputReportLength()} bytes");
            Console.WriteLine($"    Max Output: {device.GetMaxOutputReportLength()} bytes");
        }
    }
}