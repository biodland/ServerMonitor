# IPMIFanControl

<div align="center">

  ![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
  ![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
  ![License](https://img.shields.io/badge/License-MIT-green.svg?style=for-the-badge)
  ![Platform](https://img.shields.io/badge/Platform-Linux%20%7C%20Windows-lightgrey?style=for-the-badge)

  **A modern C# ASP.NET Core application for automatic fan speed control on servers supporting IPMI**

  [Quick Start](#quick-start) • [Features](#features) • [Documentation](#documentation) • [Contributing](#contributing)

</div>

---

## 📖 About

**IPMIFanControl** is a powerful, cross-platform application designed to automatically control fan speeds on servers (specifically Dell PowerEdge R720 XD, but works with any IPMI-capable server) via iDRAC/IPMI. It features a beautiful web-based dashboard for real-time monitoring, manual override capabilities, and a REST API for integration with other tools.

This project provides a significant upgrade over traditional bash-based solutions, offering:
- 🌡️ Intelligent temperature-based fan control
- 📊 Real-time web dashboard
- 🎛️ Manual override controls
- 🌐 REST API for integration
- 🔄 Automatic error recovery
- 🖥️ Cross-platform support (Linux & Windows)

---

## ✨ Features

### 🎯 Automatic Temperature-Based Control
- Monitors server temperatures continuously
- Automatically adjusts fan speeds based on configurable temperature thresholds
- Safe fallback to iDRAC dynamic control at high temperatures

### 📊 Real-Time Web Dashboard
- Beautiful, modern interface with gradient styling
- Live temperature monitoring with status indicators
- Individual fan RPM readings with visual progress bars
- Auto-refresh every 5 seconds
- Responsive design for desktop and mobile

### 🎮 Manual Override Options
- Set custom fan speeds directly from the web UI
- Quick preset buttons for common speeds (10-50%)
- One-click restore of iDRAC dynamic control
- Toggle automatic control on/off

### 🌐 REST API
- JSON endpoints for system status
- Programmatic control of fan speeds
- Integration support for monitoring tools (Prometheus, Grafana, Home Assistant)

### 🔒 Safety Features
- Automatic restoration of dynamic control on errors
- Temperature threshold protection (defaults to 55°C+)
- Graceful error handling and logging
- Comprehensive error recovery

### 🛠️ Easy Deployment
- One-command systemd service installation
- Cross-platform support (Linux & Windows)
- JSON configuration file
- Automated build and deploy scripts

---

## 🎯 Temperature Control Logic

The application automatically adjusts fan speeds based on the highest temperature reading:

| Temperature Range | Fan Speed | Description |
|------------------|-----------|-------------|
| 0-39°C | 10% | Quiet operation / Normal |
| 40-45°C | 15% | Light load |
| 46-49°C | 20% | Moderate load |
| 50-54°C | 25% | High load |
| 55°C+ | Dynamic | iDRAC auto control (Safety mode) |

> **Note**: These thresholds are fully configurable in `appsettings.json`

---

## 📋 Requirements

### System Requirements
- **Operating System**: Linux (Ubuntu 20.04+ recommended), Debian, or Windows
- **.NET Runtime**: .NET 8.0 SDK (for development) or .NET 8.0 Runtime (for production)
- **ipmitool**: System package for IPMI communication
- **Server**: Dell PowerEdge R720 XD or any IPMI-capable server with iDRAC/BMC access
- **Network**: Ability to connect to iDRAC/BMC IP address

### Install Dependencies

#### Linux (Ubuntu/Debian):
```bash
sudo apt-get update
sudo apt-get install -y ipmitool

# Install .NET 8.0 SDK
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools
```

#### Linux (RHEL/CentOS):
```bash
sudo yum install -y ipmitool

# Install .NET 8.0 SDK
sudo rpm -Uvh https://packages.microsoft.com/config/rhel/8/packages-microsoft-prod.rpm
sudo yum install -y dotnet-sdk-8.0
```

#### Windows:
```powershell
# Using Chocolatey
choco install ipmitool

# Install .NET 8.0 SDK from:
# https://dotnet.microsoft.com/download/dotnet/8.0
```

---

## 🚀 Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/biodland/IPMIFanControl.git
cd IPMIFanControl
```

### 2. Configure iDRAC/BMC Settings

Edit `appsettings.json` with your server's credentials:

```json
{
  "Server": {
    "Port": 5000
  },
  "Idrac": {
    "Ip": "192.168.0.101",
    "User": "root",
    "Password": "YOUR_IDRAC_PASSWORD_HERE"
  },
  "FanControl": {
    "EnableControl": true,
    "CheckIntervalSeconds": 30
  }
}
```

**Important**: Set proper file permissions for security:
```bash
chmod 600 appsettings.json
```

### 3. Test IPMI Connection

Verify iDRAC/BMC is accessible before running the application:

```bash
ipmitool -I lanplus -H 192.168.0.101 -U root -P YOUR_PASSWORD sdr type temperature
```

You should see temperature readings. If this fails, check:
- iDRAC IP address
- iDRAC user credentials
- Network connectivity to iDRAC
- iDRAC IPMI is enabled

### 4. Build and Run

#### Option A: Using the Build Script (Recommended)
```bash
chmod +x build.sh
./build.sh -r
```

#### Option B: Manual Build
```bash
dotnet restore
dotnet build
dotnet run
```

### 5. Access the Web Dashboard

Open your browser and navigate to:
```
http://localhost:5000
```

From another device on your network:
```
http://YOUR_SERVER_IP:5000
```

---

## 🌐 Web Dashboard Tour

The dashboard provides real-time monitoring and control:

### Cards Section
- **Temperature Card**: Shows highest temperature with status indicator (Normal/Elevated/High)
- **Fan Speed Card**: Displays average RPM and total fan count
- **Control Mode Card**: Current mode indicator (Manual/Dynamic/Monitoring)

### Fan Details
- List of all fans with individual RPM readings
- Visual progress bars showing each fan's speed relative to average
- Color-coded indicators (normal vs. high speeds)

### Control Panel
- **Quick Speed Presets**: 10%, 15%, 20%, 25%, 35%, 50% buttons
- **Restore Dynamic Control**: One-click return to iDRAC auto mode
- **Toggle Auto Control**: Pause/resume automatic temperature-based control

---

## 📡 REST API Documentation

### Get Current Status

**Endpoint**: `GET /api/status`

**Description**: Returns current system status in JSON format

**Example Response**:
```json
{
  "timestamp": "2024-01-15T10:30:00Z",
  "mode": "Manual Control",
  "temperatures": {
    "highest": 35,
    "all": [22, 28, 35, 30, 25, 32]
  },
  "fans": {
    "count": 6,
    "averageRPM": 2400,
    "fans": [
      {
        "name": "Fan1 RPM",
        "rpm": 2400
      },
      {
        "name": "Fan2 RPM",
        "rpm": 2450
      }
    ]
  }
}
```

**Example Usage**:
```bash
curl http://localhost:5000/api/status | jq
```

---

### Set Manual Fan Speed

**Endpoint**: `POST /api/control/manual`

**Description**: Set fan speed to a specific percentage (0-100%)

**Request Body**:
```json
{
  "speed": 20
}
```

**Example Usage**:
```bash
curl -X POST http://localhost:5000/api/control/manual \
  -H "Content-Type: application/json" \
  -d '{"speed": 20}'
```

---

### Restore Dynamic Control

**Endpoint**: `POST /api/control/dynamic`

**Description**: Restore iDRAC's automatic fan control

**Example Usage**:
```bash
curl -X POST http://localhost:5000/api/control/dynamic \
  -H "Content-Type: application/json"
```

**Example Response**:
```json
{
  "success": true,
  "message": "Dynamic control restored"
}
```

---

## ⚙️ Configuration

### Application Settings

All configuration is managed through `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Server": {
    "Port": 5000
  },
  "Idrac": {
    "Ip": "192.168.0.101",
    "User": "root",
    "Password": "YOUR_PASSWORD"
  },
  "FanControl": {
    "EnableControl": true,
    "CheckIntervalSeconds": 30
  }
}
```

### Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `Server:Port` | Web interface port | 5000 |
| `Idrac:Ip` | iDRAC/BMC IP address | 192.168.0.101 |
| `Idrac:User` | iDRAC/BMC username | root |
| `Idrac:Password` | iDRAC/BMC password | *(required)* |
| `FanControl:EnableControl` | Enable automatic fan control | true |
| `FanControl:CheckIntervalSeconds` | Temperature check interval | 30 |

### Environment Variables

Configuration can also be provided via environment variables:

```bash
export Idrac__Ip="192.168.0.101"
export Idrac__User="root"
export Idrac__Password="your_password"
export FanControl__EnableControl="true"
export FanControl__CheckIntervalSeconds="30"
export Server__Port="5000"
```

---

## 🔧 Running as a System Service

### Linux Systemd Service

#### Automatic Installation

The `build.sh` script includes service installation:

```bash
./build.sh -s
```

This will:
- Publish the application for production
- Create systemd service file
- Install to `/opt/dell-fan-control`
- Enable and start the service

#### Manual Installation

Create `/etc/systemd/system/ipmifancontrol.service`:

```ini
[Unit]
Description=IPMI Fan Control Service
After=network.target

[Service]
Type=notify
User=your_username
WorkingDirectory=/opt/ipmifancontrol
ExecStart=/usr/bin/dotnet /opt/ipmifancontrol/IPMIFanControl.dll
Restart=always
RestartSec=10
SyslogIdentifier=ipmifancontrol
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl daemon-reload
sudo systemctl enable ipmifancontrol.service
sudo systemctl start ipmifancontrol.service
sudo systemctl status ipmifancontrol.service
```

#### Service Management

```bash
# Check status
sudo systemctl status ipmifancontrol.service

# View logs
sudo journalctl -u ipmifancontrol.service -f

# Stop service
sudo systemctl stop ipmifancontrol.service

# Start service
sudo systemctl start ipmifancontrol.service

# Restart service
sudo systemctl restart ipmifancontrol.service
```

### Windows Service

Using NSSM (Non-Sucking Service Manager):

```cmd
# Install NSSM if needed
choco install nssm

# Install service
nssm install IPMIFanControl "C:\Program Files\dotnet\dotnet.exe"
nssm set IPMIFanControl AppDirectory "C:\path\to\IPMIFanControl"
nssm set IPMIFanControl AppParameters "bin\Release\net8.0\IPMIFanControl.dll"
nssm set IPMIFanControl AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"
nssm set IPMIFanControl DisplayName "IPMI Fan Control"
nssm set IPMIFanControl Description "Automated IPMI fan control service"
nssm start IPMIFanControl
```

---

## 🔍 Troubleshooting

### Common Issues

#### ipmitool Not Found

**Error**: `ipmitool: command not found`

**Solution**: Install ipmitool
```bash
sudo apt-get install ipmitool  # Ubuntu/Debian
sudo yum install ipmitool      # RHEL/CentOS
choco install ipmitool         # Windows (Chocolatey)
```

#### Connection Refused to iDRAC

**Error**: Could not connect to iDRAC

**Solutions**:
1. Verify iDRAC IP address in `appsettings.json`
2. Ensure network connectivity: `ping 192.168.0.101`
3. Check firewall settings (port 623 for IPMI)
4. Verify iDRAC credentials
5. Try manual ipmitool test:
   ```bash
   ipmitool -I lanplus -H 192.168.0.101 -U root -P PASSWORD sdr type temperature
   ```

#### Temperature Readings Fail

**Error**: Failed to read temperatures

**Solutions**:
1. Ensure iDRAC has IPMI enabled
2. Check iDRAC user permissions (requires IPMI privileges)
3. Verify iDRAC firmware version
4. Try manual ipmitool command

#### Port Already in Use

**Error**: Port 5000 is already in use

**Solution**: Change the port in `appsettings.json`:
```json
{
  "Server": {
    "Port": 8080
  }
}
```

#### Application Won't Start

**Solutions**:
1. Check .NET version: `dotnet --version` (should be 8.0+)
2. Run without building: `dotnet run`
3. Check for syntax errors in `appsettings.json`
4. Review application logs for detailed errors

#### High CPU Usage

**Solution**: Increase `CheckIntervalSeconds` in configuration:
```json
{
  "FanControl": {
    "CheckIntervalSeconds": 60
  }
}
```

### Debug Logging

To enable verbose logging for troubleshooting:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Debug",
      "DellFanControl": "Debug"
    }
  }
}
```

---

## 🔐 Security Considerations

⚠️ **Important Security Notes**:

1. **Credential Protection**
   - iDRAC password is stored in `appsettings.json`
   - Set proper file permissions: `chmod 600 appsettings.json`
   - Consider using environment variables for sensitive data
   - Use a dedicated iDRAC account with minimal required permissions

2. **Network Security**
   - By default, the web interface binds to all network interfaces
   - In production, consider:
     - Using firewall rules (iptables, ufw, Windows Firewall)
     - Setting up reverse proxy (nginx/Apache) with HTTPS
     - Implementing VPN or network segmentation

3. **HTTPS Configuration**
   - For production, configure HTTPS with SSL certificates
   - Example using Kestrel:
     ```json
     {
       "Kestrel": {
         "Endpoints": {
           "Https": {
             "Url": "https://*:5001",
             "Certificate": {
               "Path": "certificate.pfx",
               "Password": "your_cert_password"
             }
           }
         }
       }
     }
     ```

4. **Access Control**
   - Consider implementing authentication for the web interface
   - Use reverse proxy authentication (nginx basic auth, OAuth2, etc.)
   - Restrict API access to trusted networks

---

## 📈 Integration Examples

### Prometheus Metrics

You can extend the application to expose Prometheus metrics by adding the OpenTelemetry packages and creating a metrics endpoint.

### Grafana Dashboard

Use the JSON API to feed data to Grafana for advanced visualization:

```bash
# Create a data source in Grafana using the API endpoint
# Build panels to display temperature and fan metrics over time
```

### Home Assistant

Create a custom sensor to display server status in Home Assistant:

```yaml
sensor:
  - platform: rest
    resource: http://YOUR_SERVER_IP:5000/api/status
    name: Dell Server Temperatures
    value_template: "{{ value_json.temperatures.highest }}"
    unit_of_measurement: "°C"
    scan_interval: 30
    json_attributes:
      - temperatures
      - fans
      - mode

template:
  - sensor:
      - name: Dell Server Fan RPM
        state: "{{ state_attr('sensor.dell_server_temperatures', 'fans').averageRPM }}"
        unit_of_measurement: "RPM"
```

### Monitoring Scripts

Example bash script for monitoring:

```bash
#!/bin/bash

# IPMI Fan Control Monitor
# Alerts if temperature exceeds threshold

TEMP_THRESHOLD=50
API_URL="http://localhost:5000/api/status"

while true; do
  TEMP=$(curl -s "$API_URL" | jq -r '.temperatures.highest')
  
  if [ "$TEMP" -gt "$TEMP_THRESHOLD" ]; then
    echo "WARNING: High temperature detected: ${TEMP}°C"
    # Send alert (email, webhook, etc.)
  fi
  
  sleep 60
done
```

---

## 📊 Performance

Typical resource usage:

- **CPU Usage**: < 1% on typical systems
- **Memory Usage**: ~50-100 MB
- **Network Bandwidth**: Minimal (only iDRAC communication)
- **Fan Response Time**: Within 5-10 seconds of temperature change

---

## 🤝 Contributing

We welcome contributions! Please follow these guidelines:

### Development Setup

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature-name`
3. Make your changes
4. Add tests if applicable
5. Commit your changes: `git commit -m 'Add some feature'`
6. Push to the branch: `git push origin feature/your-feature-name`
7. Submit a pull request

### Code Style

- Follow C# coding conventions
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep methods focused and concise
- Apply appropriate error handling

### Commit Messages

Use clear, descriptive commit messages:
```
feat: add Prometheus metrics endpoint
fix: resolve iDRAC connection timeout issue
docs: update installation instructions
refactor: improve error handling in IPMIService
```

### Pull Request Process

1. Ensure your code compiles and passes linting
2. Update documentation as needed
3. Add tests for new functionality
4. Ensure all tests pass
5. Update CHANGELOG.md (if applicable)

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

### MIT License Summary

- ✅ Commercial use
- ✅ Modification
- ✅ Distribution
- ✅ Private use
- ⚠️ License and copyright notice
- ⚠️ Include the full license text in distributions

See [LICENSE](LICENSE) for the full license text.

---

## 🙏 Acknowledgments

- Built with [ASP.NET Core](https://docs.microsoft.com/aspnet/core)
- Uses [ipmitool](https://github.com/ipmitool/ipmitool) for IPMI communication
- Inspired by community solutions for server fan control
- UI design inspired by modern dashboard patterns

---

## 📞 Support

### Documentation

- [Quick Start Guide](#quick-start)
- [Configuration](#configuration)
- [API Documentation](#rest-api-documentation)
- [Troubleshooting](#troubleshooting)

### Getting Help

- 📖 Check the [documentation](#documentation)
- 🐛 [Open an issue](https://github.com/biodland/IPMIFanControl/issues) on GitHub
- 💬 Check existing [discussions](https://github.com/biodland/IPMIFanControl/discussions)

---

## 🗺️ Roadmap

Future enhancements planned:

- [ ] Historical temperature/fan data storage
- [ ] Prometheus metrics export
- [ ] Email/webhook notifications on alerts
- [ ] Multi-server support from single dashboard
- [ ] Custom fan profile configuration
- [ ] Mobile application
- [ ] Integration with monitoring platforms (Datadog, New Relic)
- [ ] Machine learning for optimal fan patterns

---

## 📸 Screenshots

*Include screenshots of the web dashboard when available*

---

## 📝 Changelog

### Version 1.0.0 (2024-06)

#### Added
- Initial release of IPMIFanControl
- Automatic temperature-based fan control
- Real-time web dashboard
- REST API for integration
- Manual override controls
- Cross-platform support (Linux & Windows)
- Systemd service support
- Comprehensive documentation

---

<div align="center">

**Made with ❤️ for quieter servers**

[⬆ Back to Top](#ipmifancontrol)

</div>