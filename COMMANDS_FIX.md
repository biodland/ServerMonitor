# IPMI Commands Fix for Dell R720 XD

## Problem Identified

The original IPMI service was using incorrect commands for Dell R720 XD:

1. **Sensor Reading:** Used `ipmitool sensor reading` which expects a specific sensor ID
   - **Error:** `sensor reading <id> ... [id] id: name of desired sensor`
   - **Fix:** Changed to `ipmitool sensor` to list all sensors

2. **Fan Control:** Used incorrect IPMI raw commands
   - **Error:** `Unable to send RAW command (channel=0x0 netfn=0x30 lun=0x0 cmd=0x45 rsp=0xc1): Invalid command`
   - **Fix:** Updated to correct Dell R720 XD commands

3. **Data Parsing:** Incorrect parsing of sensor output format
   - **Error:** Fan RPM values were decimals (e.g., "16320.000") but code used `int.TryParse`
   - **Fix:** Changed to `double.TryParse` and cast to int

## Correct Dell R720 XD IPMI Commands

### Temperature/Fan Sensors
```bash
ipmitool sensor
```

Output format:
```
Fan1             | 16320.000  | RPM        | ok
Inlet Temp       | 32.000     | degrees C  | ok
Exhaust Temp     | 35.000     | degrees C  | ok
Temp             | 42.000     | degrees C  | ok
Temp             | 44.000     | degrees C  | ok
```

### Fan Control Commands

**Enable Manual Control:**
```bash
ipmitool raw 0x30 0x30 0x01 0x00
```

**Set Fan Speed (percentage in hex):**
```bash
# 20% = 0x14
ipmitool raw 0x30 0x30 0x02 0xff 0x14

# 25% = 0x19
ipmitool raw 0x30 0x30 0x02 0xff 0x19

# 30% = 0x1E
ipmitool raw 0x30 0x30 0x02 0xff 0x1E

# 50% = 0x32
ipmitool raw 0x30 0x30 0x02 0xff 0x32

# 100% = 0x64
ipmitool raw 0x30 0x30 0x02 0xff 0x64
```

**Restore Automatic Control:**
```bash
ipmitool raw 0x30 0x30 0x01 0x01
```

## Hexadecimal Conversion Table

| Percentage | Hex Value |
|------------|-----------|
| 10%        | 0x0A      |
| 15%        | 0x0F      |
| 20%        | 0x14      |
| 25%        | 0x19      |
| 30%        | 0x1E      |
| 40%        | 0x28      |
| 50%        | 0x32      |
| 60%        | 0x3C      |
| 75%        | 0x4B      |
| 100%       | 0x64      |

## Changes Made to IPMIService_Local.cs

### 1. Fixed Sensor Reading Command
```csharp
// Before (broken):
var command = BuildCommand("sensor reading");

// After (fixed):
var command = BuildCommand("sensor");
```

### 2. Fixed Temperature Parsing
```csharp
// Updated to handle Dell R720 format with better logging
if (line.Contains("degrees C"))
{
    var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length >= 2)
    {
        var sensorName = parts[0].Trim();
        var tempValue = parts[1].Trim();

        if (double.TryParse(tempValue, out var temp))
        {
            temperatures.Add((int)Math.Round(temp));
            _logger.LogDebug("Temperature sensor '{Name}': {Temp}°C", sensorName, temp);
        }
    }
}
```

### 3. Fixed Fan RPM Parsing
```csharp
// Changed from int.TryParse to double.TryParse
if (line.Contains("RPM") && (line.StartsWith("Fan") || line.Contains("Fan")))
{
    var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length >= 2)
    {
        var fanName = parts[0].Trim();
        var rpmValue = parts[1].Trim();

        // RPM values are decimals like "16320.000"
        if (double.TryParse(rpmValue, out var rpm))
        {
            fans.Add(new FanInfo
            {
                Name = fanName,
                RPM = (int)rpm,
                Timestamp = DateTime.Now
            });
            _logger.LogDebug("Fan '{Name}': {RPM} RPM", fanName, rpm);
        }
    }
}
```

### 4. Fixed Fan Control Commands
```csharp
// Enable manual control
var enableManual = BuildCommand("raw 0x30 0x30 0x01 0x00");
await ExecuteCommandAsync(enableManual);

// Set fan speed (convert percentage to hex)
int hexValue = percentage;
var hexString = hexValue.ToString("X2");
var setSpeed = BuildCommand($"raw 0x30 0x30 0x02 0xff 0x{hexString}");
await ExecuteCommandAsync(setSpeed);

// Restore automatic control
var command = BuildCommand("raw 0x30 0x30 0x01 0x01");
await ExecuteCommandAsync(command);
```

## Testing

After applying these fixes, the application should:

1. ✅ Successfully read temperature sensors
2. ✅ Successfully read fan RPM values
3. ✅ Successfully set fan speeds
4. ✅ Successfully restore automatic fan control
5. ✅ Display correct values in the web interface

## Verification Commands

Test locally before running the application:

```bash
# Test sensor reading
ipmitool sensor

# Test manual control
ipmitool raw 0x30 0x30 0x01 0x00

# Set to 20%
ipmitool raw 0x30 0x30 0x02 0xff 0x14

# Check fans should be at ~20% speed
ipmitool sensor | grep Fan

# Restore auto control
ipmitool raw 0x30 0x30 0x01 0x01
```

## References

- Dell R720 XD Fan Control Guide: https://blog.filegarden.net/2020/10/06/reduce-the-fan-noise-of-the-dell-r720xd-plus-other-12th-gen-servers-with-ipmi
- ipmitool Documentation: http://ipmitool.sourceforge.net/
- Dell iDRAC 7 Documentation

## Summary

This fix resolves all IPMI command issues for Dell R720 XD servers:
- Correct sensor reading commands
- Correct fan control IPMI commands
- Correct data parsing for sensor output
- Comprehensive logging for debugging