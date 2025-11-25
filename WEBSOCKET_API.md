# WebSocket API Documentation

This document describes the WebSocket protocol and API used by the HID Device Monitor application. This API allows external applications (web dashboards, overlays, other local services) to receive real-time input data from the monitored HID devices (Steering Wheels, Pedals, Gamepads) or the Keyboard Simulator.

## Connection Details

- **Protocol**: WebSocket (`ws://`)
- **Default Port**: `8080` (Configurable via `--port` argument)
- **URL**: `ws://localhost:8080/`
- **Direction**: Server-to-Client (Broadcast only)
- **Data Format**: JSON (UTF-8 encoded text)

## Message Protocol

The server broadcasts the current state of the input device to all connected clients.

- **Update Rate**: Approximately 30 updates per second (throttled to ~33ms intervals).
- **Message Type**: Text
- **Encoding**: UTF-8

### Payload Structure

Every message received from the server is a JSON object representing the normalized state of the racing controls.

```json
{
  "steering": 32767,
  "throttle": 0,
  "brake": 0,
  "wheel_buttons": [false, false, false, false, false, false, false, false, false, false]
}
```

### Field Definitions

| Field | Type | Range | Description |
|-------|------|-------|-------------|
| `steering` | Integer | `0` - `65535` | Steering wheel position.<br>• `0`: Full Left<br>• `32767`: Center<br>• `65535`: Full Right |
| `throttle` | Integer | `0` - `65535` | Accelerator pedal position.<br>• `0`: Released (0%)<br>• `65535`: Fully Pressed (100%) |
| `brake` | Integer | `0` - `65535` | Brake pedal position.<br>• `0`: Released (0%)<br>• `65535`: Fully Pressed (100%) |
| `wheel_buttons` | Array\<Boolean\> | `[10]` | State of the first 10 buttons.<br>• `true`: Pressed<br>• `false`: Released<br>Index 0 corresponds to Button 1, Index 1 to Button 2, etc. |

### Data Normalization

The server automatically normalizes input from different device types to match the standard racing wheel format described above.

#### 1. Racing Wheels & Pedals
- **Steering**: Mapped directly from the device's X-Axis.
- **Throttle**: Mapped directly from the device's Y-Axis or Throttle Axis.
- **Brake**: Mapped directly from the device's Z-Axis or Brake Axis.

#### 2. Gamepads (e.g., Xbox, PlayStation controllers)
When a device is classified as a Gamepad, the server applies the following mapping:
- **Steering**: Left Stick X-Axis.
  - Input range `-1.0` to `1.0` is mapped to `0` - `65535`.
- **Throttle**: Right Stick Y-Axis.
  - Pushing the stick **UP** increases throttle from `0` to `65535`.
- **Brake**: Left Stick Y-Axis.
  - Pushing the stick **UP** increases brake from `0` to `65535`.

#### 3. Keyboard Simulator
- **Steering**: `A` (Left) / `D` (Right).
  - Smoothly interpolates between `0` and `65535`.
  - Auto-centers when keys are released (unless damping is disabled).
- **Throttle**: `W`.
  - Increases value when held, decreases when released.
- **Brake**: `S` or `Space`.
  - Increases value when held, decreases when released.

## HTTP Status API

The server also provides a standard HTTP endpoint to check the server status.

- **URL**: `http://localhost:8080/api/status`
- **Method**: `GET`
- **Content-Type**: `application/json`

### Response Example

```json
{
  "server": {
    "status": "running",
    "websocket_url": "ws://localhost:8080/",
    "http_url": "http://localhost:8080/",
    "uptime": "2023-10-27 14:30:00",
    "connected_clients": 1
  },
  "settings": {
    "data_format": "application/json",
    "port": 8080
  }
}
```

## Client Implementation Example (JavaScript)

Here is a minimal example of how to connect to the WebSocket server and handle data in a web browser or Node.js application.

```javascript
const ws = new WebSocket('ws://localhost:8080/');

ws.onopen = () => {
    console.log('Connected to HID Device Monitor');
};

ws.onmessage = (event) => {
    try {
        const data = JSON.parse(event.data);
        
        // Access normalized data
        const steeringPercent = (data.steering / 65535) * 100;
        const throttlePercent = (data.throttle / 65535) * 100;
        const brakePercent = (data.brake / 65535) * 100;
        
        console.log(`Steering: ${steeringPercent.toFixed(1)}%`);
        console.log(`Throttle: ${throttlePercent.toFixed(1)}%`);
        console.log(`Brake:    ${brakePercent.toFixed(1)}%`);
        
        // Check Button 1
        if (data.wheel_buttons[0]) {
            console.log('Button 1 Pressed!');
        }
        
    } catch (e) {
        console.error('Error parsing message:', e);
    }
};

ws.onclose = () => {
    console.log('Disconnected');
};

ws.onerror = (error) => {
    console.error('WebSocket Error:', error);
};
```
