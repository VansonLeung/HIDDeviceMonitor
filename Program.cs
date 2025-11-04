using HidSharp;
using HidSharp.Reports;
using HidSharp.Reports.Input;
using System;
using System.Linq;
using System.Threading;

namespace HIDDeviceMonitor;

// Helper class to store parsed HID device information
class HidDeviceInfo
{
    public List<ButtonInfo> Buttons { get; set; } = new();
    public List<AxisInfo> Axes { get; set; } = new();
    public int TotalButtons => Buttons.Count;
    public int TotalAxes => Axes.Count;
}

class ButtonInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public int ByteIndex { get; set; }
    public int BitIndex { get; set; }
}

class AxisInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public string UsageName { get; set; } = "";
    public int ByteIndex { get; set; }
    public int BitOffset { get; set; }
    public int BitSize { get; set; }
    public int LogicalMin { get; set; }
    public int LogicalMax { get; set; }
    public bool Is16Bit => BitSize > 8;
}

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== HID Device Monitor ===");
        Console.WriteLine("Focus: Steering Wheels and Pedals\n");

        var deviceList = DeviceList.Local;
        
        while (true)
        {
            Console.WriteLine("\n--- Available HID Devices ---");
            var devices = deviceList.GetHidDevices().ToArray();
            
            if (devices.Length == 0)
            {
                Console.WriteLine("No HID devices found.");
            }
            else
            {
                for (int i = 0; i < devices.Length; i++)
                {
                    var device = devices[i];
                    Console.WriteLine($"[{i}] {device.GetManufacturer()} - {device.GetProductName()}");
                    Console.WriteLine($"    VID: 0x{device.VendorID:X4}, PID: 0x{device.ProductID:X4}");
                    Console.WriteLine($"    Path: {device.DevicePath}");
                    Console.WriteLine($"    Max Input: {device.GetMaxInputReportLength()} bytes");
                    Console.WriteLine($"    Max Output: {device.GetMaxOutputReportLength()} bytes");
                }
            }

            Console.WriteLine("\n--- Options ---");
            Console.WriteLine("Enter device number to monitor (0-{0})", devices.Length - 1);
            Console.WriteLine("'a' - Monitor ALL devices");
            Console.WriteLine("'r' - Refresh device list");
            Console.WriteLine("'q' - Quit");
            Console.Write("\nYour choice: ");
            
            var input = Console.ReadLine()?.Trim().ToLower();
            
            if (input == "q")
            {
                break;
            }
            else if (input == "r")
            {
                continue;
            }
            else if (input == "a")
            {
                MonitorAllDevices(devices);
            }
            else if (int.TryParse(input, out int index) && index >= 0 && index < devices.Length)
            {
                MonitorDevice(devices[index], index);
            }
            else
            {
                Console.WriteLine("Invalid input. Please try again.");
            }
        }
        
        Console.WriteLine("\nExiting...");
    }

    static void MonitorDevice(HidDevice device, int index)
    {
        Console.Clear();
        Console.WriteLine($"=== Monitoring Device [{index}]: {device.GetProductName()} ===");
        Console.WriteLine($"VID: 0x{device.VendorID:X4}, PID: 0x{device.ProductID:X4}\n");

        // Parse the HID Report Descriptor
        var deviceInfo = ParseReportDescriptor(device);
        
        Console.WriteLine($"📊 Device Capabilities:");
        Console.WriteLine($"   Buttons: {deviceInfo.TotalButtons}");
        Console.WriteLine($"   Axes: {deviceInfo.TotalAxes}");
        
        if (deviceInfo.Buttons.Count > 0)
        {
            Console.WriteLine($"\n🔘 Button Layout (first 32):");
            for (int i = 0; i < Math.Min(32, deviceInfo.Buttons.Count); i++)
            {
                var btn = deviceInfo.Buttons[i];
                if (i % 8 == 0 && i > 0) Console.WriteLine();
                if (i % 8 == 0) Console.Write("   ");
                Console.Write($"B{i+1}:[{btn.ByteIndex}].{btn.BitIndex} ");
            }
            Console.WriteLine();
        }
        
        if (deviceInfo.Axes.Count > 0)
        {
            Console.WriteLine($"\n🎮 Detected Axes:");
            foreach (var axis in deviceInfo.Axes)
            {
                string bitInfo = axis.BitOffset != 0 || axis.BitSize % 8 != 0
                    ? $"Byte[{axis.ByteIndex}] + {axis.BitOffset} bits, Size: {axis.BitSize} bits"
                    : $"Bytes[{axis.ByteIndex}..{axis.ByteIndex + (axis.BitSize / 8) - 1}]";
                Console.WriteLine($"   {axis.Name}: {axis.UsageName} (Range: {axis.LogicalMin} to {axis.LogicalMax}) - {bitInfo}");
            }
        }
        
        Console.WriteLine("\n⌨️  Press 'd' for debug mode, any other key to stop monitoring\n");
        Console.WriteLine(new string('─', 80));

        try
        {
            HidStream stream;
            if (!device.TryOpen(out stream))
            {
                Console.WriteLine("❌ Failed to open device. It may be in use or require elevated permissions.");
                Console.WriteLine("On macOS, you may need to grant Input Monitoring permissions.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            using (stream)
            {
                stream.ReadTimeout = 100; // Short timeout for responsive display
                var buffer = new byte[device.GetMaxInputReportLength()];
                var previousBuffer = new byte[device.GetMaxInputReportLength()];
                
                var cancellationSource = new CancellationTokenSource();
                bool debugMode = false;
                
                // Start a background task to check for key press
                var keyCheckTask = Task.Run(() =>
                {
                    var key = Console.ReadKey(true);
                    if (key.KeyChar == 'd' || key.KeyChar == 'D')
                    {
                        debugMode = true;
                        Console.Clear();
                        Console.WriteLine("=== DEBUG MODE ACTIVATED ===");
                        Console.WriteLine("Shows all bytes and highlights changes\n");
                        Console.WriteLine("Press any key to stop\n");
                        Console.WriteLine(new string('─', 80));
                        Console.ReadKey(true);
                    }
                    cancellationSource.Cancel();
                });

                // Store the console position where we'll update the display
                int displayStartLine = Console.CursorTop;
                var lastUpdateTime = DateTime.Now;

                while (!cancellationSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        
                        if (bytesRead > 0)
                        {
                            // Throttle updates to avoid flickering (update every 50ms)
                            var now = DateTime.Now;
                            if ((now - lastUpdateTime).TotalMilliseconds < 50)
                            {
                                continue;
                            }
                            lastUpdateTime = now;

                            // Move cursor back to display start position
                            Console.SetCursorPosition(0, displayStartLine);
                            
                            if (debugMode)
                            {
                                DisplayDebugMode(buffer, previousBuffer, bytesRead, now);
                            }
                            else
                            {
                                // Display timestamp and data size
                                Console.WriteLine($"🕐 Last Update: {now:HH:mm:ss.fff} | Data Size: {bytesRead} bytes{new string(' ', 30)}");
                                Console.WriteLine();
                                
                                // Display Buttons
                                DisplayButtons(buffer, deviceInfo);
                                Console.WriteLine();
                                
                                // Display Axes
                                DisplayAxes(buffer, deviceInfo);
                                Console.WriteLine();
                                
                                // Display raw data (compact view)
                                DisplayRawData(buffer, bytesRead);
                                
                                // Clear any remaining lines from previous display
                                for (int i = 0; i < 3; i++)
                                {
                                    Console.WriteLine(new string(' ', 80));
                                }
                            }
                            
                            // Copy current buffer to previous for comparison
                            Array.Copy(buffer, previousBuffer, bytesRead);
                        }
                    }
                    catch (TimeoutException)
                    {
                        // Timeout is normal, just continue
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\n❌ Error reading data: {ex.Message}{new string(' ', 50)}");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }

        Console.WriteLine("\n\n✅ Stopped monitoring. Press any key to continue...");
        Console.ReadKey();
    }

    static HidDeviceInfo ParseReportDescriptor(HidDevice device)
    {
        var info = new HidDeviceInfo();
        
        try
        {
            var reportDescriptor = device.GetReportDescriptor();
            
            // Find input reports
            var inputReports = reportDescriptor.InputReports.ToArray();
            
            if (inputReports.Length > 0)
            {
                var inputReport = inputReports[0]; // Use first input report
                var dataItems = inputReport.DataItems.ToArray();
                
                int buttonIndex = 0;
                int axisIndex = 0;
                int currentBitOffset = 0;
                
                // Check if report uses Report ID (if so, descriptor offsets need +1 byte adjustment)
                bool hasReportId = inputReport.ReportID != 0;
                int reportIdOffset = hasReportId ? 8 : 0; // Add 8 bits (1 byte) if Report ID exists
                
                foreach (var item in dataItems)
                {
                    var usages = item.Usages.GetAllValues().ToArray();
                    
                    if (usages.Length > 0)
                    {
                        var usage = usages[0];
                        var usagePage = usage >> 16;
                        var usageId = usage & 0xFFFF;
                        
                        // Check if this is a button (Usage Page 0x09 = Button)
                        if (usagePage == 0x09)
                        {
                            // This is a button collection
                            for (int i = 0; i < item.ElementCount; i++)
                            {
                                int bitPos = reportIdOffset + currentBitOffset + (i * item.ElementBits);
                                info.Buttons.Add(new ButtonInfo
                                {
                                    Index = buttonIndex++,
                                    Name = $"Button {buttonIndex}",
                                    ByteIndex = bitPos / 8,
                                    BitIndex = bitPos % 8
                                });
                            }
                        }
                        // Check for axes (Usage Page 0x01 = Generic Desktop)
                        else if (usagePage == 0x01)
                        {
                            string usageName = GetUsageName((int)usageId);
                            
                            for (int i = 0; i < item.ElementCount; i++)
                            {
                                int bitPos = reportIdOffset + currentBitOffset + (i * item.ElementBits);
                                info.Axes.Add(new AxisInfo
                                {
                                    Index = axisIndex++,
                                    Name = item.ElementCount > 1 ? $"{usageName} {i + 1}" : usageName,
                                    UsageName = usageName,
                                    ByteIndex = bitPos / 8,
                                    BitOffset = bitPos % 8,
                                    BitSize = item.ElementBits,
                                    LogicalMin = item.LogicalMinimum,
                                    LogicalMax = item.LogicalMaximum
                                });
                            }
                        }
                    }
                    
                    currentBitOffset += item.ElementCount * item.ElementBits;
                }
                
                if (hasReportId)
                {
                    Console.WriteLine($"ℹ️  Device uses Report ID: {inputReport.ReportID} (offsets adjusted +1 byte)\n");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not parse report descriptor: {ex.Message}");
            Console.WriteLine("Will use basic interpretation instead.\n");
        }
        
        // Fallback: if no descriptor info, use basic interpretation
        if (info.TotalButtons == 0 && info.TotalAxes == 0)
        {
            Console.WriteLine("ℹ️  Using fallback interpretation (descriptor parsing failed)");
            Console.WriteLine("   Assuming: Byte[0]=Report ID, Bytes[1-4]=Buttons, Bytes after=Axes\n");
            
            // Assume common layout: first 16-32 buttons, then axes
            // Note: Byte 0 is often the Report ID, so buttons typically start at byte 1
            for (int i = 0; i < 32; i++)
            {
                info.Buttons.Add(new ButtonInfo
                {
                    Index = i,
                    Name = $"Button {i + 1}",
                    ByteIndex = 1 + (i / 8), // Start at byte 1 (skip report ID)
                    BitIndex = i % 8
                });
            }
            
            // Common axes - adjust based on typical HID game controller layout
            // Most racing wheels have steering (16-bit) starting around byte 1-2 or 2-3
            string[] axisNames = { "X-Axis", "Y-Axis", "Z-Axis", "Rx-Axis", "Ry-Axis", "Rz-Axis", "Slider", "Dial" };
            
            // Create multiple interpretations for common layouts
            // Layout 1: Steering at bytes [1-2] (what SIMAGIC seems to use based on your observation)
            info.Axes.Add(new AxisInfo
            {
                Index = 0,
                Name = "X-Axis (Steering?)",
                UsageName = "X-Axis",
                ByteIndex = 1,
                BitOffset = 0,
                BitSize = 16,
                LogicalMin = 0,
                LogicalMax = 65535
            });
            
            // Other potential axes
            for (int i = 1; i < 8; i++)
            {
                info.Axes.Add(new AxisInfo
                {
                    Index = i,
                    Name = axisNames[i],
                    UsageName = axisNames[i],
                    ByteIndex = 1 + (i * 2), // 16-bit axes, starting after first axis
                    BitOffset = 0,
                    BitSize = 16,
                    LogicalMin = 0,
                    LogicalMax = 65535
                });
            }
        }
        
        return info;
    }

    static string GetUsageName(int usageId)
    {
        return usageId switch
        {
            0x30 => "X-Axis",
            0x31 => "Y-Axis",
            0x32 => "Z-Axis",
            0x33 => "Rx-Axis",
            0x34 => "Ry-Axis",
            0x35 => "Rz-Axis",
            0x36 => "Slider",
            0x37 => "Dial",
            0x38 => "Wheel",
            0x39 => "Hat Switch",
            0xC4 => "Accelerator",
            0xC5 => "Brake",
            _ => $"Usage 0x{usageId:X2}"
        };
    }

    static void DisplayDebugMode(byte[] buffer, byte[] previousBuffer, int length, DateTime now)
    {
        Console.WriteLine($"🕐 {now:HH:mm:ss.fff} | DEBUG MODE - All Bytes (changed bytes highlighted){new string(' ', 20)}");
        Console.WriteLine();
        
        // Display all bytes in a grid format, 16 bytes per row
        for (int row = 0; row < (length + 15) / 16; row++)
        {
            int startIdx = row * 16;
            Console.Write($"[{startIdx,3}]  ");
            
            // Display hex values
            for (int col = 0; col < 16 && startIdx + col < length; col++)
            {
                int idx = startIdx + col;
                bool changed = idx < previousBuffer.Length && buffer[idx] != previousBuffer[idx];
                
                if (changed)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.BackgroundColor = ConsoleColor.DarkRed;
                }
                
                Console.Write($"{buffer[idx]:X2} ");
                
                if (changed)
                {
                    Console.ResetColor();
                }
            }
            
            // Pad the rest if less than 16 bytes
            int remaining = 16 - Math.Min(16, length - startIdx);
            Console.Write(new string(' ', remaining * 3));
            
            Console.Write("  ");
            
            // Display 16-bit values (little-endian)
            for (int col = 0; col < 16 && startIdx + col + 1 < length; col += 2)
            {
                int idx = startIdx + col;
                int value = buffer[idx] | (buffer[idx + 1] << 8);
                Console.Write($"{value,6} ");
            }
            
            Console.WriteLine(new string(' ', 10));
        }
        
        Console.WriteLine();
        Console.WriteLine("💡 Look for changing bytes when you move controls");
        Console.WriteLine("   16-bit values are shown on the right (little-endian)");
        Console.WriteLine(new string(' ', 80));
    }

    static void DisplayButtons(byte[] buffer, HidDeviceInfo deviceInfo)
    {
        Console.WriteLine("🔘 BUTTONS:");
        
        if (deviceInfo.TotalButtons == 0)
        {
            Console.WriteLine("   No buttons detected");
            return;
        }
        
        var pressedButtons = new List<int>();
        var buttonStates = new List<(int index, bool pressed)>();
        
        foreach (var button in deviceInfo.Buttons)
        {
            if (button.ByteIndex < buffer.Length)
            {
                bool isPressed = (buffer[button.ByteIndex] & (1 << button.BitIndex)) != 0;
                buttonStates.Add((button.Index + 1, isPressed));
                if (isPressed)
                {
                    pressedButtons.Add(button.Index + 1);
                }
            }
        }
        
        // Show pressed buttons
        if (pressedButtons.Count > 0)
        {
            Console.Write("   Pressed: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(string.Join(", ", pressedButtons.Select(b => $"B{b}")));
            Console.ResetColor();
            Console.WriteLine($"{new string(' ', 50)}");
        }
        else
        {
            Console.WriteLine($"   None pressed{new string(' ', 60)}");
        }
        
        // Show grid of first 32 buttons (or fewer)
        int displayCount = Math.Min(32, buttonStates.Count);
        if (displayCount > 0)
        {
            Console.Write("   Status: ");
            for (int i = 0; i < displayCount; i++)
            {
                var (index, pressed) = buttonStates[i];
                
                if (i > 0 && i % 16 == 0)
                {
                    Console.WriteLine();
                    Console.Write("           ");
                }
                
                if (pressed)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"[B{index,2}]");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($" B{index,2} ");
                    Console.ResetColor();
                }
            }
            Console.WriteLine($"{new string(' ', 20)}");
        }
        
        Console.WriteLine($"   Total: {deviceInfo.TotalButtons} buttons (showing first {displayCount}){new string(' ', 30)}");
    }

    static void DisplayAxes(byte[] buffer, HidDeviceInfo deviceInfo)
    {
        Console.WriteLine("🎚️  AXES:");
        
        if (deviceInfo.TotalAxes == 0)
        {
            Console.WriteLine("   No axes detected");
            return;
        }
        
        foreach (var axis in deviceInfo.Axes.Take(10)) // Show first 10 axes
        {
            if (axis.ByteIndex < buffer.Length)
            {
                int value = 0;
                
                // Handle bit-aligned values (for packed HID data)
                if (axis.BitOffset != 0 || (axis.BitSize % 8 != 0))
                {
                    // Bit-packed data - need to extract bits carefully
                    int totalBits = axis.BitSize;
                    int bitPosition = axis.BitOffset;
                    int byteIndex = axis.ByteIndex;
                    
                    // Read enough bytes to cover all bits
                    int bytesNeeded = ((bitPosition + totalBits + 7) / 8);
                    
                    if (byteIndex + bytesNeeded <= buffer.Length)
                    {
                        // Extract bits from the buffer
                        for (int i = 0; i < totalBits; i++)
                        {
                            int currentBit = bitPosition + i;
                            int currentByte = byteIndex + (currentBit / 8);
                            int currentBitInByte = currentBit % 8;
                            
                            if (currentByte < buffer.Length)
                            {
                                int bitValue = (buffer[currentByte] >> currentBitInByte) & 1;
                                value |= (bitValue << i);
                            }
                        }
                    }
                }
                else
                {
                    // Byte-aligned values (standard case)
                    if (axis.BitSize <= 8)
                    {
                        value = buffer[axis.ByteIndex];
                    }
                    else if (axis.BitSize <= 16 && axis.ByteIndex + 1 < buffer.Length)
                    {
                        // 16-bit value (little-endian)
                        value = buffer[axis.ByteIndex] | (buffer[axis.ByteIndex + 1] << 8);
                    }
                    else if (axis.BitSize <= 32 && axis.ByteIndex + 3 < buffer.Length)
                    {
                        // 32-bit value (little-endian)
                        value = buffer[axis.ByteIndex] | 
                               (buffer[axis.ByteIndex + 1] << 8) |
                               (buffer[axis.ByteIndex + 2] << 16) |
                               (buffer[axis.ByteIndex + 3] << 24);
                    }
                }
                
                // Handle signed values if logical min is negative
                if (axis.LogicalMin < 0 && axis.BitSize > 0)
                {
                    int signBit = 1 << (axis.BitSize - 1);
                    if ((value & signBit) != 0)
                    {
                        // Sign extend
                        value |= ~((1 << axis.BitSize) - 1);
                    }
                }
                
                string rangeInfo = axis.LogicalMax != 0 ? $"({axis.LogicalMin}..{axis.LogicalMax})" : "";
                
                // Show byte range and bit info
                int lastByte = axis.ByteIndex + ((axis.BitOffset + axis.BitSize - 1) / 8);
                string byteInfo = axis.BitOffset != 0 || axis.BitSize % 8 != 0
                    ? $"[{axis.ByteIndex}-{lastByte}]:{axis.BitOffset}bit+{axis.BitSize}bit"
                    : axis.BitSize <= 8 ? $"[{axis.ByteIndex}]" : 
                      axis.BitSize <= 16 ? $"[{axis.ByteIndex}-{axis.ByteIndex + 1}]" :
                      $"[{axis.ByteIndex}-{axis.ByteIndex + 3}]";
                
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"   {axis.Name,-15}");
                Console.ResetColor();
                Console.Write($": {value,6} {rangeInfo,-20} ");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"bytes{byteInfo}");
                Console.ResetColor();
                Console.WriteLine($"{new string(' ', 5)}");
            }
        }
        
        Console.WriteLine($"   Total: {deviceInfo.TotalAxes} axes{new string(' ', 50)}");
    }

    static void DisplayRawData(byte[] buffer, int length)
    {
        Console.WriteLine("📊 RAW DATA (non-zero bytes):");
        Console.Write("   ");
        
        int displayCount = 0;
        for (int i = 0; i < length && displayCount < 32; i++)
        {
            if (buffer[i] != 0)
            {
                Console.Write($"[{i}]=0x{buffer[i]:X2} ");
                displayCount++;
            }
        }
        
        if (displayCount == 0)
        {
            Console.Write("All zeros");
        }
        
        Console.WriteLine(new string(' ', 50));
    }

    static void MonitorAllDevices(HidDevice[] devices)
    {
        Console.Clear();
        Console.WriteLine("=== Monitoring ALL Devices ===");
        Console.WriteLine("Press any key to stop monitoring\n");

        var deviceInfos = new List<(HidDevice device, HidStream stream, byte[] buffer, int index, HidDeviceInfo info)>();
        var cancellationSource = new CancellationTokenSource();

        try
        {
            // Open all devices and parse their descriptors
            for (int i = 0; i < devices.Length; i++)
            {
                try
                {
                    if (devices[i].TryOpen(out HidStream stream))
                    {
                        stream.ReadTimeout = 50; // Very short timeout for multiple devices
                        var buffer = new byte[devices[i].GetMaxInputReportLength()];
                        var info = ParseReportDescriptor(devices[i]);
                        deviceInfos.Add((devices[i], stream, buffer, i, info));
                        Console.WriteLine($"✅ Opened device [{i}]: {devices[i].GetProductName()}");
                        Console.WriteLine($"   Buttons: {info.TotalButtons}, Axes: {info.TotalAxes}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to open device [{i}]: {ex.Message}");
                }
            }

            if (deviceInfos.Count == 0)
            {
                Console.WriteLine("No devices could be opened.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine($"\n📡 Monitoring {deviceInfos.Count} device(s)...");
            Console.WriteLine(new string('─', 80));
            
            // Start a background task to check for key press
            var keyCheckTask = Task.Run(() =>
            {
                Console.ReadKey(true);
                cancellationSource.Cancel();
            });

            int displayStartLine = Console.CursorTop;
            var lastUpdateTime = DateTime.Now;

            while (!cancellationSource.Token.IsCancellationRequested)
            {
                bool hasData = false;
                
                foreach (var (device, stream, buffer, index, info) in deviceInfos)
                {
                    try
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        
                        if (bytesRead > 0)
                        {
                            hasData = true;
                            
                            // Throttle updates
                            var now = DateTime.Now;
                            if ((now - lastUpdateTime).TotalMilliseconds < 50)
                            {
                                continue;
                            }
                            lastUpdateTime = now;

                            Console.SetCursorPosition(0, displayStartLine);
                            
                            Console.WriteLine($"🕐 {now:HH:mm:ss.fff} | Device[{index}]: {device.GetProductName()}{new string(' ', 30)}");
                            Console.WriteLine();
                            
                            DisplayButtons(buffer, info);
                            Console.WriteLine();
                            DisplayAxes(buffer, info);
                            Console.WriteLine();
                            DisplayRawData(buffer, bytesRead);
                            
                            // Clear remaining lines
                            for (int i = 0; i < 2; i++)
                            {
                                Console.WriteLine(new string(' ', 80));
                            }
                        }
                    }
                    catch (TimeoutException)
                    {
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\n❌ Device[{index}] error: {ex.Message}{new string(' ', 40)}");
                    }
                }
                
                if (!hasData)
                {
                    Thread.Sleep(10);
                }
            }
        }
        finally
        {
            // Clean up all streams
            foreach (var (_, stream, _, _, _) in deviceInfos)
            {
                stream?.Dispose();
            }
        }

        Console.WriteLine("\n\n✅ Stopped monitoring. Press any key to continue...");
        Console.ReadKey();
    }
}
