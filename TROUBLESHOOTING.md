# IPMI Connection Troubleshooting

## Common Issues and Solutions

### Issue: "Unable to establish IPMI v2 / RMCP+ session"

This error occurs when the IPMI client cannot establish a secure connection to iDRAC. This has several possible causes:

#### Solution 1: Use Local In-Band IPMI (Recommended for Dell R720 XD)

The most reliable approach is to use local in-band IPMI, which connects directly to the BMC without network authentication:

```bash
# Test local IPMI access
ipmitool sensor reading

# If this works, configure the application to use local IPMI
# by using appsettings.local.json with "UseLocal": true
```

**Advantages:**
- No network configuration needed
- No authentication requirements
- More reliable and faster
- Works even if iDRAC network settings are misconfigured

#### Solution 2: Try IPMI v1.5 instead of v2.0

Some older iDRAC versions have issues with IPMI v2.0:

```bash
# Try IPMI v1.5 (less secure but more compatible)
ipmitool -I lan -H 172.16.0.21 -U root -P $pass sensor reading
```

Configure in `appsettings.json`:
```json
{
  "Idrac": {
    "Interface": "lan"
  }
}
```

#### Solution 3: Try Different Cipher Suites

Different iDRAC firmware versions support different cipher suites:

```bash
# Try each cipher suite (0, 1, 3, 15, 17)
ipmitool -I lanplus -H 172.16.0.21 -U root -P $pass -C 0 sensor reading
ipmitool -I lanplus -H 172.16.0.21 -U root -P $pass -C 1 sensor reading
ipmitool -I lanplus -H 172.16.0.21 -U root -P $pass -C 3 sensor reading
ipmitool -I lanplus -H 172.16.0.21 -U root -P $pass -C 15 sensor reading
```

Configure in `appsettings.json`:
```json
{
  "Idrac": {
    "CipherSuite": 0
  }
}
```

#### Solution 4: Verify iDRAC Settings

Access iDRAC web interface (https://172.16.0.21) and verify:

1. **IPMI is enabled:**
   - iDRAC Settings > Network > Services
   - Ensure "IPMI over LAN" is enabled

2. **User permissions:**
   - iDRAC Settings > User Configuration
   - Verify user has "IPMI" privilege
   - Try creating a dedicated IPMI user

3. **IPMI protocol version:**
   - Check if IPMI v2.0 is supported
   - Downgrade to v1.5 if needed

4. **Firmware version:**
   - Check iDRAC firmware version
   - Update to latest if experiencing issues

#### Solution 5: Network Configuration

Check network connectivity:

```bash
# Ping iDRAC
ping -c 4 172.16.0.21

# Check if IPMI port is open
nc -zv 172.16.0.21 623

# Check firewall rules
iptables -L | grep 623
ufw status
```

Ensure port 623 (UDP) is not blocked by firewall.

### Issue: "internal error" (free interface)

The `-I free` interface is experimental and often fails on Dell servers. Use `-I lan` or `-I lanplus` instead.

### Issue: Permission Denied

If you get permission errors with local IPMI:

```bash
# Check user groups
groups

# Add user to ipmi group (if exists)
sudo usermod -a -G ipmi $USER

# Or run with sudo
sudo ipmitool sensor reading
```

### Issue: "ipmitool: command not found"

Install ipmitool:

```bash
# Debian/Ubuntu
sudo apt-get update
sudo apt-get install ipmitool ipmiutil

# RHEL/CentOS
sudo yum install ipmitool

# Verify installation
ipmitool -V
```

## Dell R720 XD Specific Notes

### Default iDRAC Settings

- **Default IP:** Often obtained via DHCP or set during iDRAC configuration
- **Default User:** `root`
- **Default Password:** Often empty or `calvin`
- **IPMI Support:** Dell R720 XD supports both IPMI v1.5 and v2.0

### Known Issues

1. **Firmware Bugs:** Some iDRAC firmware versions have IPMI bugs
   - Solution: Update iDRAC firmware to latest version
   - Version 2.x is more stable than early 1.x

2. **Cipher Suite Limitations:** Older firmware may not support all cipher suites
   - Solution: Try different `-C` values or use v1.5

3. **In-Band vs Out-of-Band:** In-band is always more reliable
   - Solution: Prefer local in-band IPMI when possible

## Configuration Examples

### Local In-Band IPMI (Recommended)

Create `appsettings.local.json`:
```json
{
  "Idrac": {
    "Ip": "",
    "User": "",
    "Password": "",
    "Interface": "lanplus",
    "CipherSuite": 3,
    "UseLocal": true
  }
}
```

### Network IPMI with v1.5

```json
{
  "Idrac": {
    "Ip": "172.16.0.21",
    "User": "root",
    "Password": "your-password",
    "Interface": "lan",
    "CipherSuite": 0,
    "UseLocal": false
  }
}
```

### Network IPMI with Different Cipher Suite

```json
{
  "Idrac": {
    "Ip": "172.16.0.21",
    "User": "root",
    "Password": "your-password",
    "Interface": "lanplus",
    "CipherSuite": 15,
    "UseLocal": false
  }
}
```

## Testing Commands

Use these commands to test different configurations:

```bash
# Test local in-band
ipmitool sensor reading
ipmitool mc info

# Test network IPMI v2.0 with various cipher suites
ipmitool -I lanplus -H 172.16.0.21 -U root -P $pass -C 3 mc info

# Test network IPMI v1.5
ipmitool -I lan -H 172.16.0.21 -U root -P $pass mc info

# Test without password (interactive)
ipmitool -I lanplus -H 172.16.0.21 -U root mc info

# Test connection only
ipmitool -I lanplus -H 172.16.0.21 -U root -P $pass -v
```

## Getting Help

If none of these solutions work:

1. Check iDRAC logs for connection errors
2. Verify network connectivity with tcpdump
3. Try from a different machine
4. Contact Dell Support for firmware issues
5. Check Dell R720 XD documentation for specific IPMI requirements

## Application Diagnostics

The application provides startup diagnostics:

```
→ Using LOCAL in-band IPMI service
[INFO] Local IPMI connection test: SUCCESS
```

If you see errors in the logs, check:

1. Is ipmitool installed?
2. Does the user have permission to access IPMI?
3. Is the BMC accessible (local or network)?
4. Are credentials correct (if using network IPMI)?

Use the `/api/status` endpoint to verify real-time IPMI connectivity:
```bash
curl http://localhost:5000/api/status
```