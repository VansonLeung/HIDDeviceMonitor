# HID Device Monitor

A .NET 9.0 console application for discovering and monitoring HID (Human Interface Device) input devices, with a focus on steering wheels and pedals.

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
