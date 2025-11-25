using HidSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HIDDeviceMonitor;

/// <summary>
/// Configuration class to reduce parameter passing
/// </summary>
class ProgramConfig
{
    public bool DisableWebSocket { get; set; }
    public int WebSocketPort { get; set; } = 8080;
    public WebSocketServer? WebSocketServer { get; set; }
    public bool DisableDamping { get; set; }
    public int KeyboardIncrement { get; set; } = 8000;
    public int KeyboardDecrement { get; set; } = 1000;
}