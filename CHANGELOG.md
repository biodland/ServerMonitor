# Changelog

All notable changes to the ServerMonitor project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Planned
- Additional vendor providers (SuperMicro, HPE, Lenovo)
- Thermal curve fan profiles
- Persistent configuration storage
- Native IPMI library (Mongoose-implementations)
- Prometheus metrics export
- Email/webhook notifications on alerts
- Multi-server support from single dashboard
- Mobile application or PWA

---

## [2.0.0] - 2026-06-02

### Changed
- **Project renamed** from IPMIFanControl to **ServerMonitor** to reflect the expanded scope beyond IPMI fan control
- Upgraded from .NET 8.0 to **.NET 10.0**
- Replaced monolithic IPMIService with modular **provider architecture** (IServerProvider, IServerProviderCandidate, IServerProviderFactory, IServerDetectionService)
- Provider selection now uses DMI/SMBIOS auto-detection at startup with `ForceProvider` override option
- Provider resolution moved before `builder.Build()` to prevent async dependency injection hangs
- Fan control service interval is now configurable (was hardcoded at 30s)
- Fan speed range updated to 5–100% (matching DellFanController.MinSpeedPercentage)
- Dashboard auto-refresh interval changed from 5s to 2s for system stats, 5s for hardware metrics

### Added
- **System stats collection** via /proc and /sys filesystems (CpuStats, MemoryStats, NetworkInterfaceStats, StorageVolumeStats, StorageDeviceStats)
- **SystemStatsService** — background service collecting system stats every 2 seconds with 300-sample rolling buffer
- **MetricsCollectorService** — background service collecting hardware metrics every 5 seconds with 120-sample rolling buffer
- **FanControlService** — background service for temperature-based fan control with configurable interval
- **StatsController** and **Views/Stats/** — dedicated system stats web page
- **GPU monitoring** support (NVIDIA nvidia-smi, AMD rocm-smi, Intel sysfs) via IGpuMonitor interface
- **Dell server provider** with full implementation for PowerEdge R740XD and R240
  - DellServerProvider implementing IServerProvider with DisplayName and InitializeAsync
  - DellTemperatureMonitor with core temperature support (lm-sensors) and IPMI inlet/exhaust
  - DellFanController with DellFanController.MinSpeedPercentage (5%) floor
  - DellPowerMonitor tracking CPU and power supply watts
  - DellGpuMonitor supporting NVIDIA, AMD, and Intel GPUs
  - InBandIpmiClient (ipmitool local) and OutOfBandIpmiClient (ipmitool LAN)
  - IpmiClientFactory for automatic IPMI client selection
- **IIpmiClient interface** with IsAvailable, ExecuteRawAsync, GetBmcInfoAsync
- **IServerProviderCandidate interface** for provider self-registration and DMI-based detection
- **IServerDetectionService** for SMBIOS/DMI-based vendor identification
- **Network interface filtering** with glob patterns (e.g., `lo`, `docker*`, `veth*`)
- **StatsCheck** console tool for quick system stats verification outside the web app
- **appsettings.local.json** support for local configuration overrides (git-ignored)
- **ForceProvider** configuration option to bypass auto-detection and force a specific provider
- Rolling history buffers: 120 samples for hardware metrics, 300 samples for system stats
- OverallHealth computed property on ServerMetrics
- TemperatureReading.IsPackage property and computed Status property
- PowerReading.CpuWatts, PowerSupplyWatts, Source, IsAvailable properties
- FanReading.PercentageSpeed and IsHealthy computed properties
- ServerVendor enum (Dell, SuperMicro, AsRock, HPE, Lenovo, IBM, Generic)

### API Changes
- `POST /api/control/manual` → `POST /api/fans/speed/{pct}` (set fan speed by percentage)
- `POST /api/control/dynamic` → `POST /api/fans/auto` (restore automatic fan control)
- Added `GET /api/provider` — get current provider info
- Added `GET /api/history` — get hardware metrics history
- Added `GET /api/test` — test provider connectivity
- Added `GET /api/stats` — get current system stats
- Added `GET /api/stats/history` — get system stats history

### Web Interface
- New **System Stats** page (Views/Stats/Index.cshtml) with CPU, memory, network, and storage monitoring
- Dashboard page updated with GPU monitoring and improved layout
- Navigation bar updated with System Stats link

### Removed
- Monolithic IPMIService (replaced by modular provider architecture)
- Old IPMI-only fan control logic (now provider-based)
- Hardcoded 30-second check interval
- Old manual override preset buttons (10%, 15%, 20%, 25%, 35%, 50%)

---

## [1.0.0] - 2026-06-01

### Added
- Initial release of IPMIFanControl
- Automatic temperature-based fan control system
- Web-based dashboard with real-time monitoring
- Temperature thresholds (10-25% fan speeds based on temp)
- REST API endpoints for programmatic control
  - `GET /api/status` — Get system status
  - `POST /api/control/manual` — Set manual fan speed
  - `POST /api/control/dynamic` — Restore dynamic control
- Manual override controls with quick presets (10%, 15%, 20%, 25%, 35%, 50%)
- Individual fan RPM monitoring with visual progress bars
- Auto-refresh dashboard (every 5 seconds)
- Automatic restoration of iDRAC dynamic control on errors
- Temperature safety threshold (55°C+ triggers dynamic mode)
- Comprehensive error handling and logging
- JSON configuration file (appsettings.json)
- Environment variable support for configuration
- Linux support with systemd service installation
- Build and deploy script (build.sh)
- Complete documentation
- MIT License for open-source distribution

[2.0.0]: https://github.com/biodland/IPMIFanControl/releases/tag/v2.0.0
[1.0.0]: https://github.com/biodland/IPMIFanControl/releases/tag/v1.0.0
