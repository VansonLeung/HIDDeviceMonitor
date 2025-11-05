Clarifications Needed:
1. Data Format Interpretation
From the output, I see your device sends reports with specific patterns. To properly decode this:

Do you want the app to auto-detect/learn the data ranges (track min/max as you use the device), OR
Do you already know the SIMAGIC Alpha's data format (which bytes represent what)?

>> I don't know the data format; i guess i do not need to know the data ranges for now. Just obtain the data values is ok.

2. Button Detection
I see the app currently interprets bytes [0] and [1] as button bitfields. For your steering wheel:

How many physical buttons does your wheel have? >> More than 10 buttons
Should I continue using the basic bitfield interpretation, or do you need a specific mapping? >> Basic bitfield interpretation is ok for now.

3. Axes Detection
The current code shows bytes 2+ as potential axes. For steering wheels typically:

Steering angle (main axis)
Additional axes (shifter paddles, encoders, etc.)

Do you want:

A) Simple approach: Track all changing bytes as "potential axes" and show their min/max/current values dynamically? 
B) Manual specification: You tell me which byte indices are which axes (e.g., bytes [4-5] = steering, byte [12] = something else)?  
C) Smart detection: The app monitors which bytes change frequently and treats them as axes automatically?

>> Can it identify specific controls (e.g., X-axis for steering, Y-axis for throttle) by parsing the Report Descriptor of the HID device?
>> Can it recognize the HID steering wheel and query its report descriptor?

1. Display Preferences
For the output format, would you prefer:

A) Continuous scrolling (like now) with enhanced info?
B) Fixed display area that updates in place (cleaner, less scrolling)?
C) Summary view showing only when values change significantly?

>> B) Fixed display area that updates in place (cleaner, less scrolling)

5. Calibration
For axes ranges (min/max):

Should there be a calibration mode where you turn the wheel full left/right to set ranges?
Or just auto-track min/max values as you use it?

>> Forget it for now. Just display the current value(s).

6. Additional Features
Would you like:

Option to export/log data to a file for analysis? >> Not now
Visual indicators like ASCII bars for axes? (e.g., [====|====] for centered steering) >> Not now
Dead zone detection (identify resting/neutral positions)?  >> Not now
