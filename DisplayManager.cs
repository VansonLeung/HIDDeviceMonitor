using System;
using System.Collections.Generic;
using System.Linq;

namespace HIDDeviceMonitor;

/// <summary>
/// Handles all display/rendering logic
/// </summary>
public static class DisplayManager
{
    public static void ShowDeviceInfo(InputSource source, DeviceCapabilities caps)
    {
        Console.Clear();
        Console.WriteLine($"=== Monitoring: {source.Name} ===");
        Console.WriteLine($"Type: {source.DeviceType}");
        if (!string.IsNullOrEmpty(caps.AdditionalInfo))
        {
            Console.WriteLine($"ℹ️  {caps.AdditionalInfo}");
        }
        Console.WriteLine();

        Console.WriteLine($"📊 Device Capabilities:");
        Console.WriteLine($"   Buttons: {caps.TotalButtons}");
        Console.WriteLine($"   Axes: {caps.TotalAxes}");

        if (caps.Buttons.Count > 0)
        {
            Console.WriteLine($"\n🔘 Button Layout (first 32):");
            for (int i = 0; i < Math.Min(32, caps.Buttons.Count); i++)
            {
                var btn = caps.Buttons[i];
                if (i % 8 == 0 && i > 0) Console.WriteLine();
                if (i % 8 == 0) Console.Write("   ");
                Console.Write($"B{i + 1}:[{btn.ByteIndex}].{btn.BitIndex} ");
            }
            Console.WriteLine();
        }

        if (caps.Axes.Count > 0)
        {
            Console.WriteLine($"\n🎮 Detected Axes:");
            foreach (var axis in caps.Axes)
            {
                string bitInfo = axis.BitOffset != 0 || axis.BitSize % 8 != 0
                    ? $"Byte[{axis.ByteIndex}] + {axis.BitOffset} bits, Size: {axis.BitSize} bits"
                    : $"Bytes[{axis.ByteIndex}..{axis.ByteIndex + (axis.BitSize / 8) - 1}]";
                Console.WriteLine($"   {axis.Name}: {axis.UsageName} (Range: {axis.LogicalMin} to {axis.LogicalMax}) - {bitInfo}");
            }
        }

        Console.WriteLine("\n⌨️  Press 'd' for debug mode, any other key to stop monitoring\n");
        Console.WriteLine(new string('─', 80));
    }

    public static void ShowInputState(InputState state, DeviceCapabilities caps, bool debugMode = false)
    {
        if (debugMode)
        {
            ShowDebugMode(state);
        }
        else
        {
            ShowNormalMode(state, caps);
        }
    }

    private static void ShowNormalMode(InputState state, DeviceCapabilities caps)
    {
        Console.WriteLine($"🕐 Last Update: {state.Timestamp:HH:mm:ss.fff} | Data Size: {state.RawData.Length} bytes{new string(' ', 30)}");
        Console.WriteLine();

        ShowButtons(state, caps);
        Console.WriteLine();

        ShowAxes(state);
        Console.WriteLine();

        ShowRawData(state.RawData);

        // Clear remaining lines
        for (int i = 0; i < 3; i++)
        {
            Console.WriteLine(new string(' ', 80));
        }
    }

    private static void ShowButtons(InputState state, DeviceCapabilities caps)
    {
        Console.WriteLine("🔘 BUTTONS:");

        if (caps.TotalButtons == 0)
        {
            Console.WriteLine("   No buttons detected");
            return;
        }

        var pressedButtons = state.Buttons.Where(kvp => kvp.Value).Select(kvp => kvp.Key + 1).ToList();

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

        // Show grid of first 32 buttons
        int displayCount = Math.Min(32, caps.TotalButtons);
        if (displayCount > 0)
        {
            Console.Write("   Status: ");
            for (int i = 0; i < displayCount; i++)
            {
                if (i > 0 && i % 16 == 0)
                {
                    Console.WriteLine();
                    Console.Write("           ");
                }

                bool pressed = state.Buttons.ContainsKey(i) && state.Buttons[i];

                if (pressed)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"[B{i + 1,2}]");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($" B{i + 1,2} ");
                    Console.ResetColor();
                }
            }
            Console.WriteLine($"{new string(' ', 20)}");
        }

        Console.WriteLine($"   Total: {caps.TotalButtons} buttons (showing first {displayCount}){new string(' ', 30)}");
    }

    private static void ShowAxes(InputState state)
    {
        Console.WriteLine("🎚️  AXES:");

        if (state.Axes.Count == 0)
        {
            Console.WriteLine("   No axes detected");
            return;
        }

        foreach (var kvp in state.Axes.Take(10))
        {
            var axis = kvp.Value;
            string rangeInfo = axis.MaxValue != 0 ? $"({axis.MinValue}..{axis.MaxValue})" : "";

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"   {axis.Name,-20}");
            Console.ResetColor();
            Console.Write($": {axis.Value,6} {rangeInfo,-20} ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"bytes{axis.ByteInfo}");
            Console.ResetColor();
            Console.WriteLine($"{new string(' ', 5)}");
        }

        Console.WriteLine($"   Total: {state.Axes.Count} axes{new string(' ', 50)}");
    }

    private static void ShowRawData(byte[] data)
    {
        Console.WriteLine("📊 RAW DATA (non-zero bytes):");
        Console.Write("   ");

        int displayCount = 0;
        for (int i = 0; i < data.Length && displayCount < 32; i++)
        {
            if (data[i] != 0)
            {
                Console.Write($"[{i}]=0x{data[i]:X2} ");
                displayCount++;
            }
        }

        if (displayCount == 0)
        {
            Console.Write("All zeros");
        }

        Console.WriteLine(new string(' ', 50));
    }

    private static void ShowDebugMode(InputState state)
    {
        Console.WriteLine($"🕐 {state.Timestamp:HH:mm:ss.fff} | DEBUG MODE - All Bytes{new string(' ', 20)}");
        Console.WriteLine();

        var data = state.RawData;
        
        // Display all bytes in a grid format, 16 bytes per row
        for (int row = 0; row < (data.Length + 15) / 16; row++)
        {
            int startIdx = row * 16;
            Console.Write($"[{startIdx,3}]  ");

            // Display hex values
            for (int col = 0; col < 16 && startIdx + col < data.Length; col++)
            {
                int idx = startIdx + col;
                Console.Write($"{data[idx]:X2} ");
            }

            // Pad the rest if less than 16 bytes
            int remaining = 16 - Math.Min(16, data.Length - startIdx);
            Console.Write(new string(' ', remaining * 3));

            Console.Write("  ");

            // Display 16-bit values (little-endian)
            for (int col = 0; col < 16 && startIdx + col + 1 < data.Length; col += 2)
            {
                int idx = startIdx + col;
                int value = data[idx] | (data[idx + 1] << 8);
                Console.Write($"{value,6} ");
            }

            Console.WriteLine(new string(' ', 10));
        }

        Console.WriteLine();
        Console.WriteLine("💡 Hex values on left, 16-bit interpretations on right");
        Console.WriteLine(new string(' ', 80));
    }
}
