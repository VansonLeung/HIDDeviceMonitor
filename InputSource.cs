using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HIDDeviceMonitor;

/// <summary>
/// Abstract base class for input sources (HID devices or keyboard simulation)
/// </summary>
public abstract class InputSource : IDisposable
{
    public abstract string Name { get; }
    public abstract string DeviceType { get; }
    public abstract bool IsConnected { get; }
    
    /// <summary>
    /// Initialize and acquire the input source
    /// </summary>
    public abstract Task<bool> InitializeAsync();
    
    /// <summary>
    /// Get current input state
    /// </summary>
    public abstract InputState GetCurrentState();
    
    /// <summary>
    /// Poll for input updates (if needed)
    /// </summary>
    public abstract void Poll();
    
    /// <summary>
    /// Get device information for display
    /// </summary>
    public abstract DeviceCapabilities GetCapabilities();
    
    public abstract void Dispose();
}

/// <summary>
/// Current state of all inputs
/// </summary>
public class InputState
{
    public Dictionary<string, AxisState> Axes { get; set; } = new();
    public Dictionary<int, bool> Buttons { get; set; } = new();
    public byte[] RawData { get; set; } = Array.Empty<byte>();
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>
/// State of a single axis
/// </summary>
public class AxisState
{
    public string Name { get; set; } = "";
    public int Value { get; set; }
    public int MinValue { get; set; }
    public int MaxValue { get; set; }
    public string ByteInfo { get; set; } = "";
}

/// <summary>
/// Device capability information
/// </summary>
public class DeviceCapabilities
{
    public int TotalButtons { get; set; }
    public int TotalAxes { get; set; }
    public List<AxisCapability> Axes { get; set; } = new();
    public List<ButtonCapability> Buttons { get; set; } = new();
    public string AdditionalInfo { get; set; } = "";
    public DeviceType DeviceType { get; set; } = DeviceType.Unknown;
    public string Manufacturer { get; set; } = "";
    public string ProductName { get; set; } = "";
    public int VendorId { get; set; }
    public int ProductId { get; set; }
    public int UsagePage { get; set; }
    public int UsageId { get; set; }
}

/// <summary>
/// HID device type classification
/// </summary>
public enum DeviceType
{
    Unknown,
    SteeringWheel,      // Racing wheel with steering axis
    PedalController,    // Dedicated pedal set (throttle, brake, clutch)
    WheelAndPedals,     // Integrated wheel + pedals
    Gamepad,           // Standard game controller
    Joystick,          // Flight stick or joystick
    Other
}

public class AxisCapability
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public string UsageName { get; set; } = "";
    public int LogicalMin { get; set; }
    public int LogicalMax { get; set; }
    public int ByteIndex { get; set; }
    public int BitOffset { get; set; }
    public int BitSize { get; set; }
}

public class ButtonCapability
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public int ByteIndex { get; set; }
    public int BitIndex { get; set; }
}
