using HidSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HIDDeviceMonitor;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== HID Device Monitor ===");
        Console.WriteLine("Focus: Steering Wheels and Pedals\n");

        // Parse command-line arguments
        bool showHelp = args.Any(a => a == "-h" || a == "--help");
        bool showMenu = args.Any(a => a == "-m" || a == "--menu");
        bool useKeyboard = args.Any(a => a.ToLower() == "keyboard" || a.ToLower() == "--keyboard");
        bool enableDamping = args.Any(a => a.ToLower() == "--damping");
        bool enableWebSocket = args.Any(a => a.ToLower() == "websocket" || a.ToLower() == "--websocket");
        int webSocketPort = 8080;

        // Parse WebSocket port if specified
        var portArg = args.FirstOrDefault(a => a.StartsWith("--port="));
        if (portArg != null && int.TryParse(portArg.Split('=')[1], out int port))
        {
            webSocketPort = port;
        }

        // Start WebSocket server if requested
        WebSocketServer? webSocketServer = null;
        if (enableWebSocket)
        {
            webSocketServer = StartWebSocketServer(webSocketPort);
            if (webSocketServer == null)
            {
                Console.WriteLine("❌ Failed to start WebSocket server. Continuing without WebSocket support.");
            }
        }

        // Handle help flag
        if (showHelp)
        {
            ShowHelp();
            return;
        }

        // Handle menu flag or keyboard mode
        if (showMenu || useKeyboard)
        {
            if (useKeyboard)
            {
                RunKeyboardMode(enableDamping, webSocketServer, webSocketPort);
                return;
            }

            RunHidDeviceMode(webSocketServer, webSocketPort);
            return;
        }

        // Default behavior: auto-connect to Simagic device
        RunAutoConnectMode(webSocketServer, webSocketPort);
    }

    static void ShowHelp()
    {
        Console.WriteLine("HID Device Monitor - Steering Wheel and Pedal Controller");
        Console.WriteLine();
        Console.WriteLine("USAGE:");
        Console.WriteLine("  HIDDeviceMonitor [OPTIONS]");
        Console.WriteLine();
        Console.WriteLine("OPTIONS:");
        Console.WriteLine("  -h, --help           Show this help message");
        Console.WriteLine("  -m, --menu           Show interactive device selection menu");
        Console.WriteLine("  --keyboard           Use keyboard simulation mode");
        Console.WriteLine("  --damping            Enable input damping (keyboard mode only)");
        Console.WriteLine("  --websocket          Enable WebSocket server for real-time data");
        Console.WriteLine("  --port=PORT          Set WebSocket server port (default: 8080)");
        Console.WriteLine();
        Console.WriteLine("DEFAULT BEHAVIOR:");
        Console.WriteLine("  When no flags are provided, the program automatically connects to");
        Console.WriteLine("  the first available Simagic steering wheel device and starts monitoring.");
        Console.WriteLine();
        Console.WriteLine("EXAMPLES:");
        Console.WriteLine("  HIDDeviceMonitor                    # Auto-connect to Simagic device");
        Console.WriteLine("  HIDDeviceMonitor -m                 # Show device selection menu");
        Console.WriteLine("  HIDDeviceMonitor --keyboard         # Use keyboard simulation");
        Console.WriteLine("  HIDDeviceMonitor --websocket --port=3000  # Start WebSocket server on port 3000");
        Console.WriteLine();
        Console.WriteLine("WEBSOCKET SERVER:");
        Console.WriteLine("  When enabled, serves static files at http://localhost:PORT/");
        Console.WriteLine("  and provides real-time data via WebSocket at ws://localhost:PORT/");
        Console.WriteLine("  Status API available at http://localhost:PORT/api/status");
    }

    static void RunAutoConnectMode(WebSocketServer? webSocketServer, int webSocketPort)
    {
        Console.WriteLine("🔍 Auto-connecting to Simagic steering wheel...");

        var deviceList = DeviceList.Local;
        var devices = deviceList.GetHidDevices().ToArray();

        MonitorAllDevices(devices, webSocketServer);

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

    static WebSocketServer? StartWebSocketServer(int preferredPort)
    {
        int[] portsToTry = { preferredPort, 3000, 4000, 5000, 6000, 7000, 8000, 9000 };

        foreach (int port in portsToTry)
        {
            try
            {
                Console.WriteLine($"Starting WebSocket server on port {port}...");
                var server = new WebSocketServer(port);
                server.StartAsync().Wait();
                Console.WriteLine("✅ WebSocket server started successfully");
                return server;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Port {port} failed: {ex.Message}");
                if (port == portsToTry.Last())
                {
                    Console.WriteLine("❌ All ports failed. WebSocket server not available.");
                    return null;
                }
            }
        }

        return null;
    }

    static void RunKeyboardMode(bool enableDamping, WebSocketServer? webSocketServer, int webSocketPort)
    {
        Console.WriteLine("🎹 Keyboard Simulation Mode");
        Console.WriteLine($"Damping: {(enableDamping ? "Enabled" : "Disabled")}\n");

        using var keyboardSource = new KeyboardInputSource(enableDamping);

        if (!keyboardSource.InitializeAsync().Result)
        {
            Console.WriteLine("❌ Failed to initialize keyboard input");
            return;
        }

        MonitorInputSource(keyboardSource, true, webSocketServer, webSocketPort);
    }

    static void RunHidDeviceMode(WebSocketServer? webSocketServer, int webSocketPort)
    {
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
                RunKeyboardMode(enableDamping: true, webSocketServer, webSocketPort);
                continue; // Go back to menu instead of exiting
            }
            else if (input == "a")
            {
                MonitorAllDevices(devices, webSocketServer);
            }
            else if (int.TryParse(input, out int index) && index >= 0 && index < devices.Length)
            {
                using var hidSource = new HidInputSource(devices[index], index);
                if (hidSource.InitializeAsync().Result)
                {
                    MonitorInputSource(hidSource, false, webSocketServer, webSocketPort);
                }
            }
            else
            {
                Console.WriteLine("Invalid input. Please try again.");
            }
        }

        Console.WriteLine("\nExiting...");
    }

    static void MonitorInputSource(InputSource source, bool isKeyboardMode, WebSocketServer? webSocketServer = null, int webSocketPort = 8080)
    {
        var caps = source.GetCapabilities();

        // Show WebSocket status before clearing screen
        if (webSocketServer != null && webSocketServer.IsRunning)
        {
            Console.WriteLine($"🌐 WebSocket server is running on ws://localhost:{webSocketPort}/");
        }

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
        MonitorSources(new[] { (source, caps) }, cancellationSource, webSocketServer, () => debugMode, isKeyboardMode);
    }

    static void MonitorAllDevices(HidDevice[] devices, WebSocketServer? webSocketServer = null)
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
                Console.ReadKey();
                return;
            }

            Console.WriteLine($"\n📡 Monitoring {sources.Count} device(s)...");
            Console.WriteLine(new string('─', 80));

            var cancellationSource = new CancellationTokenSource();
            var keyCheckTask = Task.Run(() =>
            {
                Console.ReadKey(true);
                cancellationSource.Cancel();
            });

            // Use common monitoring loop for all devices
            MonitorSources(sources.ToArray(), cancellationSource, webSocketServer, () => false, false);
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

    static void MonitorSources((InputSource source, DeviceCapabilities caps)[] sourcePairs, CancellationTokenSource cancellationSource, WebSocketServer? webSocketServer, Func<bool> getDebugMode, bool isKeyboardMode)
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

                    // Broadcast to WebSocket clients if server is available
                    if (webSocketServer != null)
                    {
                        webSocketServer.BroadcastInputDataAsync(state, caps).Wait();
                    }

                    // Throttle updates to avoid flickering
                    var now = DateTime.Now;
                    if ((now - lastUpdateTime).TotalMilliseconds < 100)
                    {
                        Thread.Sleep(20);
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
            Console.ReadKey();
        }
        else
        {
            Console.WriteLine("\n\n✅ Stopped monitoring. Press any key to continue...");
            Console.ReadKey();
        }
    }
}
