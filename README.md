# ServerMonitor

<div align="center">

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Linux](https://img.shields.io/badge/Linux-FCC624?style=for-the-badge&logo=linux&logoColor=black)

**A modular, multi-vendor server monitoring & fan control system**

</div>

> **Note:** This project was previously called *IPMIFanControl* / *DellFanControl*. It has been refactored into a generic, extensible monitoring system that can support multiple server vendors. See [DESIGN.md](DESIGN.md) for the full architecture document.

## Features

- 🖥️ **Multi-vendor support** via a clean provider pattern
- 🌡️ **Real-time temperature monitoring** (IPMI sensors + lm-sensors CPU cores)
- 🌀 **Fan control** with manual / automatic modes
- ⚡ **Power consumption** monitoring
- 🎮 **GPU monitoring** (NVIDIA, AMD, Intel)
- 📊 **Live dashboard** with charts and quick stats
- 📈 **System Stats page** — CPU load, memory usage, per-NIC network throughput, filesystem & block-device I/O
- 🔌 **REST API** for integration & automation

## Currently supported hardware

| Vendor      | Model       | Status              |
|-------------|-------------|---------------------|
| Dell        | R720 XD     | ✅ Implemented      |
| Dell        | R740 XD     | ✅ Implemented      |
| Dell        | R240        | ✅ Implemented      |
| Dell        | (Generic)   | ✅ Fallback         |
| SuperMicro  | -           | 🔄 Planned          |
| ASRock      | -           | 🔄 Planned          |
| HPE         | -           | 🔄 Planned          |

Adding new providers requires only implementing `IServerProvider` for the vendor/model.

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
│   └── System/            # ShellExecutor, lm-sensors parser
├── Providers/             # Vendor/model implementations
│   ├── ProviderBase/
│   └── Dell/
├── Services/              # Application services / hosted services
│   ├── MetricsCollectorService
│   └── FanControlService
├── Controllers/           # MVC + API
└── Views/Dashboard/       # Razor dashboard
```

See [DESIGN.md](DESIGN.md) for the detailed architecture and the future native-IPMI roadmap.

## Quick start

### Prerequisites

```bash
sudo apt-get install ipmitool lm-sensors
sudo sensors-detect --auto
```

### Configuration

Edit `appsettings.json`:

```json
{
  "ServerMonitor": {
    "Server":     { "Port": 5000 },
    "Ipmi":       { "UseLocal": true },
    "FanControl": { "EnableControl": false, "ManualSpeed": 20, "CheckIntervalSeconds": 30 }
  }
}
```

For out-of-band IPMI:

```json
"Ipmi": {
  "UseLocal": false,
  "Host": "192.168.0.101",
  "Username": "root",
  "Password": "your-idrac-password"
}
```

### Run

```bash
dotnet run
```

Open http://localhost:5000 to access the dashboard.

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

By default virtual interfaces (`lo`, `veth*`, `br-*`, `docker*`, `virbr*`, `vnet*`,
`tun*`, `tap*`, `wg*`) are hidden. Override via configuration:

```json
"ServerMonitor": {
  "Stats": {
    "Network": {
      "Include": [ "eno1", "eno2" ],          // optional whitelist
      "Exclude": [ "veth*", "br-*", "lo" ]    // overrides the defaults
    }
  }
}
```

Both lists support `*` wildcards.

## Adding a new server provider

1. Create a class in `Providers/<Vendor>/` that derives from `ServerProviderBase`
   (or use `DellProviderBase` as a template for similar vendors).
2. Implement `Vendor`, `Model`, `IsSupported(ServerInfo)`, and the three monitor
   components (`ITemperatureMonitor`, `IFanController`, `IPowerMonitor`).
3. Register it in `Program.cs`:
   ```csharp
   builder.Services.AddSingleton<IServerProvider, MyVendorProvider>();
   ```
4. The factory automatically picks the most specific matching provider for the
   detected hardware.

## Roadmap

- ✅ Phase 1 — Modular refactor (this release)
- 🔄 Phase 2 — SuperMicro / ASRock providers
- 🔄 Phase 3 — Thermal-curve fan control
- 🔄 Phase 4 — Persisted metrics history (SQLite/InfluxDB)
- 🔄 Phase 5 — Native IPMI implementation (replace the ipmitool dependency)

See [DESIGN.md](DESIGN.md) for full details.

## License

MIT — see [LICENSE](LICENSE).
