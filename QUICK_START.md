# Quick Start Guide - Dell R720 XD IPMI Fan Control

## Step 1: Enable Local IPMI (Recommended)

Since you're experiencing network IPMI issues, use local in-band IPMI which is more reliable:

```bash
# Test if local IPMI works
ipmitool sensor reading

# If this works, create the local configuration
/var/opt/fancontrol/IPMIFanControl/appsettings.local.json
```

## Step 2: Create Local Configuration

Create `/var/opt/fancontrol/IPMIFanControl/appsettings.local.json`:

```json
{
  "Server": {
    "Port": 5000
  },
  "Idrac": {
    "Ip": "",
    "User": "",
    "Password": "",
    "Interface": "lanplus",
    "CipherSuite": 3,
    "UseLocal": true
  },
  "FanControl": {
    "EnableControl": true,
    "CheckIntervalSeconds": 30
  }
}
```

**Key Setting:** `"UseLocal": true` enables local in-band IPMI.

## Step 3: Restart the Application

```bash
cd /var/opt/fancontrol/IPMIFanControl

# If running as service
sudo systemctl restart dell-fancontrol

# If running manually
sudo dotnet run
```

## Step 4: Verify Connection

The application will show startup diagnostics:

```
→ Using LOCAL in-band IPMI service
[INFO] Local IPMI connection test: SUCCESS
╔═══════════════════════════════════════════════════════════════╗
║   Dell R720 XD Fan Control System                       ║
╠═══════════════════════════════════════════════════════════════╣
║   Web Interface: http://localhost:5000                  ║
║   API Endpoint:    http://localhost:5000/api/status     ║
╚═══════════════════════════════════════════════════════════════╝
```

## Step 5: Access Web Interface

Open your browser: `http://your-server-ip:5000`

You should see:
- Current temperatures
- Fan speeds
- Control panel for manual override

## Troubleshooting Local IPMI

### Error: "ipmitool: command not found"
```bash
sudo apt-get update
sudo apt-get install ipmitool ipmiutil
```

### Error: "Could not open device at /dev/ipmi0 or /dev/ipmi/0"
```bash
# Load IPMI kernel modules
sudo modprobe ipmi_devintf
sudo modprobe ipmi_si

# Make permanent
echo "ipmi_devintf" | sudo tee -a /etc/modules
echo "ipmi_si" | sudo tee -a /etc/modules
```

### Error: Permission denied
```bash
# Give user permission to access IPMI
sudo usermod -a -G ipmi $USER
# OR run as root (not recommended for security)
```

## Alternative: Fix Network IPMI

If you prefer network IPMI, try these steps:

### Option A: Try IPMI v1.5
```bash
ipmitool -I lan -H 172.16.0.21 -U root -P $pass sensor reading
```

Edit `appsettings.json`:
```json
{
  "Idrac": {
    "Interface": "lan"
  }
}
```

### Option B: Try Different Cipher Suites
```bash
# Try cipher suite 0
ipmitool -I lanplus -H 172.16.0.21 -U root -P $pass -C 0 sensor reading

# Try cipher suite 1
ipmitool -I lanplus -H 172.16.0.21 -U root -P $pass -C 1 sensor reading

# Try cipher suite 15
ipmitool -I lanplus -H 172.16.0.21 -U root -P $pass -C 15 sensor reading
```

Edit `appsettings.json`:
```json
{
  "Idrac": {
    "CipherSuite": 0
  }
}
```

### Option C: Access iDRAC Web Interface

1. Go to https://172.16.0.21
2. Login with root/user credentials
3. Check:
   - iDRAC Settings > Network > Services > IPMI over LAN: **Enabled**
   - iDRAC Settings > User Configuration > User Privileges: **IPMI enabled**
   - Note the exact password being used

### Option D: Update iDRAC Firmware

If all else fails, update iDRAC firmware:
- Download from Dell Support website
- Update via iDRAC web interface or Lifecycle Controller

## Verify Application Status

```bash
# Check if service is running
sudo systemctl status dell-fancontrol

# View logs
sudo journalctl -u dell-fancontrol -f

# Test API endpoint
curl http://localhost:5000/api/status | jq
```

## Expected Output

Successful API response example:
```json
{
  "currentTemperature": 42,
  "currentFanStatus": "Normal",
  "currentFanSpeedPercentage": 15,
  "currentMode": "Manual",
  "timestamp": "2024-01-15T10:30:00Z",
  "temperatures": [
    {
      "name": "CPU1 Temp",
      "value": 41,
      "unit": "°C"
    },
    {
      "name": "CPU2 Temp", 
      "value": 43,
      "unit": "°C"
    }
  ],
  "fans": [
    {
      "name": "Fan1",
      "speedRPM": 4800
    }
  ]
}
```

## Next Steps

1. ✅ Configure local IPMI (recommended)
2. ✅ Restart application
3. ✅ Verify connection
4. ✅ Access web interface
5. ✅ Monitor temperatures
6. ✅ Adjust fan control settings as needed

## Need Help?

- Check the full troubleshooting guide: `TROUBLESHOOTING.md`
- Review the main README: `README.md`
- Check application logs for specific errors
- Verify ipmitool works independently first