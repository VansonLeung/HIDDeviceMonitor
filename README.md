# HID Device Monitor

A .NET 9.0 console application for discovering, classifying, and monitoring HID (Human Interface Device) input devices. Specializes in steering wheels, pedals, and game controllers with real-time WebSocket broadcasting capabilities.

## Features

- **Device Discovery & Classification**: Automatically discovers and classifies HID devices using HID usage page/ID specifications
- **Real-time Monitoring**: Monitor single or multiple HID devices with live input data
- **Gamepad Racing Support**: Maps gamepad axes to steering/throttle/brake controls for racing applications
- **WebSocket Server**: Broadcasts input data to web clients in real-time JSON format
- **Keyboard Simulation**: Test applications without physical hardware using keyboard controls
- **HID Descriptor Parsing**: Extracts device capabilities from HID report descriptors
- **Modular Architecture**: Clean separation of concerns for easy extension
- **Cross-platform**: Works on macOS, Windows, and Linux

## Requirements

- .NET 9.0 SDK
- macOS, Windows, or Linux
- **macOS Note**: Grant Input Monitoring permissions to Terminal/IDE in System Settings > Privacy & Security

## Installation & Usage

### Building and Running

```bash
cd HIDDeviceMonitor
dotnet build
dotnet run [mode] [options]
```

### Command Line Options

- **Auto-connect Mode** (default): `dotnet run` - Automatically connects to first available Simagic steering wheel
- **Device Menu**: `dotnet run -m` or `dotnet run --menu` - Interactive device selection menu
- **Keyboard Simulation**: `dotnet run --keyboard` or `dotnet run keyboard` - Use keyboard controls
- **WebSocket Control**: `dotnet run --disable-websocket` - Disable WebSocket server
- **Damping Control**: `dotnet run --disable-damping` - Disable input damping in keyboard mode
- **Port Setting**: `dotnet run --port=8080` - Set WebSocket server port
- **Help**: `dotnet run -h` or `dotnet run --help` - Show help information

### Examples

```bash
# Auto-connect to first Simagic device (default)
dotnet run

# Show device selection menu
dotnet run --menu

# Keyboard simulation without damping
dotnet run --keyboard --disable-damping

# WebSocket server on custom port
dotnet run --port=3000

# Keyboard mode without WebSocket
dotnet run keyboard --disable-websocket
```

### Quick Start Script

```bash
./run.sh
```

### Main Menu Options

When using `--menu` mode, you'll see an interactive menu with these options:

1. **Enter device number (0-N)**: Monitor a specific device
2. **'k'**: Switch to keyboard simulation mode
3. **'a'**: Monitor all devices simultaneously
4. **'r'**: Refresh device list
5. **'q'**: Quit application

**Default Behavior**: Without `--menu`, the application automatically connects to the first available Simagic steering wheel device.

### Keyboard Simulation Controls

| Key | Action |
|-----|--------|
| A/D | Steer left/right |
| W | Throttle up |
| S | Brake |
| Space | Brake (alternative) |
| 1-9 | Toggle buttons 1-9 |
| Q | Quit monitoring |

**Damping**: Enabled by default in keyboard mode. Use `--disable-damping` to disable gradual release and centering.

## Device Classification

The application classifies HID devices using their HID usage page and usage ID from the device descriptor:

### Supported Device Types

- **Steering Wheels** (Usage Page 0x02, Usage 0x02): Automobile Simulation Devices
- **Gamepads** (Usage Page 0x01, Usage 0x05): Standard game controllers
- **Joysticks** (Usage Page 0x01, Usage 0x04): Flight and general joysticks

### Gamepad Racing Mapping

For gamepads classified as `DeviceType.Gamepad`, the application provides special mapping to racing controls:

- **Steering**: Left stick X-axis (centered, -1 to +1 range)
- **Throttle**: Right stick Y-axis inverted (push up increases throttle)
- **Brake**: Left stick Y-axis inverted (push up increases brake)

This allows standard gamepads to be used in racing applications without hardware modification.

## WebSocket Server

Broadcasts real-time HID input data to connected web clients.

### Server Endpoints

- **WebSocket**: `ws://localhost:8080/` - Real-time input data
- **HTTP Static Files**: `http://localhost:8080/` - Serves client files
- **Status API**: `http://localhost:8080/api/status` - Server status

### Message Format

```json
{
  "steering": 32767,
  "throttle": 0,
  "brake": 0,
  "wheel_buttons": [false, false, false, false, false, false, false, false, false, false]
}
```

**Field Descriptions:**
- `steering`: 0-65535 (centered at 32767)
- `throttle`: 0-65535 (0 = no throttle, 65535 = full throttle)
- `brake`: 0-65535 (0 = no brake, 65535 = full brake)
- `wheel_buttons`: Array of 10 button states

### Included Web Client

The `client-example/index.html` provides a complete dashboard showing:
- Real-time steering, throttle, and brake gauges
- Button state indicators
- Server status information
- Modern, responsive UI

## Architecture

### Core Components

- **`DeviceClassifier.cs`**: HID device classification using usage specifications
- **`InputSource.cs`**: Abstract base for input sources (HID, Keyboard)
- **`WebSocketServer.cs`**: Real-time data broadcasting with gamepad mapping
- **`DisplayManager.cs`**: Console UI rendering
- **`Program.cs`**: Main application logic

### Key Classes

```csharp
class DeviceCapabilities
{
    DeviceType DeviceType;
    int TotalButtons;
    int TotalAxes;
    List<AxisCapability> Axes;
    List<ButtonCapability> Buttons;
}

class InputState
{
    Dictionary<string, AxisState> Axes;
    Dictionary<int, bool> Buttons;
    byte[] RawData;
}
```

## Understanding Device Output

### Device Classification Display

```
📊 Device: Logitech Gamepad
   Type: Gamepad (Usage: 0x01/0x05)
   VID: 0x046D, PID: 0xC216
   Buttons: 12, Axes: 6
```

### Real-time Monitoring Display

```
🕐 Last Update: 14:23:15.123 | Data Size: 8 bytes

🔘 BUTTONS:
   Pressed: B1, B4
   Status:  B1  B2  B3  B4  B5  ...
   Total: 12 buttons

🎚️  AXES:
   65584 1 (X)     :    128 (0..255)    bytes[1]
   65584 2 (Y)     :    127 (0..255)    bytes[2]
   ...

📊 RAW DATA:
   [1]=0x80 [2]=0x7F [3]=0x83 ...
```

### WebSocket Output (Gamepad Racing Mode)

```
{
  "steering": 32767,
  "throttle": 16384,
  "brake": 0,
  "wheel_buttons": [true, false, false, ...]
}
```

## Troubleshooting

### macOS Permission Issues
```
❌ Failed to open device
```
**Solution**: System Settings > Privacy & Security > Input Monitoring > Add Terminal/IDE

### No Devices Found
- Check USB connections
- Try `r` to refresh device list
- Verify device compatibility

### Classification Issues
- Ensure device uses standard HID usage pages/IDs
- Check raw data for axis/button changes
- Some devices may require custom mapping

### WebSocket Connection Issues
- Verify port 8080 is available
- Check firewall settings
- Ensure client connects to correct WebSocket URL

## Extending the Application

### Adding New Device Types

1. Update `DeviceClassifier.cs` to handle new usage pages/IDs
2. Add axis mapping logic in `WebSocketServer.cs`
3. Test with target devices

### Custom Input Sources

```csharp
public class CustomInputSource : InputSource
{
    public override string Name => "Custom Device";
    // Implement required methods
}
```

### Custom WebSocket Serialization

```csharp
public class CustomSerializer : IDataSerializer
{
    public string ContentType => "application/custom";
    public byte[] Serialize(object data) { /* implement */ }
}
```

## Technical Details

- **HID Library**: HidSharp for cross-platform HID access
- **Classification**: Uses HID usage page/ID from device descriptors
- **Data Parsing**: Handles bit-packed axes and variable ranges
- **WebSocket**: JSON serialization with configurable format
- **Performance**: 50ms update intervals, 100ms read timeouts

## License

This is a testing and development tool. Use freely for personal and commercial projects.
