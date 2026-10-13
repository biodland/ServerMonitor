# ServerMonitor

<div align="center">

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![.NET](https://img.shields.io/badge/.NET_10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Linux](https://img.shields.io/badge/Linux-FCC624?style=for-the-badge&logo=linux&logoColor=black)

**A modular, multi-vendor server monitoring & fan control system**

</div>

> **Note:** This project was previously called *IPMIFanControl* / *DellFanControl*. It has been refactored into a generic, extensible monitoring system that can support multiple server vendors. See [DESIGN.md](DESIGN.md) for the full architecture document.

## Features

- 🖥️ **Multi-vendor support** via a clean provider pattern with auto-detection
- 🌡️ **Real-time temperature monitoring** (IPMI sensors + lm-sensors CPU cores)
- 🌀 **Fan control** with manual / automatic modes and periodic re-application
- ⚡ **Power consumption** monitoring
- 🎮 **GPU monitoring** (NVIDIA via nvidia-smi, AMD via rocm-smi, Intel via sysfs)
- 📊 **Live dashboard** with charts and quick stats (5-second refresh)
- 📈 **System Stats page** — CPU load, memory usage, per-NIC network throughput, filesystem & block-device I/O (2-second refresh)
- 🔌 **REST API** for integration & automation
- 🔍 **Auto-detection** of server hardware via DMI/SMBIOS with provider fallback chain

## Screenshots

![Screenshot of Dashboard](docs/screenshots/Dashboard.png)
![Screenshot of System Stats page](docs/screenshots/SystemStats.png)

## Currently supported hardware

| Vendor      | Model       | Status              |
|-------------|-------------|---------------------|
| Dell        | R720 XD     | ✅ Implemented       |
| Dell        | R740 XD     | ✅ Implemented       |
| Dell        | R240        | ✅ Implemented       |
| Dell        | (Generic)   | ✅ Fallback          |
| SuperMicro  | -           | 🔄 Planned           |
| ASRock      | -           | 🔄 Planned           |
| HPE         | -           | 🔄 Planned           |

Adding new providers requires only implementing `IServerProvider` / `IServerProviderCandidate` for the vendor/model.

## Architecture

```
ServerMonitor/
├── Core/                  # Domain interfaces, models, enums
│   ├── Enums/
│   ├── Interfaces/
│   └── Models/
├── Infrastructure/        # External-system adapters
│   ├── Detection/         # DMI hardware detection + provider factory
│   ├── Gpu/               # nvidia-smi / rocm-smi / sysfs
│   ├── Ipmi/              # IpmiToolClient (ipmitool wrapper)
│   └── System/            # ShellExecutor, lm-sensors parser, LinuxSystemStatsCollector
├── Providers/             # Vendor/model implementations
│   ├── ProviderBase/
│   └── Dell/
├── Services/              # Application services / hosted services
│   ├── MetricsCollectorService
│   ├── SystemStatsService
│   └── FanControlService
├── Controllers/           # MVC + API
│   ├── DashboardController
│   ├── StatsController
│   └── ApiController
└── Views/                 # Razor views
    ├── Dashboard/
    ├── Stats/
    └── Shared/
```

See [DESIGN.md](DESIGN.md) for the detailed architecture and the future native-IPMI roadmap.

## Quick start

### Prerequisites

```bash
sudo apt-get install ipmitool lm-sensors
sudo sensors-detect --auto
```

> **Note:** `ipmitool` requires root privileges for in-band IPMI. Run the application with `sudo` or add the user to the appropriate group.

For Docker deployments, the image includes `lm-sensors`. Run `sudo sensors-detect --auto` on the Docker host before starting the container; sensor detection configures host kernel drivers and should not run during the image build.

### Configuration

Create `appsettings.json` (git-ignored by default) in the project root:

```json
{
  "ServerMonitor": {
    "Server":     { "Port": 5000 },
    "Ipmi":       { "UseLocal": true },
    "FanControl": { "EnableControl": false, "ManualSpeed": 20, "CheckIntervalSeconds": 30 }
  }
}
```

For out-of-band IPMI (e.g. iDRAC over the network):

```json
{
  "ServerMonitor": {
    "Ipmi": {
      "UseLocal": false,
      "Host": "192.168.0.101",
      "Username": "root",
      "Password": "your-idrac-password",
      "Interface": "lanplus",
      "CipherSuite": 3
    }
  }
}
```

You can also create `appsettings.local.json` for local overrides (also git-ignored).

### Run

```bash
dotnet run
```

Open http://localhost:5000 to access the dashboard. The application will auto-detect your server hardware and select the appropriate provider at startup.

## REST API

| Method | Path                          | Description                            |
|--------|-------------------------------|----------------------------------------|
| GET    | `/api/status`                 | Latest hardware metrics snapshot       |
| GET    | `/api/provider`               | Active provider info & capabilities    |
| GET    | `/api/history`                | Hardware metrics history for graphing  |
| GET    | `/api/test`                   | Test the IPMI / hardware connection    |
| POST   | `/api/fans/speed/{percent}`   | Set manual fan speed (5-100%)          |
| POST   | `/api/fans/auto`              | Restore automatic fan control          |
| GET    | `/api/stats`                  | Latest system stats (CPU/mem/net/disk) |
| GET    | `/api/stats/history`          | System stats history for graphing      |

## System Stats

The `/Stats` page (also available at the top-level nav) shows:

- **CPU** — overall and per-core usage, load averages, user/system/iowait breakdown, model name
- **Memory** — total / used / available / cached / buffers, swap usage
- **Network** — per-interface status, link speed, IPv4 addresses, RX/TX rate, totals & errors
- **Storage volumes** — every mounted filesystem with size, free, used, and usage bar
- **Storage devices** — every block device (HDD/SSD) with size, R/W rate, model, and temperature when available

### Filtering network interfaces

By default virtual interfaces (`lo`, `veth*`, `br-*`, `docker*`, `virbr*`, `vnet*`, `tun*`, `tap*`, `wg*`) are hidden. Override via configuration:

```json
"ServerMonitor": {
  "Stats": {
    "Network": {
      "Include": [ "eno1", "eno2" ],
      "Exclude": [ "veth*", "br-*", "lo" ]
    }
  }
}
```

Both lists support `*` wildcards. If `Include` is set, only matching interfaces are shown; `Exclude` filters the remainder.

## Fan control service

The `FanControlService` is an optional background service that periodically re-applies a fixed fan speed. This is useful because some BMCs revert to automatic control after a watchdog timeout. It is **disabled by default**; enable it in configuration:

```json
"ServerMonitor": {
  "FanControl": {
    "EnableControl": true,
    "ManualSpeed": 20,
    "CheckIntervalSeconds": 30
  }
}
```

On graceful shutdown the service automatically restores automatic fan control.

## Adding a new server provider

1. Create a class in `Providers/<Vendor>/` that derives from `ServerProviderBase`
   (or use `DellProviderBase` as a template for similar vendors).
2. Implement `Vendor`, `Model`, `IsSupported(ServerInfo)`, and the three monitor
   components (`ITemperatureMonitor`, `IFanController`, `IPowerMonitor`).
3. Also implement `IServerProviderCandidate` (a marker interface that lets the factory
   enumerate candidates without conflicting with the singleton `IServerProvider`).
4. Register it in `Program.cs`:
   ```csharp
   builder.Services.AddSingleton<IServerProviderCandidate, MyVendorProvider>();
   ```
5. The factory automatically picks the most specific matching provider for the
   detected hardware, falling back to generic providers.

You can also force a specific provider via configuration:

```json
"ServerMonitor": {
  "ForceProvider": "R720XD"
}
```

## Tools

**StatsCheck** — a standalone CLI tool that prints system stats to the console, useful for verifying the collector works without running the web app:

```bash
dotnet run --project src/Tools/StatsCheck/StatsCheck.csproj
```

## Roadmap

- ✅ Phase 1 — Modular refactor with provider pattern
- ✅ Phase 2 — Dell R740 XD / R240 providers, auto-detection, system stats
- 🔄 Phase 3 — SuperMicro / ASRock providers
- 🔄 Phase 4 — Thermal-curve fan control
- 🔄 Phase 5 — Persisted metrics history (SQLite/InfluxDB)
- 🔄 Phase 6 — Native IPMI implementation (replace the ipmitool dependency)

See [DESIGN.md](DESIGN.md) for full details.

## License

MIT — see [LICENSE](LICENSE).
