using System;
using System.Collections.Generic;
using System.Linq;

namespace HIDDeviceMonitor;

/// <summary>
/// Handles all display/rendering logic
/// </summary>
public static class DisplayManager
{
    public static void ShowDeviceInfo(InputSource source, DeviceCapabilities caps, bool debugMode = false)
    {
        Console.WriteLine($"=== {source.Name} ===");
        Console.WriteLine($"Type: {source.DeviceType} | Buttons: {caps.TotalButtons} | Axes: {caps.TotalAxes}");

        if (!string.IsNullOrEmpty(caps.AdditionalInfo))
        {
            Console.WriteLine($"Info: {caps.AdditionalInfo}");
        }

        Console.WriteLine(debugMode
            ? "DEBUG MODE – raw bytes shown"
            : "Press 'd' to toggle debug • Any other key to stop");
        Console.WriteLine(new string('-', 60));
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
        Console.WriteLine($"Last Update: {state.Timestamp:HH:mm:ss.fff} • Payload: {state.RawData.Length} bytes");
        ShowButtons(state, caps);
        ShowAxes(state);
        ShowRawData(state.RawData);
    }

    private static void ShowButtons(InputState state, DeviceCapabilities caps)
    {
        Console.Write("Buttons: ");

        if (caps.TotalButtons == 0)
        {
            Console.WriteLine("none detected");
            return;
        }

        var pressedButtons = state.Buttons.Where(kvp => kvp.Value).Select(kvp => kvp.Key + 1).ToList();
        string pressedInfo = pressedButtons.Count == 0
            ? "none"
            : string.Join(',', pressedButtons.Take(8).Select(b => $"B{b}"));

        if (pressedButtons.Count > 8)
        {
            pressedInfo += ", ...";
        }

        Console.WriteLine($"pressed [{pressedInfo}] | total {caps.TotalButtons}");

        int slotCount = Math.Min(16, caps.TotalButtons);
        if (slotCount == 0)
        {
            return;
        }

        var compact = new char[slotCount];
        for (int i = 0; i < slotCount; i++)
        {
            bool pressed = state.Buttons.TryGetValue(i, out bool value) && value;
            compact[i] = pressed ? 'X' : '.';
        }

        Console.WriteLine($"         [{new string(compact)}] (first {slotCount})");
    }

    private static void ShowAxes(InputState state)
    {
        Console.Write("Axes: ");

        if (state.Axes.Count == 0)
        {
            Console.WriteLine("none detected");
            return;
        }

        var axisLines = state.Axes.Take(5)
            .Select(kvp => $"{kvp.Key}={kvp.Value.Value}")
            .ToList();

        Console.WriteLine(string.Join(" | ", axisLines));
        if (state.Axes.Count > 5)
        {
            Console.WriteLine($"       (+{state.Axes.Count - 5} more axes)");
        }
    }

    private static void ShowRawData(byte[] data)
    {
        Console.Write("Raw bytes: ");

        int shown = 0;
        for (int i = 0; i < data.Length && shown < 16; i++)
        {
            if (data[i] != 0)
            {
                Console.Write($"[{i}:{data[i]:X2}] ");
                shown++;
            }
        }

        if (shown == 0)
        {
            Console.Write("all zeros");
        }

        Console.WriteLine();
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
