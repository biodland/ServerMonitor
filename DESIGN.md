# ServerMonitor - Design Document

**Project Name**: ServerMonitor  
**Version**: 2.0.0  
**Status**: Refactoring Phase 1  
**Last Updated**: 2024

---

## 📑 Table of Contents

1. [Overview](#overview)
2. [Goals](#goals)
3. [Architecture](#architecture)
4. [Project Structure](#project-structure)
5. [Core Components](#core-components)
6. [Provider Pattern](#provider-pattern)
7. [Data Models](#data-models)
8. [Configuration](#configuration)
9. [Web Interface](#web-interface)
10. [Future Roadmap](#future-roadmap)

---

## Overview

**ServerMonitor** is a modular, multi-vendor server hardware monitoring and fan control application. Originally built for Dell R720 XD servers, it has been refactored to support multiple server platforms through a clean provider pattern.

### Supported Hardware

| Vendor | Model | Status | Provider |
|--------|-------|--------|----------|
| Dell | R720 XD | ✅ Implemented | `DellR720XdProvider` |
| Dell | R740 XD | 🚧 Planned | `DellR740XdProvider` |
| Dell | R240 | 🚧 Planned | `DellR240Provider` |
| SuperMicro | Various | 🚧 Planned | `SuperMicroProvider` |
| AsRock Rack | Various | 📋 Future | `AsRockProvider` |
| HPE | ProLiant | 📋 Future | `HpeProvider` |

---

## Goals

### Primary Goals
1. **Multi-vendor Support**: Single application supporting multiple server platforms
2. **Modular Architecture**: Clean separation of concerns with provider pattern
3. **Extensibility**: Easy to add new server providers
4. **Maintainability**: Clear code structure and documentation
5. **Testability**: Well-defined interfaces enable unit testing

### Secondary Goals
1. Auto-detection of server hardware
2. Real-time monitoring with charts and graphs
3. Modern, responsive web interface
4. RESTful API for integration
5. Future native IPMI implementation (no ipmitool dependency)

---

## Architecture

### Layered Architecture

```
┌─────────────────────────────────────────────────────────┐
│                  Presentation Layer                      │
│              (Controllers + Razor Views)                 │
├─────────────────────────────────────────────────────────┤
│                  Application Layer                       │
│           (Services + Business Logic)                    │
├─────────────────────────────────────────────────────────┤
│                  Provider Layer                          │
│    (Vendor-specific Implementations: Dell, SuperMicro)  │
├─────────────────────────────────────────────────────────┤
│                  Hardware Abstraction                    │
│            (IPMI Client + Sensor Reading)                │
├─────────────────────────────────────────────────────────┤
│                  Infrastructure                          │
│         (ipmitool, lm-sensors, system tools)            │
└─────────────────────────────────────────────────────────┘
```

### Key Design Patterns

1. **Provider Pattern**: Each server vendor has its own implementation
2. **Strategy Pattern**: Different monitoring strategies per vendor
3. **Factory Pattern**: Provider selection at runtime
4. **Repository Pattern**: Abstracted data access
5. **Dependency Injection**: All services injected via DI container

---

## Project Structure

```
ServerMonitor/
├── ServerMonitor.csproj
├── Program.cs                          # Application entry point
├── appsettings.json                    # Configuration
│
├── Core/                               # Core abstractions
│   ├── Interfaces/
│   │   ├── IServerProvider.cs         # Main provider interface
│   │   ├── ITemperatureMonitor.cs     # Temperature monitoring
│   │   ├── IFanController.cs          # Fan control
│   │   ├── IPowerMonitor.cs           # Power monitoring
│   │   ├── IGpuMonitor.cs             # GPU monitoring
│   │   └── IIpmiClient.cs             # IPMI abstraction
│   │
│   ├── Models/
│   │   ├── ServerInfo.cs              # Server metadata
│   │   ├── TemperatureReading.cs      # Temperature data
│   │   ├── FanReading.cs              # Fan data
│   │   ├── PowerReading.cs            # Power data
│   │   ├── GpuReading.cs              # GPU data
│   │   ├── SensorReading.cs           # Generic sensor
│   │   └── ServerMetrics.cs           # Aggregated metrics
│   │
│   └── Enums/
│       ├── ServerVendor.cs            # Dell, SuperMicro, etc.
│       ├── FanControlMode.cs          # Auto, Manual
│       └── HealthStatus.cs            # Good, Warning, Critical
│
├── Providers/                          # Vendor implementations
│   ├── ProviderBase/
│   │   ├── ServerProviderBase.cs      # Base class
│   │   └── IpmiProviderBase.cs        # Common IPMI logic
│   │
│   ├── Dell/
│   │   ├── DellProviderBase.cs        # Common Dell logic
│   │   ├── DellR720XdProvider.cs      # R720 XD specific
│   │   ├── DellR740XdProvider.cs      # R740 XD specific
│   │   ├── DellR240Provider.cs        # R240 specific
│   │   └── DellCommands.cs            # Dell IPMI commands
│   │
│   ├── SuperMicro/
│   │   ├── SuperMicroProviderBase.cs
│   │   ├── SuperMicroX10Provider.cs
│   │   └── SuperMicroCommands.cs
│   │
│   └── AsRock/
│       ├── AsRockProviderBase.cs
│       └── AsRockCommands.cs
│
├── Infrastructure/                     # Low-level utilities
│   ├── Ipmi/
│   │   ├── IpmiToolClient.cs          # Wraps ipmitool
│   │   ├── NativeIpmiClient.cs        # Future: native IPMI
│   │   └── IpmiCommandBuilder.cs      # Command construction
│   │
│   ├── System/
│   │   ├── ShellExecutor.cs           # Shell command execution
│   │   ├── LmSensorsParser.cs         # lm-sensors parser
│   │   └── ProcessHelper.cs           # Process utilities
│   │
│   └── Detection/
│       ├── ServerDetectionService.cs   # Auto-detect server
│       └── DmiInfoReader.cs           # DMI/SMBIOS reader
│
├── Services/                           # Application services
│   ├── MonitoringService.cs           # Background monitoring
│   ├── MetricsCollectorService.cs     # Aggregates all metrics
│   ├── FanControlService.cs           # Fan control logic
│   ├── HistoryService.cs              # Metrics history
│   └── HealthCheckService.cs          # Health status
│
├── Web/                                # Web layer
│   ├── Controllers/
│   │   ├── DashboardController.cs     # Main dashboard
│   │   ├── ApiController.cs           # REST API
│   │   └── ConfigController.cs        # Configuration
│   │
│   ├── Models/
│   │   └── ViewModels/                # View-specific models
│   │
│   └── Views/
│       ├── Dashboard/
│       ├── Config/
│       └── Shared/
│
└── wwwroot/                           # Static assets
    ├── css/
    ├── js/
    └── lib/                           # Local Chart.js, etc.
```

---

## Core Components

### IServerProvider Interface

The main interface that all server providers implement:

```csharp
public interface IServerProvider
{
    ServerVendor Vendor { get; }
    string Model { get; }
    bool IsSupported(ServerInfo serverInfo);
    
    Task<ServerInfo> GetServerInfoAsync();
    Task<bool> TestConnectionAsync();
    
    ITemperatureMonitor TemperatureMonitor { get; }
    IFanController FanController { get; }
    IPowerMonitor PowerMonitor { get; }
}
```

### ITemperatureMonitor

```csharp
public interface ITemperatureMonitor
{
    Task<List<TemperatureReading>> GetTemperaturesAsync();
    Task<List<TemperatureReading>> GetCpuCoreTemperaturesAsync();
}
```

### IFanController

```csharp
public interface IFanController
{
    Task<List<FanReading>> GetFanStatusAsync();
    Task<bool> SetFanSpeedAsync(int percentage);
    Task<bool> RestoreAutoControlAsync();
    bool SupportsManualControl { get; }
    int MinSpeedPercentage { get; }
    int MaxSpeedPercentage { get; }
}
```

### IPowerMonitor

```csharp
public interface IPowerMonitor
{
    Task<PowerReading> GetPowerMetricsAsync();
    bool IsSupported { get; }
}
```

### IIpmiClient

```csharp
public interface IIpmiClient
{
    Task<string> ExecuteCommandAsync(string command);
    Task<List<SensorReading>> GetAllSensorsAsync();
    Task<bool> TestConnectionAsync();
    IpmiClientType ClientType { get; }  // InBand, OutBand, Native
}
```

---

## Provider Pattern

### Provider Hierarchy

```
ServerProviderBase (abstract)
├── DellProviderBase (Dell common logic)
│   ├── DellR720XdProvider
│   ├── DellR740XdProvider
│   └── DellR240Provider
├── SuperMicroProviderBase
│   ├── SuperMicroX10Provider
│   └── SuperMicroX11Provider
└── AsRockProviderBase
    └── AsRockE3Provider
```

### Provider Selection Strategy

1. **Auto-detection**: Use DMI/SMBIOS info to identify server
2. **Configuration override**: Allow manual specification
3. **Fallback**: Generic IPMI provider for unknown servers

### Vendor-Specific IPMI Commands

#### Dell
```
Manual fan control:    raw 0x30 0x30 0x01 0x00
Auto fan control:      raw 0x30 0x30 0x01 0x01
Set fan speed:         raw 0x30 0x30 0x02 0xff 0x<HEX>
Power consumption:     sensor get "Pwr Consumption"
```

#### SuperMicro
```
Manual fan control:    raw 0x30 0x45 0x01 0x01
Auto fan control:      raw 0x30 0x45 0x01 0x00
Set fan zone:          raw 0x30 0x70 0x66 0x01 0x<ZONE> 0x<DUTY>
Power monitoring:      sdr type "Power"
```

#### AsRock
```
Standard IPMI commands with limited OEM extensions
Fan control via: raw 0x3a 0x01 0x<FAN> 0x<SPEED>
```

---

## Data Models

### ServerInfo

```csharp
public class ServerInfo
{
    public ServerVendor Vendor { get; set; }
    public string Model { get; set; }
    public string Manufacturer { get; set; }
    public string SerialNumber { get; set; }
    public string BiosVersion { get; set; }
    public string IpmiVersion { get; set; }
    public Dictionary<string, string> Properties { get; set; }
}
```

### TemperatureReading

```csharp
public class TemperatureReading
{
    public string Name { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; } = "°C";
    public double? HighThreshold { get; set; }
    public double? CriticalThreshold { get; set; }
    public TemperatureSource Source { get; set; }  // IPMI, LmSensors, GPU
    public DateTime Timestamp { get; set; }
}
```

### ServerMetrics (Aggregated)

```csharp
public class ServerMetrics
{
    public ServerInfo Server { get; set; }
    public List<TemperatureReading> Temperatures { get; set; }
    public List<TemperatureReading> CpuCores { get; set; }
    public List<FanReading> Fans { get; set; }
    public PowerReading Power { get; set; }
    public List<GpuReading> Gpus { get; set; }
    public FanControlMode CurrentMode { get; set; }
    public DateTime Timestamp { get; set; }
    public HealthStatus OverallHealth { get; set; }
}
```

---

## Configuration

### appsettings.json Structure

```json
{
  "ServerMonitor": {
    "Provider": {
      "AutoDetect": true,
      "Vendor": "Dell",
      "Model": "R720XD"
    },
    "Ipmi": {
      "Mode": "InBand",
      "Host": "127.0.0.1",
      "Username": "",
      "Password": "",
      "Interface": "lanplus",
      "CipherSuite": 3
    },
    "Monitoring": {
      "RefreshIntervalSeconds": 5,
      "HistoryRetentionMinutes": 60,
      "EnableGpuMonitoring": true,
      "EnableLmSensors": true
    },
    "FanControl": {
      "Mode": "Auto",
      "ThresholdsCelsius": {
        "Low": 30,
        "Medium": 40,
        "High": 50,
        "Critical": 60
      },
      "FanSpeeds": {
        "Low": 15,
        "Medium": 25,
        "High": 50,
        "Critical": 100
      }
    }
  },
  "Server": {
    "Port": 1080,
    "Host": "0.0.0.0"
  }
}
```

---

## Web Interface

### URL Structure

```
GET  /                          → Dashboard (main view)
GET  /config                    → Configuration page
GET  /history                   → Historical data view

API:
GET  /api/server/info           → Server identification
GET  /api/metrics/current       → Current metrics
GET  /api/metrics/history       → Historical metrics
GET  /api/temperature           → Temperature data only
GET  /api/fans                  → Fan data only
GET  /api/power                 → Power data only

POST /api/control/fan/manual    → Set manual fan speed
POST /api/control/fan/auto      → Restore auto control
```

### Dashboard Features

1. **Real-time monitoring** with 5-second refresh
2. **Tiled stat cards** showing key metrics
3. **CPU core temperatures** in compact view
4. **Temperature sensors** with progress bars
5. **GPU monitoring** (auto-hidden if not present)
6. **Fan speed visualization**
7. **Real-time charts** (temperature, fans, power)
8. **Speed control buttons**

---

## Dependency Injection Setup

```csharp
// Program.cs

// Register IPMI client based on configuration
builder.Services.AddSingleton<IIpmiClient>(sp => {
    var config = sp.GetRequiredService<IConfiguration>();
    var mode = config["ServerMonitor:Ipmi:Mode"];
    return mode switch {
        "InBand" => new IpmiToolClient(...),
        "OutBand" => new IpmiToolClient(...),
        "Native" => new NativeIpmiClient(...),
        _ => throw new InvalidOperationException()
    };
});

// Register provider via factory
builder.Services.AddSingleton<IServerProviderFactory>();
builder.Services.AddSingleton<IServerProvider>(sp => {
    var factory = sp.GetRequiredService<IServerProviderFactory>();
    return factory.CreateProvider();
});

// Register monitoring services
builder.Services.AddSingleton<MonitoringService>();
builder.Services.AddHostedService<MonitoringService>();

// Register infrastructure
builder.Services.AddSingleton<ShellExecutor>();
builder.Services.AddSingleton<LmSensorsParser>();
```

---

## Future Roadmap

### Phase 2: Multi-Server Support
- ✅ Implement Dell R740 XD provider
- ✅ Implement Dell R240 provider
- ✅ Implement SuperMicro X10 provider
- ✅ Add server auto-detection
- ✅ Provider configuration UI

### Phase 3: Native IPMI Implementation
- Research IPMI v2.0 protocol
- Implement RMCP+ support
- Add cipher suite handling
- Replace ipmitool dependency
- Performance benchmarking

**Native IPMI Benefits:**
- No external dependencies
- Better performance (no shell spawning)
- Cross-platform without system packages
- Connection pooling
- Custom retry logic

**Native IPMI Challenges:**
- Complex protocol implementation
- Authentication (cipher suites 0-17)
- Session management
- Vendor OEM commands

### Phase 4: Advanced Features
- Historical data persistence (SQLite)
- Multi-server monitoring (one app, multiple servers)
- Alert system with notifications
- Email/Slack/Discord integration
- Custom dashboards
- User authentication
- Role-based access control

### Phase 5: Enterprise Features
- Prometheus metrics export
- Grafana dashboard templates
- LDAP/AD integration
- Audit logging
- Rate limiting
- API authentication

---

## Migration Strategy

### From IPMIFanControl to ServerMonitor

1. **Phase 1**: Refactor codebase (current)
   - Rename project
   - Restructure into modular architecture
   - Implement provider pattern for Dell

2. **Phase 2**: Maintain backward compatibility
   - Keep existing API endpoints
   - Migrate config schema gracefully
   - Document changes

3. **Phase 3**: Add new vendors
   - Implement other Dell models
   - Add SuperMicro support
   - Add AsRock support

---

## Testing Strategy

### Unit Tests
- Test each provider in isolation
- Mock IPMI client for predictable tests
- Test configuration parsing
- Test metric aggregation logic

### Integration Tests
- Test against real IPMI tool output
- Test web API endpoints
- Test view rendering

### Hardware Tests
- Manual testing on actual hardware
- Verify fan control commands work
- Verify temperature readings accurate

---

## Performance Considerations

1. **Caching**: Cache sensor readings for 5 seconds
2. **Async**: All I/O operations async
3. **Pooling**: Reuse IPMI connections when possible
4. **Throttling**: Limit IPMI command frequency
5. **Background**: All monitoring in background services

---

## Security Considerations

1. **Authentication**: IPMI credentials in secure config
2. **Permissions**: Run with minimal privileges
3. **Validation**: Validate all user input
4. **Rate Limiting**: Prevent API abuse
5. **HTTPS**: TLS for web interface (production)

---

## License

MIT License - See LICENSE file

## Contributing

Contributions welcome! Please follow:
1. Create issue for discussion
2. Fork and create feature branch
3. Implement with tests
4. Submit pull request

---

**End of Design Document**
