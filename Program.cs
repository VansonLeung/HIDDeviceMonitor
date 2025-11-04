using HidSharp;
using System;
using System.Linq;

namespace HIDDeviceMonitor;

class Program
{
    static ProgramConfig _config = new();

    static void Main(string[] args)
    {
        Console.WriteLine("=== HID Device Monitor ===");
        Console.WriteLine("Focus: Steering Wheels and Pedals\n");

        ParseArguments(args);
        InitializeWebSocket();

        // Handle help flag
        if (args.Any(a => a == "-h" || a == "--help"))
        {
            ShowHelp();
            return;
        }

        // Set config for mode runner
        ModeRunner.SetConfig(_config);

        // Set input data callback for side effects (WebSocket broadcasting)
        ModeRunner.SetInputDataCallback((state, caps) =>
        {
            if (_config.WebSocketServer != null)
            {
                Console.WriteLine("Broadcasting input data to WebSocket clients...");
                _config.WebSocketServer.BroadcastInputDataAsync(state, caps).Wait();
            }
        });

        // Route to appropriate mode
        if (args.Any(a => a.ToLower() == "keyboard" || a.ToLower() == "--keyboard"))
        {
            ModeRunner.RunKeyboardMode();
        }
        else if (args.Any(a => a == "-m" || a == "--menu"))
        {
            ModeRunner.RunHidDeviceMode();
        }
        else
        {
            ModeRunner.RunAutoConnectMode();
        }
    }

    static void ParseArguments(string[] args)
    {
        _config.DisableWebSocket = args.Any(a => a.ToLower() == "--disable-websocket");
        _config.DisableDamping = args.Any(a => a.ToLower() == "--disable-damping");

        var portArg = args.FirstOrDefault(a => a.StartsWith("--port="));
        if (portArg != null && int.TryParse(portArg.Split('=')[1], out int port))
        {
            _config.WebSocketPort = port;
        }
    }

    static void InitializeWebSocket()
    {
        if (_config.DisableWebSocket)
        {
            Console.WriteLine("ℹ️  WebSocket server disabled via command-line flag.");
            return;
        }

        // Start WebSocket server asynchronously without blocking
        Task.Run(() =>
        {
            var server = StartWebSocketServerAsync(_config.WebSocketPort);
            if (server == null)
            {
                Console.WriteLine("❌ Failed to start WebSocket server. Continuing without WebSocket support.");
            }
        });

        Console.WriteLine($"🌐 InitializeWebSocket completed (server starting in background).");
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

    static WebSocketServer? StartWebSocketServerAsync(int preferredPort)
    {
        int[] portsToTry = { preferredPort, 3000, 4000, 5000, 6000, 7000, 8000, 9000 };

        foreach (int port in portsToTry)
        {
            try
            {
                Console.WriteLine($"Starting WebSocket server on port {port}...");
                var server = new WebSocketServer(port);
                _config.WebSocketServer = server; // Store the server instance immediately
                _ = server.StartAsync(); // Start asynchronously without waiting
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
}
