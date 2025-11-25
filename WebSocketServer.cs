using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace HIDDeviceMonitor;

/// <summary>
/// Interface for data serialization strategies
/// Allows swapping between JSON, binary, or other formats
/// </summary>
public interface IDataSerializer
{
    string ContentType { get; }
    byte[] Serialize(object data);
}

/// <summary>
/// JSON implementation of data serializer
/// </summary>
public class JsonDataSerializer : IDataSerializer
{
    public string ContentType => "application/json";

    public byte[] Serialize(object data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = false // Compact JSON for WebSocket
        });
        return Encoding.UTF8.GetBytes(json);
    }
}

/// <summary>
/// WebSocket server for broadcasting HID input data
/// </summary>
public class WebSocketServer : IDisposable
{
    private readonly HttpListener _httpListener;
    private readonly List<WebSocket> _connectedClients = new();
    private readonly IDataSerializer _dataSerializer;
    private readonly int _port;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _serverTask;
    private bool _isRunning = false;

    public bool IsRunning => _isRunning;

    public WebSocketServer(int port = 8080, IDataSerializer? dataSerializer = null)
    {
        _port = port;
        _dataSerializer = dataSerializer ?? new JsonDataSerializer();
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"http://localhost:{port}/");
    }

    /// <summary>
    /// Start the WebSocket server
    /// </summary>
    public Task StartAsync()
    {
        if (_isRunning) return Task.CompletedTask;

        try
        {
            _httpListener.Start();
            _isRunning = true;
            Console.WriteLine($"🌐 WebSocket server started on ws://localhost:{_port}/");
            Console.WriteLine($"📄 Static file server started on http://localhost:{_port}/");
            Console.WriteLine($"📊 Status API available at http://localhost:{_port}/api/status");

            _serverTask = Task.Run(() => ServerLoop(_cancellationTokenSource.Token));
            return _serverTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to start WebSocket server: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Stop the WebSocket server
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning) return;

        _cancellationTokenSource.Cancel();

        // Close all client connections
        foreach (var client in _connectedClients.ToArray())
        {
            try
            {
                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", CancellationToken.None);
            }
            catch { }
        }

        _connectedClients.Clear();
        _httpListener.Stop();
        _isRunning = false;

        Console.WriteLine("🛑 WebSocket server stopped");
    }

    /// <summary>
    /// Broadcast input data to all connected clients
    /// </summary>
    public async Task BroadcastInputDataAsync(InputState inputState, DeviceCapabilities capabilities)
    {
        WebSocket[] clients;
        lock (_connectedClients)
        {
            if (!_isRunning || _connectedClients.Count == 0)
            {
                return;
            }
            clients = _connectedClients.ToArray();
        }

        // Convert InputState to the expected JSON format
        var data = ConvertInputStateToJson(inputState, capabilities);

        var serializedData = _dataSerializer.Serialize(data);

        // Send to all connected clients
        var disconnectedClients = new List<WebSocket>();

        foreach (var client in clients)
        {
            try
            {
                if (client.State == WebSocketState.Open)
                {
                    await client.SendAsync(serializedData, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    disconnectedClients.Add(client);
                }
            }
            catch
            {
                disconnectedClients.Add(client);
            }
        }

        // Remove disconnected clients
        if (disconnectedClients.Count > 0)
        {
            lock (_connectedClients)
            {
                foreach (var client in disconnectedClients)
                {
                    _connectedClients.Remove(client);
                }
            }
        }
    }

    /// <summary>
    /// Get server status information
    /// </summary>
    public object GetServerStatus()
    {
        return new
        {
            server = new
            {
                status = _isRunning ? "running" : "stopped",
                websocket_url = $"ws://localhost:{_port}/",
                http_url = $"http://localhost:{_port}/",
                uptime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                connected_clients = _connectedClients.Count
            },
            settings = new
            {
                data_format = _dataSerializer.ContentType,
                port = _port
            }
        };
    }

    private async Task ServerLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();

                if (context.Request.IsWebSocketRequest)
                {
                    // Handle WebSocket connection
                    var wsContext = await context.AcceptWebSocketAsync(null);
                    var webSocket = wsContext.WebSocket;

                    lock (_connectedClients)
                    {
                        _connectedClients.Add(webSocket);
                    }

                    Console.WriteLine($"🔗 WebSocket client connected. Total clients: {_connectedClients.Count}");

                    // Handle client in background (fire and forget)
                    _ = HandleWebSocketClientAsync(webSocket, cancellationToken);
                }
                else if (context.Request.HttpMethod == "GET")
                {
                    // Handle HTTP requests
                    await HandleHttpRequestAsync(context);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine($"⚠️ WebSocket server error: {ex.Message}");
                await Task.Delay(1000, cancellationToken); // Prevent tight loop on errors
            }
        }
    }

    private async Task HandleWebSocketClientAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new byte[1024];

            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                // WebSocket clients are receive-only for now
                // Could be extended to receive commands from clients
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ WebSocket client error: {ex.Message}");
        }
        finally
        {
            lock (_connectedClients)
            {
                _connectedClients.Remove(webSocket);
            }

            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
                }
            }
            catch { }

            Console.WriteLine($"🔌 WebSocket client disconnected. Total clients: {_connectedClients.Count}");
        }
    }

    private async Task HandleHttpRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            // Check if it's the status API
            if (request.Url?.LocalPath == "/api/status")
            {
                await ServeStatusApiAsync(context);
            }
            else
            {
                await ServeStaticFileAsync(context);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ HTTP request error: {ex.Message}");
            response.StatusCode = 500;
            response.Close();
        }
    }

    private async Task ServeStatusApiAsync(HttpListenerContext context)
    {
        var response = context.Response;
        response.ContentType = _dataSerializer.ContentType;
        response.Headers.Add("Access-Control-Allow-Origin", "*");

        var statusData = GetServerStatus();
        var data = _dataSerializer.Serialize(statusData);

        await response.OutputStream.WriteAsync(data);
        response.Close();
    }

    private async Task ServeStaticFileAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        string filePath = request.Url?.LocalPath?.TrimStart('/') ?? "index.html";
        if (string.IsNullOrEmpty(filePath)) filePath = "index.html";

        // Look for files in client-example directory
        string fullPath = Path.Combine("client-example", filePath);

        if (!File.Exists(fullPath))
        {
            response.StatusCode = 404;
            response.Close();
            return;
        }

        string contentType = GetContentType(fullPath);
        response.ContentType = contentType;

        using (var fileStream = File.OpenRead(fullPath))
        {
            await fileStream.CopyToAsync(response.OutputStream);
        }
        response.Close();
    }

    private static string GetContentType(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLower();
        return ext switch
        {
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            _ => "text/plain"
        };
    }

    // Debug flag to print axis names only once
    private static bool _debugAxisNamesPrinted = false;

    /// <summary>
    /// Convert InputState to the JSON format expected by the client
    /// </summary>
    private static object ConvertInputStateToJson(InputState inputState, DeviceCapabilities capabilities)
    {
        int steering = 32767; // Default center
        int throttle = 0;
        int brake = 0;

        if (capabilities.DeviceType == DeviceType.Gamepad)
        {
            // Gamepad mapping (Option 2):
            // Steering: Left Stick X (Axis 0)
            // Throttle: Right Stick Y (Axis 3)
            // Brake: Left Stick Y (Axis 1) - inverted

            // Helper function to get axis capability by index (0-based)
            AxisCapability? GetAxisCapability(int index)
            {
                return capabilities.Axes.FirstOrDefault(a => a.Index == index);
            }

            // Steering: Left Stick X (axis index 0)
            var steeringAxis = GetAxisCapability(0);
            if (steeringAxis != null && inputState.Axes.TryGetValue(steeringAxis.Name, out AxisState? steeringValue))
            {
                int rawValue = steeringValue.Value;
                double center = (steeringAxis.LogicalMax + steeringAxis.LogicalMin) / 2.0;
                double range = steeringAxis.LogicalMax - steeringAxis.LogicalMin;
                double normalized = (rawValue - center) / (range / 2.0); // -1 to 1
                steering = (int)((normalized + 1) * 32767.5); // Map to 0-65535 centered at 32767
            }

            // Throttle: Right Stick Y (axis index 3)
            var throttleAxis = GetAxisCapability(3);
            if (throttleAxis != null && inputState.Axes.TryGetValue(throttleAxis.Name, out AxisState? throttleValue))
            {
                int rawValue = throttleValue.Value;
                double center = (throttleAxis.LogicalMax + throttleAxis.LogicalMin) / 2.0;
                double normalized = Math.Max(0, (center - rawValue) / (center - throttleAxis.LogicalMin)); // 0 to 1 when pushed up
                throttle = (int)(normalized * 65535);
            }

            // Brake: Left Stick Y (axis index 1) - inverted
            var brakeAxis = GetAxisCapability(1);
            if (brakeAxis != null && inputState.Axes.TryGetValue(brakeAxis.Name, out AxisState? brakeValue))
            {
                int rawValue = brakeAxis.LogicalMax - brakeValue.Value;
                double center = (brakeAxis.LogicalMax + brakeAxis.LogicalMin) / 2.0;
                double normalized = Math.Max(0, (center - rawValue) / (center - brakeAxis.LogicalMin)); // 0 to 1 when pushed up
                brake = (int)(normalized * 65535);
            }
        }
        else
        {
            // Original racing wheel mapping
            // Extract steering (X-axis) - try different possible names
            foreach (var kvp in inputState.Axes)
            {
                if (kvp.Key.Contains("X-Axis") || kvp.Key.Contains("Steering"))
                {
                    steering = kvp.Value.Value;
                    break;
                }
            }

            // Extract throttle (Y-axis)
            foreach (var kvp in inputState.Axes)
            {
                if (kvp.Key.Contains("Y-Axis") || kvp.Key.Contains("Throttle"))
                {
                    throttle = kvp.Value.Value;
                    break;
                }
            }

            // Extract brake (Z-axis)
            foreach (var kvp in inputState.Axes)
            {
                if (kvp.Key.Contains("Z-Axis") || kvp.Key.Contains("Brake"))
                {
                    brake = kvp.Value.Value;
                    break;
                }
            }
        }

        // Extract buttons (first 10 buttons)
        bool[] buttons = new bool[10];
        for (int i = 0; i < Math.Min(10, capabilities.TotalButtons); i++)
        {
            buttons[i] = inputState.Buttons.ContainsKey(i) && inputState.Buttons[i];
        }

        return new
        {
            steering = steering,
            throttle = throttle,
            brake = brake,
            wheel_buttons = buttons
        };
    }

    public void Dispose()
    {
        StopAsync().Wait();
        _cancellationTokenSource.Dispose();
    }
}