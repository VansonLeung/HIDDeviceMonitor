# HID Device Monitor

A .NET 9.0 console application for discovering and monitoring HID (Human Interface Device) input devices, with a focus on steering wheels and pedals. Features modular architecture with keyboard simulation support for testing without physical hardware.

## Features

- **Device Discovery**: Lists all connected HID devices with details (VID, PID, manufacturer, product name)
- **HID Report Descriptor Parsing**: Automatically identifies buttons and axes from the device's HID descriptor
- **Single Device Monitoring**: Connect to and monitor input from a specific device by index
- **Multi-Device Monitoring**: Monitor all available HID devices simultaneously
- **Keyboard Simulation Mode**: Test without physical devices using keyboard controls
- **Fixed Display Mode**: Clean, non-scrolling display that updates in place
- **Real-time Input Display**: Shows:
  - Button states (which buttons are pressed, with visual grid)
  - Axis values with their logical ranges and byte positions
  - Raw byte data for debugging
- **Modular Architecture**: Clean separation of concerns for easy extension

## Requirements

- .NET 9.0 SDK
- macOS, Windows, or Linux
- **macOS Note**: You may need to grant Input Monitoring permissions to Terminal/IDE

## Usage

### Running the Application

**HID Device Mode (default):**
```bash
cd HIDDeviceMonitor
dotnet run
```

**Keyboard Simulation Mode:**
```bash
dotnet run keyboard
```

Or with damping:
```bash
dotnet run keyboard --damping
```

**With WebSocket Server:**
```bash
# HID device mode with WebSocket server
dotnet run websocket

# Keyboard mode with WebSocket server
dotnet run keyboard websocket

# Custom port
dotnet run websocket --port=3000
```

Or use the quick start script:
```bash
./run.sh
```

### Main Menu Options

1. **Enter device number (0-N)**: Monitor a specific device with detailed analysis
2. **'k'**: Switch to Keyboard simulation mode
3. **'a'**: Monitor ALL devices simultaneously
4. **'r'**: Refresh the device list (useful when plugging in new devices)
5. **'q'**: Quit the application

### Keyboard Simulation Controls

When in keyboard mode, you can simulate a racing wheel:

| Key | Action |
|-----|--------|
| W | Throttle Up |
| S | Brake |
| A/D | Steer Left/Right |
| Space | Brake (alternative) |
| 1-9 | Toggle Buttons 1-9 |
| Q | Quit monitoring |

**Damping Mode**: When enabled (`--damping` flag), throttle/brake gradually release and steering returns to center smoothly.

### Enhanced Display Format

When monitoring a device, you'll see:

```
=== Monitoring: SIMAGIC Alpha ===
Type: HID Device
ℹ️  Report ID: 1

📊 Device Capabilities:
   Buttons: 32
   Axes: 6

🔘 Button Layout (first 32):
   B1:[15].0 B2:[15].1 B3:[15].2 B4:[15].3 B5:[15].4 B6:[15].5 B7:[15].6 B8:[15].7
   B9:[16].0 B10:[16].1 ...

🎮 Detected Axes:
   X-Axis: X-Axis (Range: 0 to 65535) - Bytes[1..2]
   Y-Axis: Y-Axis (Range: 0 to 4095) - Byte[3] + 0 bits, Size: 12 bits
   ...

⌨️  Press 'd' for debug mode, any other key to stop monitoring

────────────────────────────────────────────────────────────────────────────────
🕐 Last Update: 12:18:06.872 | Data Size: 64 bytes

🔘 BUTTONS:
   Pressed: B1, B3, B11
   Status:  B1  [B2] B3  B4  ...  (pressed buttons highlighted)
   Total: 32 buttons (showing first 32)

🎚️  AXES:
   X-Axis (Steering)  :  32768 (0..65535)           bytes[1-2]
   Y-Axis (Throttle)  :   4095 (0..4095)            bytes[3-4]:0bit+12bit
   Z-Axis (Brake)     :      0 (0..4095)            bytes[4-5]:4bit+12bit
   Total: 3 axes

📊 RAW DATA (non-zero bytes):
   [0]=0x01 [1]=0xAF [2]=0x7C [3]=0xFF [4]=0x0F ...
```

## Architecture

The application is built with a modular architecture:

### Core Components

- **`InputSource.cs`**: Abstract base class for all input sources
  - `HidInputSource`: HID device implementation
  - `KeyboardInputSource`: Keyboard simulation implementation
- **`DisplayManager.cs`**: Handles all UI rendering and formatting
- **`Program.cs`**: Main application logic and menu system

### Key Classes

```csharp
abstract class InputSource
{
    abstract DeviceCapabilities GetCapabilities();
    abstract InputState GetCurrentState();
    abstract void Poll();
}

class DeviceCapabilities
{
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
    DateTime Timestamp;
}
```

## Understanding the Output

### Button Display
- **Pressed**: Shows only the buttons currently being pressed
- **Status Grid**: Visual grid with pressed buttons highlighted in green
- **Layout**: Shows which byte and bit each button reads from (for debugging)

### Axis Display
- Shows axis name (e.g., X-Axis, Wheel, Slider)
- Current value
- Logical min/max range in parentheses
- Byte positions and bit-packing information
- Values update in real-time as you move controls

### Raw Data
- Shows non-zero bytes for debugging
- Format: `[byte_index]=0xHH` (hexadecimal)
- Useful for understanding the raw data format and troubleshooting

## Common Device Types

### Steering Wheels
- Look for devices from manufacturers like Logitech, Thrustmaster, Fanatec, SIMAGIC
- Typical VID/PID combinations:
  - Logitech: VID 0x046D
  - Thrustmaster: VID 0x044F
  - Fanatec: VID 0x0EB7
  - SIMAGIC: VID 0x0483

### Pedals
- May appear as separate HID devices or integrated with the wheel
- Axis data typically represents pedal position
- Often use 12-bit values (0-4095 range) packed into bytes

## Troubleshooting

### macOS Permission Issues
If you see "❌ Failed to open device", you may need to:
1. Go to System Settings > Privacy & Security > Input Monitoring
2. Add your Terminal application or IDE
3. Restart the application

### No Devices Found
- Ensure devices are properly connected via USB
- Try unplugging and reconnecting the device
- Use 'r' to refresh the device list

### Button Detection Issues
- Check the "Button Layout" section to see byte/bit mappings
- Compare with "RAW DATA" to see if bytes change when pressing buttons
- Use debug mode ('d' key) to see all byte changes in real-time

### Descriptor Parsing Warning
If you see "⚠️ Warning: Could not parse report descriptor", the app will fall back to basic interpretation:
- Assumes 32 buttons (bytes 1-4)
- Assumes 8 common axes (16-bit values)
- This fallback works for most devices but may not be optimal

## Technical Details

- Uses **HidSharp** library for cross-platform HID access
- Parses HID Report Descriptors to identify device capabilities
- Handles bit-packed data (e.g., 12-bit axes packed in bytes)
- Detects Report ID and adjusts byte offsets automatically
- 100ms read timeout for responsive display
- 50ms update throttling to prevent display flickering
- Fixed display area with cursor positioning for clean updates
- Keyboard simulation uses damping algorithm for realistic input

## Extending the Application

To add a new input source:

1. Create a class that inherits from `InputSource`
2. Implement required methods: `InitializeAsync()`, `GetCapabilities()`, `GetCurrentState()`, `Poll()`
3. Add menu option in `Program.cs`
4. Display is automatically handled by `DisplayManager`

Example:
```csharp
public class MyCustomInputSource : InputSource
{
    public override string Name => "My Custom Device";
    // ... implement other methods
}
```

## WebSocket Server

The application can run a WebSocket server to broadcast HID input data to connected clients in real-time. This is useful for:

- Creating web-based dashboards
- Remote monitoring of input devices
- Integration with other applications
- Testing HID input processing

### WebSocket Message Format

The server broadcasts JSON messages with the following structure:

```json
{
  "steering": 32767,
  "throttle": 0,
  "brake": 0,
  "wheel_buttons": [false, false, false, false, false, false, false, false, false, false]
}
```

**Field Descriptions:**
- `steering`: Steering wheel position (0-65535, center at 32767)
- `throttle`: Throttle pedal position (0-65535)
- `brake`: Brake pedal position (0-65535)
- `wheel_buttons`: Array of 10 booleans for wheel buttons 1-10

### Server Endpoints

- **WebSocket**: `ws://localhost:8080/` - Real-time input data
- **HTTP Static Files**: `http://localhost:8080/` - Serves client HTML/JS/CSS
- **Status API**: `http://localhost:8080/api/status` - Server and device status

### Included Client

The `client-example/index.html` file provides a complete web client that:
- Connects to the WebSocket server
- Displays real-time steering, throttle, and brake values
- Shows button states with visual indicators
- Fetches server status information
- Provides a modern, responsive UI

To use the client:
1. Start the server: `dotnet run websocket`
2. Open `http://localhost:8080/` in a web browser

### Data Serialization

The WebSocket server uses a modular serialization system. Currently implemented:

- **JSON Serializer** (`JsonDataSerializer`): Compact JSON format

To add a new serialization format:

```csharp
public class BinaryDataSerializer : IDataSerializer
{
    public string ContentType => "application/octet-stream";
    
    public byte[] Serialize(object data)
    {
        // Implement binary serialization
        return new byte[0];
    }
}
```

Then use it:
```csharp
var serializer = new BinaryDataSerializer();
var server = new WebSocketServer(port: 8080, dataSerializer: serializer);
```

## What's New in This Version

✨ **Keyboard Simulation Mode**: Test without physical hardware
✨ **Modular Architecture**: Clean separation of concerns, easy to extend
✨ **Enhanced Button Display**: Grid view showing all button states
✨ **Bit-Packed Data Support**: Correctly handles 12-bit axes and packed fields
✨ **Report ID Detection**: Automatically adjusts byte offsets for devices using Report IDs
✨ **Improved Error Handling**: Clear messages with emoji indicators
✨ **Display Manager**: Centralized rendering logic
✨ **Fixed Keyboard Mode**: Resolved issue where keyboard input immediately quit monitoring
✨ **Updated Keyboard Controls**: S key now controls brake (Z-axis) instead of throttle down
✨ **Menu Navigation**: Keyboard mode now returns to main menu instead of exiting program
✨ **WebSocket Server**: Real-time broadcasting of HID data to web clients

## License

This is a testing/diagnostic tool. Use freely for personal projects.

## Features

- **Device Discovery**: Lists all connected HID devices with details (VID, PID, manufacturer, product name)
- **HID Report Descriptor Parsing**: Automatically identifies buttons and axes from the device's HID descriptor
- **Single Device Monitoring**: Connect to and monitor input from a specific device by index
- **Multi-Device Monitoring**: Monitor all available HID devices simultaneously
- **Fixed Display Mode**: Clean, non-scrolling display that updates in place
- **Real-time Input Display**: Shows:
  - Button states (which buttons are pressed)
  - Axis values with their logical ranges
  - Raw byte data for debugging

## Requirements

- .NET 9.0 SDK
- macOS, Windows, or Linux
- **macOS Note**: You may need to grant Input Monitoring permissions to Terminal/IDE

## Usage

### Running the Application

```bash
cd HIDDeviceMonitor
dotnet run
```

Or use the quick start script:
```bash
./run.sh
```

### Main Menu Options

1. **Enter device number (0-N)**: Monitor a specific device with detailed analysis
2. **'a'**: Monitor ALL devices simultaneously
3. **'r'**: Refresh the device list (useful when plugging in new devices)
4. **'q'**: Quit the application

### Enhanced Display Format

When monitoring a device, you'll see:

```
=== Monitoring Device [0]: SIMAGIC Alpha ===
VID: 0xXXXX, PID: 0xXXXX

📊 Device Capabilities:
   Buttons: 32
   Axes: 6

🎮 Detected Axes:
   X-Axis: X-Axis (Range: 0 to 65535)
   Y-Axis: Y-Axis (Range: 0 to 65535)
   Wheel: Wheel (Range: -32768 to 32767)
   ...

⌨️  Press any key to stop monitoring

────────────────────────────────────────────────────────────────────────────────
🕐 Last Update: 12:18:06.872 | Data Size: 64 bytes

🔘 BUTTONS:
   Pressed: B1, B3, B11
   Total: 32 buttons

🎚️  AXES:
   X-Axis         :  32768 (0..65535)
   Y-Axis         :  16384 (0..65535)
   Wheel          :   1024 (-32768..32767)
   Total: 6 axes

📊 RAW DATA (non-zero bytes):
   [0]=0x01 [1]=0xA8 [2]=0x81 [4]=0xA0 [5]=0x04 ...
```

## Understanding the Output

### Button Display
- Shows only the buttons currently being pressed
- Format: `B1, B3, B11` (Button numbers start from 1)
- Total button count is automatically detected from the HID descriptor

### Axis Display
- Shows axis name (e.g., X-Axis, Wheel, Slider)
- Current value
- Logical min/max range in parentheses
- Values update in real-time as you move controls

### Raw Data
- Shows non-zero bytes for debugging
- Format: `[byte_index]=0xHH` (hexadecimal)
- Useful for understanding the raw data format

## Common Device Types

### Steering Wheels
- Look for devices from manufacturers like Logitech, Thrustmaster, Fanatec, SIMAGIC
- Typical VID/PID combinations:
  - Logitech: VID 0x046D
  - Thrustmaster: VID 0x044F
  - Fanatec: VID 0x0EB7

### Pedals
- May appear as separate HID devices or integrated with the wheel
- Axis data typically represents pedal position

## Troubleshooting

### macOS Permission Issues
If you see "❌ Failed to open device", you may need to:
1. Go to System Settings > Privacy & Security > Input Monitoring
2. Add your Terminal application or IDE
3. Restart the application

### No Devices Found
- Ensure devices are properly connected via USB
- Try unplugging and reconnecting the device
- Use 'r' to refresh the device list

### Descriptor Parsing Warning
If you see "⚠️ Warning: Could not parse report descriptor", the app will fall back to basic interpretation:
- Assumes 32 buttons (first 4 bytes)
- Assumes 8 common axes (16-bit values)
- This fallback still works for most devices

## Technical Details

- Uses **HidSharp** library for cross-platform HID access
- Parses HID Report Descriptors to identify device capabilities
- 100ms read timeout for responsive display
- 50ms update throttling to prevent display flickering
- Fixed display area with cursor positioning for clean updates
- Automatically detects button and axis configurations

## Testing Your Devices

1. Start the application
2. Connect your steering wheel/pedals
3. Press 'r' to refresh if not visible
4. Select the device by number
5. Turn the wheel, press buttons, push pedals
6. Observe the button states and axis values updating in real-time

## What's New in This Version

✨ **HID Report Descriptor Parsing**: Automatically identifies the correct button and axis layout
✨ **Fixed Display Mode**: No more scrolling - updates in place for easier reading
✨ **Enhanced Axis Information**: Shows axis names (X, Y, Wheel, etc.) and their value ranges
✨ **Better Button Display**: Shows only pressed buttons with total count
✨ **Improved Error Handling**: Clear messages with emoji indicators

## License

This is a testing/diagnostic tool. Use freely for personal projects.
