using HidSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HIDDeviceMonitor;

/// <summary>
/// Handles different application modes and monitoring operations
/// </summary>
static class ModeRunner
{
    private static ProgramConfig _config = new();

    // Generic callback for side effects (like WebSocket broadcasting)
    private static Action<InputState, DeviceCapabilities>? _onInputData;

    public static void SetConfig(ProgramConfig config)
    {
        _config = config;
    }

    public static void SetInputDataCallback(Action<InputState, DeviceCapabilities> onInputData)
    {
        _onInputData = onInputData;
    }

    public static void RunAutoConnectMode()
    {
        Console.WriteLine("🔍 Auto-connecting to all HID devices...");

        var devices = DeviceManager.GetAvailableDevices();

        MonitorAllDevices(devices);

        // Look for Simagic device (VID: 0x0483, PID: 0x0522)
        // HidDevice? simagicDevice = null;
        // int deviceIndex = -1;

        // for (int i = 0; i < devices.Length; i++)
        // {
        //     var device = devices[i];
        //     if (device.VendorID == 0x0483 && device.ProductID == 0x0522)
        //     {
        //         simagicDevice = device;
        //         deviceIndex = i;
        //         break;
        //     }
        // }

        // if (simagicDevice == null)
        // {
        //     Console.WriteLine("❌ No Simagic steering wheel found.");
        //     Console.WriteLine("💡 Use 'HIDDeviceMonitor -m' to see all available devices and select manually.");
        //     return;
        // }

        // Console.WriteLine($"✅ Found Simagic device: {simagicDevice.GetProductName()}");
        // Console.WriteLine("🎮 Starting monitoring... (Press Ctrl+C to stop)\n");

        // using var hidSource = new HidInputSource(simagicDevice, deviceIndex);
        // if (hidSource.InitializeAsync().Result)
        // {
        //     MonitorInputSource(hidSource, false, webSocketServer);
        // }
        // else
        // {
        //     Console.WriteLine("❌ Failed to initialize Simagic device.");
        // }
    }

    public static void RunKeyboardMode()
    {
        Console.WriteLine("🎹 Keyboard Simulation Mode");
        Console.WriteLine($"Damping: {(_config.DisableDamping ? "Disabled" : "Enabled")}\n");

        using var keyboardSource = new KeyboardInputSource(_config.DisableDamping);

        if (!keyboardSource.InitializeAsync().Result)
        {
            Console.WriteLine("❌ Failed to initialize keyboard input");
            return;
        }

        MonitorInputSource(keyboardSource, true);
    }

    public static void RunHidDeviceMode()
    {
        while (true)
        {
            var devices = DeviceManager.GetAvailableDevices();
            DeviceManager.DisplayDeviceList(devices);

            Console.WriteLine("\n--- Options ---");
            Console.WriteLine($"Enter device number to monitor (0-{devices.Length - 1})");
            Console.WriteLine("'k' - Use Keyboard simulation mode");
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
            else if (input == "k")
            {
                RunKeyboardMode();
                continue; // Go back to menu instead of exiting
            }
            else if (input == "a")
            {
                MonitorAllDevices(devices);
            }
            else if (int.TryParse(input, out int index) && index >= 0 && index < devices.Length)
            {
                using var hidSource = new HidInputSource(devices[index], index);
                if (hidSource.InitializeAsync().Result)
                {
                    MonitorInputSource(hidSource, false);
                }
            }
            else
            {
                Console.WriteLine("Invalid input. Please try again.");
            }
        }

        Console.WriteLine("\nExiting...");
    }

    public static void MonitorInputSource(InputSource source, bool isKeyboardMode)
    {
        var caps = source.GetCapabilities();

        DisplayManager.ShowDeviceInfo(source, caps, false); // Initially not in debug mode

        var cancellationSource = new CancellationTokenSource();
        bool debugMode = false;

        // Only start background key check for HID devices (not keyboard mode)
        Task? keyCheckTask = null;
        if (!isKeyboardMode)
        {
            keyCheckTask = Task.Run(() =>
            {
                while (!cancellationSource.Token.IsCancellationRequested)
                {
                    var key = Console.ReadKey(true);
                    if (key.KeyChar == 'd' || key.KeyChar == 'D')
                    {
                        debugMode = !debugMode; // Toggle debug mode
                        if (debugMode)
                        {
                            Console.WriteLine("=== DEBUG MODE ACTIVATED ===");
                            Console.WriteLine("Press 'd' again to exit debug mode, any other key to stop monitoring");
                        }
                        else
                        {
                            Console.WriteLine("=== DEBUG MODE DEACTIVATED ===");
                        }
                        Thread.Sleep(500); // Brief pause to show the message
                    }
                    else
                    {
                        cancellationSource.Cancel();
                        break;
                    }
                }
            });
        }

        // Use common monitoring loop
        MonitorSources(new[] { (source, caps) }, cancellationSource, () => debugMode, isKeyboardMode);
    }

    public static void MonitorAllDevices(HidDevice[] devices)
    {
        Console.Clear();
        Console.WriteLine("=== Monitoring ALL Devices ===");
        Console.WriteLine("Press any key to stop monitoring\n");

        var sources = new List<(InputSource source, DeviceCapabilities caps)>();

        try
        {
            // Open all devices
            for (int i = 0; i < devices.Length; i++)
            {
                var hidSource = new HidInputSource(devices[i], i);
                if (hidSource.InitializeAsync().Result)
                {
                    var caps = hidSource.GetCapabilities();
                    sources.Add((hidSource, caps));
                    Console.WriteLine($"✅ Opened device [{i}]: {hidSource.Name}");
                    Console.WriteLine($"   Buttons: {caps.TotalButtons}, Axes: {caps.TotalAxes}");
                }
            }

            if (sources.Count == 0)
            {
                Console.WriteLine("No devices could be opened.");
                Console.WriteLine("\nPress any key to continue...");
                Console.Read();
                return;
            }

            Console.WriteLine($"\n📡 Monitoring {sources.Count} device(s)...");
            Console.WriteLine(new string('─', 80));

            var cancellationSource = new CancellationTokenSource();
            var keyCheckTask = Task.Run(() =>
            {
                Console.Read();
                cancellationSource.Cancel();
            });

            // Use common monitoring loop for all devices
            MonitorSources(sources.ToArray(), cancellationSource, () => false, false);
        }
        finally
        {
            foreach (var (source, _) in sources)
            {
                source?.Dispose();
            }
        }

        Console.WriteLine("\n\n✅ Stopped monitoring. Press any key to continue...");
        Console.ReadKey();
    }

    public static void MonitorSources((InputSource source, DeviceCapabilities caps)[] sourcePairs, CancellationTokenSource cancellationSource, Func<bool> getDebugMode, bool isKeyboardMode)
    {
        var lastUpdateTime = DateTime.Now;
        int currentDeviceIndex = 0;
        var lastDeviceSwitch = DateTime.Now;

        while (!cancellationSource.Token.IsCancellationRequested)
        {
            bool hasActiveSources = false;

            // For multiple devices, cycle through them
            if (sourcePairs.Length > 1)
            {
                var now = DateTime.Now;
                if ((now - lastDeviceSwitch).TotalSeconds >= 2) // Switch every 2 seconds
                {
                    currentDeviceIndex = (currentDeviceIndex + 1) % sourcePairs.Length;
                    lastDeviceSwitch = now;
                }
            }

            var (source, caps) = sourcePairs[currentDeviceIndex];

            if (source.IsConnected)
            {
                hasActiveSources = true;

                try
                {
                    source.Poll();
                    var state = source.GetCurrentState();

                    // Call side effect callback (e.g., WebSocket broadcasting)
                    _onInputData?.Invoke(state, caps);

                    // Throttle updates to avoid flickering
                    var now = DateTime.Now;
                    if ((now - lastUpdateTime).TotalMilliseconds < 30)
                    {
                        Thread.Sleep(10);
                        continue;
                    }
                    lastUpdateTime = now;

                    // Clear screen and redraw everything
                    Console.Clear();

                    if (sourcePairs.Length == 1)
                    {
                        // Single device mode - show device info
                        DisplayManager.ShowDeviceInfo(source, caps, getDebugMode());
                    }
                    else
                    {
                        // Multi-device mode - show current device header
                        Console.WriteLine($"=== Monitoring: {source.Name} ({currentDeviceIndex + 1}/{sourcePairs.Length}) ===");
                        Console.WriteLine($"Type: {source.DeviceType}");
                        if (!string.IsNullOrEmpty(caps.AdditionalInfo))
                        {
                            Console.WriteLine($"ℹ️  {caps.AdditionalInfo}");
                        }
                        Console.WriteLine();
                    }

                    DisplayManager.ShowInputState(state, caps, getDebugMode());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n❌ Error: {ex.Message}{new string(' ', 50)}");
                    break;
                }
            }

            if (!hasActiveSources)
            {
                Thread.Sleep(100);
            }
        }

        // Show quit message
        if (isKeyboardMode)
        {
            Console.WriteLine("\n\n✅ Stopped monitoring. Press any key to continue...");
            Console.Read();
        }
        else
        {
            Console.WriteLine("\n\n✅ Stopped monitoring. Press any key to continue...");
            Console.Read();
        }
    }
}