# Changelog

All notable changes to the IPMIFanControl project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Planned
- Historical temperature/fan data storage
- Prometheus metrics export
- Email/webhook notifications on alerts
- Multi-server support from single dashboard
- Custom fan profile configuration
- Mobile application or PWA
- Integration with monitoring platforms (Datadog, New Relic)
- Machine learning for optimal fan patterns

---

## [1.0.0] - 2024-06-01

### Added
- Initial release of IPMIFanControl
- Automatic temperature-based fan control system
- Web-based dashboard with real-time monitoring
- Temperature thresholds (10-25% fan speeds based on temp)
- REST API endpoints for programmatic control
  - `GET /api/status` - Get system status
  - `POST /api/control/manual` - Set manual fan speed
  - `POST /api/control/dynamic` - Restore dynamic control
- Manual override controls with quick presets (10%, 15%, 20%, 25%, 35%, 50%)
- Individual fan RPM monitoring with visual progress bars
- Auto-refresh dashboard (every 5 seconds)
- Automatic restoration of iDRAC dynamic control on errors
- Temperature safety threshold (55°C+ triggers dynamic mode)
- Comprehensive error handling and logging
- JSON configuration file (appsettings.json)
- Environment variable support for configuration
- Cross-platform support (Linux and Windows)
- Systemd service installation support
- Build and deploy script (build.sh)
- Complete documentation
  - Comprehensive README with installation guide
  - Quick start guide
  - API documentation
  - Troubleshooting guide
  - Configuration reference
- MIT License for open-source distribution
- .gitignore file for source control
- Responsive web dashboard design
- Modern gradient UI with smooth animations
- Status indicators for temperature zones (Normal/Elevated/High)

### Features
- Temperature monitoring via iDRAC/IPMI
- Fan speed control via iDRAC command interface
- Background service for continuous monitoring
- Configurable check intervals (default 30 seconds)
- Web server hosting on configurable port (default 5000)
- C# ASP.NET Core 8.0 application
- Razor views with MVC pattern
- Dependency injection architecture
- Async/await patterns for IPMI operations
- Thread-safe state management
- Comprehensive logging with different levels
- Error recovery mechanisms

### Security
- iDRAC credential storage in configuration
- Security considerations documented
- Recommendations for file permissions
- HTTPS configuration examples
- Firewall and access control guidelines

### Integration
- REST API for external tool integration
- Examples for Prometheus, Grafana, Home Assistant
- JSON API with structured responses
- curl command examples

### Documentation
- Installation instructions for Linux (Ubuntu/Debian/RHEL/CentOS)
- Installation instructions for Windows
- Configuration guide with all options
- API documentation with examples
- Troubleshooting common issues
- Systemd service setup guide
- Windows service setup guide
- Security best practices
- Performance characteristics

[1.0.0]: https://github.com/biodland/IPMIFanControl/releases/tag/v1.0.0