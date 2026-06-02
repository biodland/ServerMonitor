# ServerMonitor - Design Document

**Project Name**: ServerMonitor  
**Version**: 2.0.0  
**Status**: Phase 2 Complete  
**Last Updated**: 2025

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
10. [REST API](#rest-api)
11. [Tools](#tools)
12. [Future Roadmap](#future-roadmap)

---

## Overview

**ServerMonitor** is a modular, multi-vendor server hardware monitoring and fan control application. Originally built as *IPMIFanControl* for Dell R720 XD servers, it has been refactored into a generic monitoring platform that supports multiple server vendors through a clean provider pattern with auto-detection.

### Supported Hardware

| Vendor | Model | Status | Provider |
|--------|-------|--------|----------|
| Dell | R720 XD | ✅ Implemented | `DellR720XdProvider` |
| Dell | R740 XD | ✅ Implemented | `DellR740XdProvider` |
| Dell | R240 | ✅ Implemented | `DellR240Provider` |
| Dell | Generic | ✅ Fallback | `DellGenericProvider` |
| SuperMicro | Various | 🔄 Planned | — |
| AsRock Rack | Various | 🔄 Planned | — |
| HPE | ProLiant | 🔄 Planned | — |

The factory selects the most specific matching provider at startup. If no model-specific provider matches, the `DellGenericProvider` acts as a fallback for any Dell server. When non-Dell vendors are implemented, each will supply its own generic fallback.

---

## Goals

### Primary Goals
1. **Multi-vendor Support**: Single application supporting multiple server platforms
2. **Modular Architecture**: Clean separation of concerns with provider pattern
3. **Extensibility**: Easy to add new server providers via `IServerProviderCandidate`
4. **Maintainability**: Clear code structure and documentation
5. **Auto-detection**: DMI/SMBIOS-based server identification with config override

### Secondary Goals
1. Real-time monitoring with charts and graphs
2. Modern, responsive web interface with two pages (Dashboard + System Stats)
3. RESTful API for integration & automation
4. GPU monitoring (NVIDIA, AMD, Intel)
5. System-level stats (CPU load, memory, network, storage)
6. Future native IPMI implementation (no ipmitool dependency)

---

## Architecture

### Layered Architecture

```
┌──────────────────────────────────────────────────────────┐
│                  Presentation Layer                      │
│         (Controllers + Razor Views + Static JS)          │
├──────────────────────────────────────────────────────────┤
│                  Application Layer                       │
│           (Services + Business Logic)                    │
├──────────────────────────────────────────────────────────┤
│                  Provider Layer                          │
│    (Vendor-specific Implementations: Dell, …)            │
├──────────────────────────────────────────────────────────┤
│                  Hardware Abstraction                    │
│            (IPMI Client + Sensor Reading)                │
├──────────────────────────────────────────────────────────┤
│                  Infrastructure                          │
│      (ipmitool, lm-sensors, /proc, /sys, shell)         │
└──────────────────────────────────────────────────────────┘
```

### Key Design Patterns

1. **Provider Pattern**: Each server vendor has its own implementation of `IServerProvider`
2. **Strategy Pattern**: Different monitoring strategies per vendor via `ITemperatureMonitor`, `IFanController`, `IPowerMonitor`
3. **Factory Pattern**: `ServerProviderFactory` selects the best provider at runtime via auto-detection
4. **Candidate Pattern**: Providers register as `IServerProviderCandidate`; the factory enumerates candidates and picks the best match, then the selected instance is registered as the singleton `IServerProvider`
5. **Dependency Injection**: All services injected via DI container

---

## Project Structure

```
src/Apps/ServerMonitor/
├── ServerMonitor.csproj              # .NET 10.0 web app
├── Program.cs                        # App entry point, DI setup, provider resolution
│
├── Core/                             # Domain abstractions
│   ├── Interfaces/
│   │   ├── IServerProvider.cs        # Main provider interface + factory + detection
│   │   ├── IMonitoringInterfaces.cs  # ITemperatureMonitor, IFanController,
│   │   │                             #   IPowerMonitor, IGpuMonitor
│   │   ├── IIpmiClient.cs            # IPMI abstraction
│   │   └── ISystemStatsCollector.cs  # System stats collection interface
│   │
│   ├── Models/
│   │   ├── ServerInfo.cs             # Server hardware metadata
│   │   ├── ServerMetrics.cs          # Aggregated server metrics snapshot
│   │   ├── SystemStats.cs            # CPU / Memory / Network / Storage models
│   │   └── Readings.cs               # SensorReading, TemperatureReading,
│   │                                 #   FanReading, PowerReading, GpuReading
│   │
│   └── Enums/
│       ├── CommonEnums.cs            # TemperatureSource, IpmiClientType, GpuVendor
│       ├── FanControlMode.cs         # Auto, Manual, Unknown
│       ├── HealthStatus.cs           # Good, Warning, Critical
│       └── ServerVendor.cs          # Dell, SuperMicro, AsRock, Hpe, Lenovo, IBM, Generic
│
├── Providers/                        # Vendor implementations
│   ├── ProviderBase/
│   │   └── ServerProviderBase.cs     # Abstract base + IServerProviderCandidate
│   │
│   └── Dell/
│       ├── DellProviderBase.cs       # Common Dell logic (creates monitors)
│       ├── DellR720XdProvider.cs     # R720 XD specific
│       ├── DellR740XdProvider.cs     # R740 XD specific
│       ├── DellR240Provider.cs       # R240 specific
│       ├── DellGenericProvider.cs    # Fallback for any Dell server
│       ├── DellTemperatureMonitor.cs # IPMI + lm-sensors temperature
│       ├── DellFanController.cs      # Dell OEM IPMI fan commands
│       ├── DellPowerMonitor.cs       # Dell IPMI power sensor
│       └── DellCommands.cs           # Dell IPMI raw command constants
│
├── Infrastructure/                   # Low-level utilities
│   ├── Ipmi/
│   │   └── IpmiToolClient.cs        # Wraps ipmitool CLI (in-band + out-of-band)
│   │
│   ├── System/
│   │   ├── ShellExecutor.cs         # Centralized shell command execution
│   │   ├── LmSensorsParser.cs       # lm-sensors output parser
│   │   └── LinuxSystemStatsCollector.cs  # /proc + /sys + df stats collector
│   │
│   ├── Detection/
│   │   ├── ServerDetectionService.cs  # DMI/SMBIOS auto-detect
│   │   └── ServerProviderFactory.cs   # Provider selection + IServerProviderCandidate
│   │
│   └── Gpu/
│       └── GpuMonitor.cs            # nvidia-smi / rocm-smi / sysfs GPU reader
│
├── Services/                         # Application services
│   ├── MetricsCollectorService.cs   # BackgroundService: polls provider every 5s
│   ├── SystemStatsService.cs        # BackgroundService: polls collector every 2s
│   ├── FanControlService.cs         # BackgroundService: re-applies manual fan speed
│   ├── IMetricsCollectorService.cs  # Interface for metrics service
│   └── ISystemStatsService.cs       # Interface for stats service
│
├── Controllers/                      # MVC + API controllers
│   ├── DashboardController.cs       # Main dashboard view
│   ├── StatsController.cs           # System stats view
│   └── ApiController.cs            # REST API (attribute-routed)
│
├── Views/
│   ├── Dashboard/Index.cshtml       # Hardware monitoring dashboard
│   ├── Stats/Index.cshtml           # System stats page
│   ├── Shared/_Layout.cshtml        # Layout with top nav
│   └── _ViewImports.cshtml
│
└── wwwroot/
    └── js/chart.js                  # Chart.js local placeholder

src/Tools/StatsCheck/
├── StatsCheck.csproj                 # References ServerMonitor.csproj
└── Program.cs                        # CLI: prints system stats to console
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
    string DisplayName { get; }
    bool IsSupported(ServerInfo serverInfo);
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<ServerInfo> GetServerInfoAsync(CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    
    ITemperatureMonitor TemperatureMonitor { get; }
    IFanController FanController { get; }
    IPowerMonitor PowerMonitor { get; }
}
```

### IServerProviderCandidate

A marker interface that allows providers to be registered in DI without conflicting with the single active `IServerProvider`:

```csharp
public interface IServerProviderCandidate : IServerProvider { }
```

All provider classes implement both `IServerProvider` and `IServerProviderCandidate`. The factory enumerates candidates, picks the best match, and the resulting instance is then registered as the singleton `IServerProvider`.

### ITemperatureMonitor

```csharp
public interface ITemperatureMonitor
{
    Task<List<TemperatureReading>> GetTemperaturesAsync(CancellationToken ct = default);
    Task<List<TemperatureReading>> GetCpuCoreTemperaturesAsync(CancellationToken ct = default);
    bool SupportsCoreTemperatures { get; }
}
```

### IFanController

```csharp
public interface IFanController
{
    Task<List<FanReading>> GetFanStatusAsync(CancellationToken ct = default);
    Task<bool> SetFanSpeedAsync(int percentage, CancellationToken ct = default);
    Task<bool> RestoreAutoControlAsync(CancellationToken ct = default);
    bool SupportsManualControl { get; }
    int MinSpeedPercentage { get; }
    int MaxSpeedPercentage { get; }
}
```

### IPowerMonitor

```csharp
public interface IPowerMonitor
{
    Task<PowerReading> GetPowerMetricsAsync(CancellationToken ct = default);
    bool IsSupported { get; }
}
```

### IGpuMonitor

```csharp
public interface IGpuMonitor
{
    Task<List<GpuReading>> GetGpuReadingsAsync(CancellationToken ct = default);
}
```

### IIpmiClient

```csharp
public interface IIpmiClient
{
    IpmiClientType ClientType { get; }   // InBand, OutBand, Native
    bool IsAvailable { get; }
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
    Task<string> ExecuteCommandAsync(string command, CancellationToken ct = default);
    Task<string> ExecuteRawAsync(string rawHex, CancellationToken ct = default);
    Task<List<SensorReading>> GetAllSensorsAsync(CancellationToken ct = default);
    Task<Dictionary<string, string>> GetBmcInfoAsync(CancellationToken ct = default);
}
```

### ISystemStatsCollector

```csharp
public interface ISystemStatsCollector
{
    Task<SystemStats> CollectAsync(CancellationToken ct = default);
}
```

### IServerProviderFactory

```csharp
public interface IServerProviderFactory
{
    Task<IServerProvider> CreateProviderAsync(CancellationToken ct = default);
    IEnumerable<IServerProvider> GetAvailableProviders();
}
```

### IServerDetectionService

```csharp
public interface IServerDetectionService
{
    Task<ServerInfo> DetectServerAsync(CancellationToken ct = default);
}
```

---

## Provider Pattern

### Provider Hierarchy

```
ServerProviderBase (abstract, implements IServerProvider + IServerProviderCandidate)
├── DellProviderBase (Dell common logic — creates Dell monitors)
│   ├── DellR720XdProvider
│   ├── DellR740XdProvider
│   ├── DellR240Provider
│   └── DellGenericProvider  (fallback — matches any Dell server)
└── (Future: SuperMicroProviderBase, AsRockProviderBase, …)
```

### Provider Selection Strategy

1. **Config override**: If `ServerMonitor:ForceProvider` is set, that provider is used directly
2. **Auto-detection**: DMI/SMBIOS info is read from `/sys/class/dmi/id/` to identify the server
3. **Specific match**: The factory tries model-specific providers first (those whose `Model` ≠ "Generic")
4. **Generic fallback**: If no specific provider matches, a generic vendor fallback is used
5. **Error**: If no provider matches at all, the application exits with a fatal error

### Dell IPMI Commands

These OEM commands are common across most Dell PowerEdge servers:

```
Manual fan control:    raw 0x30 0x30 0x01 0x00
Auto fan control:      raw 0x30 0x30 0x01 0x01
Set fan speed:         raw 0x30 0x30 0x02 0xff 0x<HEX>
Power consumption:     sensor get "Pwr Consumption"
```

All Dell providers share the same fan control and power monitoring logic via `DellProviderBase`, which constructs `DellFanController`, `DellTemperatureMonitor`, and `DellPowerMonitor`. Model-specific providers only differ in the `Model` string and `SupportsModel()` matching logic.

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
    public string Hostname { get; set; }
    public Dictionary<string, string> Properties { get; set; }
    
    public string DisplayName => $"{Manufacturer} {Model}".Trim();
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
    public TemperatureSource Source { get; set; }  // Ipmi, LmSensors, Gpu, Disk, …
    public bool IsPackage { get; set; }
    public DateTime Timestamp { get; set; }
    
    public HealthStatus Status { get; }  // Computed from thresholds
}
```

### FanReading

```csharp
public class FanReading
{
    public string Name { get; set; }
    public int RPM { get; set; }
    public int? PercentageSpeed { get; set; }
    public bool IsHealthy { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### PowerReading

```csharp
public class PowerReading
{
    public double TotalWatts { get; set; }
    public double? CpuWatts { get; set; }
    public double? PowerSupplyWatts { get; set; }
    public string Source { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### GpuReading

```csharp
public class GpuReading
{
    public string Name { get; set; }
    public GpuVendor Vendor { get; set; }  // Nvidia, Amd, Intel
    public double Temperature { get; set; }
    public int? FanSpeedPercent { get; set; }
    public double? PowerWatts { get; set; }
    public int? UsagePercent { get; set; }
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
    public PowerReading? Power { get; set; }
    public List<GpuReading> Gpus { get; set; }
    public FanControlMode CurrentMode { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsConnected { get; set; }
    
    public double MaxTemperature { get; }     // Max across all temps + CPU cores
    public int AverageRpm { get; }            // Average fan RPM
    public HealthStatus OverallHealth { get; } // Computed from all thresholds
}
```

### SystemStats (System-level)

```csharp
public class SystemStats
{
    public DateTime Timestamp { get; set; }
    public CpuStats Cpu { get; set; }
    public MemoryStats Memory { get; set; }
    public List<NetworkInterfaceStats> NetworkInterfaces { get; set; }
    public List<StorageVolumeStats> StorageVolumes { get; set; }
    public List<StorageDeviceStats> StorageDevices { get; set; }
    public long UptimeSeconds { get; set; }
    public string Hostname { get; set; }
    public string KernelVersion { get; set; }
}
```

Each sub-model (`CpuStats`, `MemoryStats`, `NetworkInterfaceStats`, `StorageVolumeStats`, `StorageDeviceStats`) contains both raw counters and computed rates/percentages. Rate-based fields (network throughput, disk I/O) are computed using deltas from the previous collection cycle.

---

## Configuration

### appsettings.json Structure

```json
{
  "ServerMonitor": {
    "Server": {
      "Port": 5000
    },
    "Ipmi": {
      "UseLocal": true,
      "Host": "127.0.0.1",
      "Username": "",
      "Password": "",
      "Interface": "lanplus",
      "CipherSuite": 3
    },
    "FanControl": {
      "EnableControl": false,
      "ManualSpeed": 20,
      "CheckIntervalSeconds": 30
    },
    "ForceProvider": "",
    "Stats": {
      "Network": {
        "Include": [],
        "Exclude": ["veth*", "br-*", "docker*", "virbr*", "vnet*", "tun*", "tap*", "wg*"]
      }
    }
  }
}
```

### Configuration Keys

| Key | Default | Description |
|-----|---------|-------------|
| `ServerMonitor:Server:Port` | `5000` | Kestrel listen port |
| `ServerMonitor:Ipmi:UseLocal` | `true` | Use in-band (local) IPMI |
| `ServerMonitor:Ipmi:Host` | `127.0.0.1` | IPMI host for out-of-band |
| `ServerMonitor:Ipmi:Username` | `""` | IPMI username |
| `ServerMonitor:Ipmi:Password` | `""` | IPMI password |
| `ServerMonitor:Ipmi:Interface` | `lanplus` | IPMI interface type |
| `ServerMonitor:Ipmi:CipherSuite` | `3` | IPMI cipher suite |
| `ServerMonitor:FanControl:EnableControl` | `false` | Enable fan control background service |
| `ServerMonitor:FanControl:ManualSpeed` | `20` | Fan speed % to apply |
| `ServerMonitor:FanControl:CheckIntervalSeconds` | `30` | Re-application interval |
| `ServerMonitor:ForceProvider` | `""` | Force a specific provider by model name |
| `ServerMonitor:Stats:Network:Include` | `[]` | Network interface whitelist (glob patterns) |
| `ServerMonitor:Stats:Network:Exclude` | `[veth*, br-*, …]` | Network interface blacklist (glob patterns) |

### Backward Compatibility

The IPMI configuration also reads the legacy `Idrac:` section for compatibility with v1.x configurations. The new `ServerMonitor:Ipmi:` section takes precedence.

The port can also be set via the old `Server:Port` key as a fallback.

---

## Web Interface

### Pages

| URL | Controller | Description |
|-----|-----------|-------------|
| `/` or `/Dashboard` | `DashboardController` | Hardware monitoring dashboard |
| `/Stats` | `StatsController` | System stats (CPU/mem/net/disk) |

Both pages share the `_Layout.cshtml` which provides a top navigation bar with links to both pages.

### Dashboard Features

1. **Quick stats bar** — Max temp, average fan RPM, power, control mode
2. **CPU core temperatures** — Package + per-core readings with progress bars
3. **IPMI temperature sensors** — All sensor readings with status indicators
4. **GPU monitoring** — Auto-hidden section if no GPUs detected
5. **Real-time charts** — Temperature, fan RPM, and power over time (via Chart.js CDN with fallbacks)
6. **Fan speed list** — All fans with RPM and progress bars
7. **Fan control panel** — Auto/Manual toggle + speed preset buttons (10%–50%)
8. **Auto-refresh** — 5-second polling cycle

### System Stats Features

1. **KPI strip** — CPU %, memory %, network throughput, disk I/O
2. **CPU card** — Model name, user/system/iowait/idle breakdown, per-core usage grid
3. **Memory card** — Total, used, available, free, buffers, cached, swap with progress bars
4. **Network table** — Per-interface status, speed, IP, RX/TX rates, totals, errors
5. **Filesystems table** — Mount point, type, size, free, used, usage bar
6. **Block devices table** — Device name, model, size, R/W rates, temperature, HDD/SSD badge
7. **Auto-refresh** — 2-second polling cycle

---

## REST API

All API endpoints are attribute-routed under `/api/` via `ApiController`.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/status` | Latest `ServerMetrics` snapshot (auto-refreshes if stale) |
| `GET` | `/api/provider` | Active provider info: vendor, model, capabilities |
| `GET` | `/api/history` | Rolling hardware metrics history (compact time-series) |
| `GET` | `/api/test` | Test IPMI/hardware connection |
| `POST` | `/api/fans/speed/{percentage}` | Set manual fan speed (5–100%) |
| `POST` | `/api/fans/auto` | Restore automatic fan control |
| `GET` | `/api/stats` | Latest `SystemStats` snapshot |
| `GET` | `/api/stats/history` | Rolling system stats history (compact time-series) |

### Error Handling

The `/api/status` endpoint returns the last known state with `isConnected: false` on errors. Other endpoints return appropriate HTTP status codes (400 for bad requests, 500 for hardware failures).

---

## Dependency Injection Setup

The DI setup in `Program.cs` follows this order:

1. **Infrastructure** — `ShellExecutor`, `LmSensorsParser`, `IpmiToolClient`, `ServerDetectionService`, `ServerProviderFactory`, `GpuMonitor`, `LinuxSystemStatsCollector`
2. **Provider candidates** — All `IServerProviderCandidate` implementations are registered (specific models first, generic fallback last)
3. **Provider resolution** — The factory is invoked *before* `builder.Build()` to resolve the active `IServerProvider` synchronously (avoids async DI hangs)
4. **Services** — `MetricsCollectorService` (5s interval), `SystemStatsService` (2s interval), `FanControlService` (configurable interval)
5. **Kestrel** — Port from config, listens on all interfaces

```csharp
// Provider resolution before Build()
var factory = new ServerProviderFactory(detection, config, logger, candidates);
activeProvider = await factory.CreateProviderAsync(cts.Token);
builder.Services.AddSingleton<IServerProvider>(activeProvider);
```

This approach ensures the provider is fully initialized before the web host starts, providing immediate data on the first request.

---

## Tools

### StatsCheck

A standalone CLI tool for verifying the `LinuxSystemStatsCollector` without running the web application:

```bash
dotnet run --project src/Tools/StatsCheck/StatsCheck.csproj
```

Prints CPU, memory, network, and storage stats to the console. It takes two samples (with a 1.5s gap) so that rate-based fields have meaningful values.

---

## Future Roadmap

### Phase 3: Additional Vendors
- Implement SuperMicro provider (X10, X11 platforms)
- Implement AsRock Rack provider
- Implement HPE ProLiant provider (iLO IPMI commands)
- Each vendor will follow the same pattern: base class + model-specific classes + `IServerProviderCandidate` registration

### Phase 4: Thermal-curve Fan Control
- Temperature-based automatic fan speed adjustment
- Configurable thermal curves per provider
- Hysteresis to prevent oscillation
- Replace fixed-speed `FanControlService` with curve-based logic

### Phase 5: Persisted Metrics History
- SQLite or InfluxDB storage for historical data
- Configurable retention policy
- Historical trend charts in the dashboard
- Export capabilities (CSV, JSON)

### Phase 6: Native IPMI Implementation
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
- Authentication (cipher suites 0–17)
- Session management
- Vendor OEM commands

### Longer-term Ideas
- Prometheus metrics export
- Alert system with notifications (email, Slack, Discord)
- Multi-server monitoring from a single dashboard
- User authentication and role-based access
- Mobile-friendly PWA
- Grafana dashboard templates

---

## Performance Considerations

1. **Metrics interval**: Hardware metrics collected every 5 seconds, system stats every 2 seconds
2. **History buffer**: 120 samples for hardware metrics (~10 min), 300 for system stats (~10 min)
3. **Async**: All I/O operations are async; parallel collection where possible
4. **Delta-based rates**: Network and disk rates computed from successive samples
5. **Background services**: All collection runs in `BackgroundService` instances
6. **Caching**: Server info is lightweight and cached after first call

---

## Security Considerations

1. **Configuration**: `appsettings.json` is git-ignored; use `appsettings.local.json` for secrets
2. **IPMI credentials**: Stored in configuration files; ensure proper file permissions
3. **Permissions**: In-band IPMI requires root; run with `sudo` or appropriate capabilities
4. **Validation**: Fan speed input is validated (5–100% range per Dell provider)
5. **No authentication**: The web interface currently has no authentication — restrict network access in production

---

## License

MIT License - See LICENSE file

## Contributing

Contributions welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

---

**End of Design Document**
