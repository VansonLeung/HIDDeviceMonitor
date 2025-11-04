using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace HIDDeviceMonitor;

/// <summary>
/// Keyboard simulation input source (for testing without physical device)
/// Simulates a racing wheel with keyboard controls
/// </summary>
public class KeyboardInputSource : InputSource
{
    private int _currentThrottle = 0;
    private int _currentBrake = 0;
    private int _currentSteering = 32767; // Center position
    private readonly Dictionary<int, bool> _buttonStates = new();
    private bool _isRunning = false;
    private readonly bool _disableDamping;

    public override string Name => "Keyboard Simulator";
    public override string DeviceType => "Virtual Device";
    public override bool IsConnected => _isRunning;

    public KeyboardInputSource(bool disableDamping)
    {
        _disableDamping = disableDamping;
        
        // Initialize button states
        for (int i = 0; i < 10; i++)
        {
            _buttonStates[i] = false;
        }
    }

    public override async Task<bool> InitializeAsync()
    {
        _isRunning = true;
        Console.WriteLine("✅ Keyboard simulator initialized");
        Console.WriteLine("\n📝 Keyboard Controls:");
        Console.WriteLine("   W     - Throttle Up");
        Console.WriteLine("   S     - Brake");
        Console.WriteLine("   A/D   - Steer Left/Right");
        Console.WriteLine("   Space - Brake (alternative)");
        Console.WriteLine("   1-9   - Buttons 1-9");
        Console.WriteLine("   Q     - Quit monitoring");
        Console.WriteLine($"   Damping: {(_disableDamping ? "Disabled" : "Enabled")}\n");
        
        return await Task.FromResult(true);
    }

    public override DeviceCapabilities GetCapabilities()
    {
        var caps = new DeviceCapabilities
        {
            TotalButtons = 10,
            TotalAxes = 3,
            AdditionalInfo = "Keyboard Simulation Mode"
        };

        caps.Axes.Add(new AxisCapability
        {
            Index = 0,
            Name = "X-Axis (Steering)",
            UsageName = "X-Axis",
            LogicalMin = 0,
            LogicalMax = 65535,
            ByteIndex = 1,
            BitSize = 16
        });

        caps.Axes.Add(new AxisCapability
        {
            Index = 1,
            Name = "Y-Axis (Throttle)",
            UsageName = "Y-Axis",
            LogicalMin = 0,
            LogicalMax = 65535,
            ByteIndex = 3,
            BitSize = 16
        });

        caps.Axes.Add(new AxisCapability
        {
            Index = 2,
            Name = "Z-Axis (Brake)",
            UsageName = "Z-Axis",
            LogicalMin = 0,
            LogicalMax = 65535,
            ByteIndex = 5,
            BitSize = 16
        });

        for (int i = 0; i < 10; i++)
        {
            caps.Buttons.Add(new ButtonCapability
            {
                Index = i,
                Name = $"Button {i + 1}",
                ByteIndex = 7 + (i / 8),
                BitIndex = i % 8
            });
        }

        return caps;
    }

    public override void Poll()
    {
        // Update values based on keyboard input
        UpdateFromKeyboard();
    }

    public override InputState GetCurrentState()
    {
        var state = new InputState
        {
            Timestamp = DateTime.Now
        };

        // Add axes
        state.Axes["X-Axis (Steering)"] = new AxisState
        {
            Name = "X-Axis (Steering)",
            Value = _currentSteering,
            MinValue = 0,
            MaxValue = 65535,
            ByteInfo = "[1-2]"
        };

        state.Axes["Y-Axis (Throttle)"] = new AxisState
        {
            Name = "Y-Axis (Throttle)",
            Value = _currentThrottle,
            MinValue = 0,
            MaxValue = 65535,
            ByteInfo = "[3-4]"
        };

        state.Axes["Z-Axis (Brake)"] = new AxisState
        {
            Name = "Z-Axis (Brake)",
            Value = _currentBrake,
            MinValue = 0,
            MaxValue = 65535,
            ByteInfo = "[5-6]"
        };

        // Add buttons
        foreach (var kvp in _buttonStates)
        {
            state.Buttons[kvp.Key] = kvp.Value;
        }

        // Generate fake raw data
        var rawData = new List<byte>
        {
            0x01, // Report ID
            (byte)(_currentSteering & 0xFF),
            (byte)((_currentSteering >> 8) & 0xFF),
            (byte)(_currentThrottle & 0xFF),
            (byte)((_currentThrottle >> 8) & 0xFF),
            (byte)(_currentBrake & 0xFF),
            (byte)((_currentBrake >> 8) & 0xFF)
        };

        // Add button bytes
        byte buttonByte1 = 0;
        byte buttonByte2 = 0;
        for (int i = 0; i < 8; i++)
        {
            if (_buttonStates.ContainsKey(i) && _buttonStates[i])
                buttonByte1 |= (byte)(1 << i);
        }
        for (int i = 8; i < 10; i++)
        {
            if (_buttonStates.ContainsKey(i) && _buttonStates[i])
                buttonByte2 |= (byte)(1 << (i - 8));
        }
        rawData.Add(buttonByte1);
        rawData.Add(buttonByte2);

        state.RawData = rawData.ToArray();

        return state;
    }

    private void UpdateFromKeyboard()
    {
        if (!Console.KeyAvailable)
        {
            // Apply damping when no keys pressed
            if (!_disableDamping)
            {
                ApplyDamping();
            }
            return;
        }

        var keyInfo = Console.ReadKey(true);
        var key = keyInfo.Key;
        var keyChar = keyInfo.KeyChar;

        int increment = !_disableDamping ? 2000 : 65535;
        int decrement = !_disableDamping ? increment / 3 : 65535;

        switch (key)
        {
            case ConsoleKey.W:
                _currentThrottle = Math.Min(65535, _currentThrottle + increment);
                break;
            case ConsoleKey.S:
                _currentBrake = Math.Min(65535, _currentBrake + increment);
                break;
            case ConsoleKey.A:
                _currentSteering = Math.Max(0, _currentSteering - (!_disableDamping ? increment : 10000));
                break;
            case ConsoleKey.D:
                _currentSteering = Math.Min(65535, _currentSteering + (!_disableDamping ? increment : 10000));
                break;
            case ConsoleKey.Spacebar:
                _currentBrake = Math.Min(65535, _currentBrake + increment);
                break;
            case ConsoleKey.D1:
            case ConsoleKey.D2:
            case ConsoleKey.D3:
            case ConsoleKey.D4:
            case ConsoleKey.D5:
            case ConsoleKey.D6:
            case ConsoleKey.D7:
            case ConsoleKey.D8:
            case ConsoleKey.D9:
                int buttonNum = key - ConsoleKey.D1;
                _buttonStates[buttonNum] = !_buttonStates[buttonNum]; // Toggle
                break;
            case ConsoleKey.Q:
                _isRunning = false;
                break;
        }
    }

    private void ApplyDamping()
    {
        int decrement = 667; // Slower release for smooth damping

        // Release throttle
        if (_currentThrottle > 0)
        {
            _currentThrottle = Math.Max(0, _currentThrottle - decrement);
        }

        // Release brake
        if (_currentBrake > 0)
        {
            _currentBrake = Math.Max(0, _currentBrake - decrement);
        }

        // Center steering
        if (_currentSteering < 32767)
        {
            _currentSteering = Math.Min(32767, _currentSteering + decrement);
        }
        else if (_currentSteering > 32767)
        {
            _currentSteering = Math.Max(32767, _currentSteering - decrement);
        }
    }

    public override void Dispose()
    {
        _isRunning = false;
    }
}
