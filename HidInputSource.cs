using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HidSharp;
using HidSharp.Reports;

namespace HIDDeviceMonitor;

/// <summary>
/// HID device input source that reads from a physical HID device
/// Uses a background thread to continuously read input without blocking the main program
/// </summary>
public class HidInputSource : InputSource
{
    private readonly HidDevice _device;
    private readonly int _deviceIndex;
    private HidStream? _stream;
    private Task? _readTask;
    private CancellationTokenSource? _cancellationTokenSource;
    private InputState _currentState;
    private DeviceCapabilities? _capabilities;
    private readonly object _stateLock = new object();
    private bool _isConnected = false;
    private int _maxInputReportLength = 64; // Default

    public override string Name => _device.GetProductName() ?? "Unknown HID Device";
    public override string DeviceType => "HID Device";
    public override bool IsConnected => _isConnected;

    public HidInputSource(HidDevice device, int deviceIndex = 0)
    {
        _device = device;
        _deviceIndex = deviceIndex;
        _currentState = new InputState();
    }

    public override async Task<bool> InitializeAsync()
    {
        try
        {
            _stream = _device.Open();
            _stream.ReadTimeout = Timeout.Infinite; // Block until data is available
            
            // Get max input report length
            var reportDescriptor = _device.GetReportDescriptor();
            var inputReport = reportDescriptor.DeviceItems.FirstOrDefault()?.InputReports.FirstOrDefault();
            _maxInputReportLength = inputReport?.Length ?? 64;
            
            // Parse capabilities
            _capabilities = ParseDeviceCapabilities();
            _isConnected = true;
            
            // Start background reading thread
            _cancellationTokenSource = new CancellationTokenSource();
            _readTask = Task.Run(() => BackgroundReadLoop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            
            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to initialize HID device: {ex.Message}");
            _isConnected = false;
            return false;
        }
    }

    public override InputState GetCurrentState()
    {
        lock (_stateLock)
        {
            return _currentState;
        }
    }

    public override void Poll()
    {
        // Polling is handled automatically by the background thread
        // This method is a no-op for HID devices
    }

    public override DeviceCapabilities GetCapabilities()
    {
        if (_capabilities != null)
            return _capabilities;

        Console.WriteLine("⚠️  Device capabilities not initialized, returning default values.");

        var defaultCapabilities = new DeviceCapabilities
        {
            TotalButtons = 0,
            TotalAxes = 0,
            AdditionalInfo = "Device not initialized",
            Manufacturer = "Unknown",
            ProductName = "Unknown",
            VendorId = 0,
            ProductId = 0
        };
        defaultCapabilities.DeviceType = HIDDeviceMonitor.DeviceType.Unknown;
        return defaultCapabilities;
    }

    /// <summary>
    /// Background thread that continuously reads from the HID device
    /// This runs independently and doesn't block the main program thread
    /// </summary>
    private void BackgroundReadLoop(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[_maxInputReportLength];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Blocking read - waits for data from device
                    int bytesRead = _stream!.Read(buffer, 0, buffer.Length);

                    if (bytesRead > 0)
                    {
                        // Parse the input data
                        var inputState = ParseInputReport(buffer, bytesRead);
                        
                        // Update current state atomically
                        lock (_stateLock)
                        {
                            _currentState = inputState;
                        }
                    }
                }
                catch (TimeoutException)
                {
                    // Normal timeout, continue loop
                    continue;
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    // Log error but keep trying
                    Console.WriteLine($"⚠️  HID read error: {ex.Message}");
                    Thread.Sleep(100); // Brief delay before retrying
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation, exit gracefully
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ HID background read loop error: {ex.Message}");
            _isConnected = false;
        }
    }

    private InputState ParseInputReport(byte[] data, int length)
    {
        var state = new InputState
        {
            RawData = data.Take(length).ToArray(),
            Timestamp = DateTime.Now
        };

        var caps = GetCapabilities();

        // Parse axes
        foreach (var axis in caps.Axes)
        {
            int value = ParseAxisValue(data, axis);
            state.Axes[axis.Name] = new AxisState
            {
                Name = axis.Name,
                Value = value,
                MinValue = axis.LogicalMin,
                MaxValue = axis.LogicalMax,
                ByteInfo = $"bytes[{axis.ByteIndex}]"
            };
        }

        // Parse buttons
        foreach (var button in caps.Buttons)
        {
            bool isPressed = ParseButtonValue(data, button);
            state.Buttons[button.Index] = isPressed;
        }

        return state;
    }

    private int ParseAxisValue(byte[] data, AxisCapability axis)
    {
        if (axis.ByteIndex >= data.Length) return 0;

        int value = 0;
        
        // Handle bit-aligned values (for packed HID data)
        if (axis.BitOffset != 0 || (axis.BitSize % 8 != 0))
        {
            // Bit-packed data - need to extract bits carefully
            int totalBits = axis.BitSize;
            int bitPosition = axis.BitOffset;
            int byteIndex = axis.ByteIndex;
            
            // Read enough bytes to cover all bits
            int bytesNeeded = ((bitPosition + totalBits + 7) / 8);
            
            if (byteIndex + bytesNeeded <= data.Length)
            {
                // Extract bits from the buffer
                for (int i = 0; i < totalBits; i++)
                {
                    int currentBit = bitPosition + i;
                    int currentByte = byteIndex + (currentBit / 8);
                    int currentBitInByte = currentBit % 8;
                    
                    if (currentByte < data.Length)
                    {
                        int bitValue = (data[currentByte] >> currentBitInByte) & 1;
                        value |= (bitValue << i);
                    }
                }
            }
        }
        else
        {
            // Byte-aligned values (standard case)
            if (axis.BitSize <= 8)
            {
                value = data[axis.ByteIndex];
            }
            else if (axis.BitSize <= 16 && axis.ByteIndex + 1 < data.Length)
            {
                // 16-bit value (little-endian)
                value = data[axis.ByteIndex] | (data[axis.ByteIndex + 1] << 8);
            }
            else if (axis.BitSize <= 32 && axis.ByteIndex + 3 < data.Length)
            {
                // 32-bit value (little-endian)
                value = data[axis.ByteIndex] | 
                       (data[axis.ByteIndex + 1] << 8) |
                       (data[axis.ByteIndex + 2] << 16) |
                       (data[axis.ByteIndex + 3] << 24);
            }
        }
        
        // Handle signed values if logical min is negative
        if (axis.LogicalMin < 0 && axis.BitSize > 0)
        {
            int signBit = 1 << (axis.BitSize - 1);
            if ((value & signBit) != 0)
            {
                // Sign extend
                value |= ~((1 << axis.BitSize) - 1);
            }
        }

        return value;
    }

    private bool ParseButtonValue(byte[] data, ButtonCapability button)
    {
        if (button.ByteIndex >= data.Length) return false;

        byte buttonByte = data[button.ByteIndex];
        return (buttonByte & (1 << button.BitIndex)) != 0;
    }

    private DeviceCapabilities ParseDeviceCapabilities()
    {
        return DeviceClassifier.ParseDeviceCapabilities(_device);
    }

    public override void Dispose()
    {
        // Stop the background reading thread
        _cancellationTokenSource?.Cancel();
        
        // Wait for the thread to finish (with timeout)
        try
        {
            _readTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Ignore cancellation exceptions
        }

        _cancellationTokenSource?.Dispose();
        _stream?.Dispose();
        _isConnected = false;
    }
}
