# Contributing to ServerMonitor

Thank you for your interest in contributing to ServerMonitor! This document provides guidelines and instructions for contributing to the project.

---

## 📋 Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Coding Standards](#coding-standards)
- [Testing](#testing)
- [Submitting Changes](#submitting-changes)
- [Reporting Issues](#reporting-issues)

---

## 🤝 Code of Conduct

By participating in this project, you agree to uphold the following standards:

- Be respectful and inclusive
- Welcome newcomers and help them learn
- Focus on what is best for the community
- Show empathy towards other community members

---

## 🚀 Getting Started

### Prerequisites

- .NET 10.0 SDK
- Git
- A text editor or IDE (Visual Studio, VS Code, Rider, etc.)
- IPMI-capable server for testing (optional but recommended)
- Linux environment (ipmitool, lm-sensors for full functionality)

### Setting Up Development Environment

1. **Fork the Repository**
   ```bash
   # Fork the repository on GitHub, then clone your fork
   git clone https://github.com/YOUR_USERNAME/IPMIFanControl.git
   cd IPMIFanControl
   ```

2. **Add Upstream Remote**
   ```bash
   git remote add upstream https://github.com/biodland/IPMIFanControl.git
   ```

3. **Install Dependencies**
   ```bash
   dotnet restore
   ```

4. **Build the Project**
   ```bash
   dotnet build
   ```

5. **Run the Application**
   ```bash
   # Requires root for ipmitool access on Linux
   sudo dotnet run --project src/Apps/ServerMonitor
   ```

6. **Test Locally**
   - Copy `appsettings.json` to `appsettings.local.json` and configure your iDRAC/IPMI settings (the local file is git-ignored)
   - Access the web dashboard at `http://localhost:5000`
   - Use the StatsCheck tool for quick system stats verification: `dotnet run --project src/Tools/StatsCheck`
   - Test the API endpoints using curl or Postman

---

## 🔄 Development Workflow

### 1. Create a Branch

Always create a new branch for your changes:

```bash
git checkout -b feature/your-feature-name
# or
git checkout -b fix/your-bug-fix
```

### Branch Naming Conventions

- `feature/` - New features
- `fix/` - Bug fixes
- `docs/` - Documentation changes
- `refactor/` - Code refactoring
- `test/` - Test additions or changes
- `chore/` - Maintenance tasks

### 2. Make Your Changes

- Write clean, maintainable code
- Follow the coding standards below
- Add comments for complex logic
- Update documentation as needed

### 3. Test Your Changes

```bash
# Build
dotnet build

# Run tests (if applicable)
dotnet test

# Manual testing
sudo dotnet run --project src/Apps/ServerMonitor
```

### 4. Commit Your Changes

Use clear, descriptive commit messages:

```bash
git add .
git commit -m "feat: add SuperMicro server provider"
```

#### Commit Message Format

Follow the conventional commits format:

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types**:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting, etc.)
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

**Examples**:
```
feat(providers): add SuperMicro server provider with IPMI support

fix(ipmi): resolve connection timeout for out-of-band client

docs(readme): update provider architecture diagram

refactor(services): extract common monitoring logic into ServerProviderBase
```

### 5. Sync with Upstream

Before pushing, sync with the main repository:

```bash
git fetch upstream
git rebase upstream/main
```

### 6. Push to Your Fork

```bash
git push origin feature/your-feature-name
```

### 7. Create Pull Request

- Go to the repository on GitHub
- Click "New Pull Request"
- Select your branch
- Provide a clear description of your changes
- Link any related issues

---

## 📐 Coding Standards

### C# Coding Conventions

Follow the official C# coding conventions: https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions

#### Naming Conventions

- **Classes**: `PascalCase` (e.g., `DellServerProvider`)
- **Methods**: `PascalCase` (e.g., `GetTemperaturesAsync`)
- **Properties**: `PascalCase` (e.g., `MaxTemperature`)
- **Local variables**: `camelCase` (e.g., `highestTemp`)
- **Constants**: `PascalCase` (e.g., `MinSpeedPercentage`)
- **Private fields**: `_camelCase` (e.g., `_logger`)
- **Interfaces**: `IPascalCase` (e.g., `IServerProvider`, `IFanController`)

#### Code Style

1. **Indentation**: Use spaces, 4 spaces per indent

2. **Braces**: Opening brace on new line (Allman style)

   ```csharp
   public void ExampleMethod()
   {
       if (condition)
       {
           DoSomething();
       }
   }
   ```

3. **Async/Await**: Always use `Async` suffix for async methods

   ```csharp
   public async Task<ServerMetrics> CollectMetricsAsync()
   {
       // ...
   }
   ```

4. **Error Handling**: Always include proper error handling

   ```csharp
   try
   {
       var result = await _ipmiClient.ExecuteRawAsync(command);
   }
   catch (Exception ex)
   {
       _logger.LogError(ex, "Failed to execute IPMI command");
       throw;
   }
   ```

5. **Comments**: Use XML documentation for public APIs

   ```csharp
   /// <summary>
   /// Gets current temperature readings from the server provider
   /// </summary>
   /// <param name="cancellationToken">Cancellation token</param>
   /// <returns>List of temperature readings with optional core temperatures</returns>
   public async Task<List<TemperatureReading>> GetTemperaturesAsync(CancellationToken cancellationToken = default)
   {
       // ...
   }
   ```

### Provider Architecture

ServerMonitor uses a provider pattern for multi-vendor support. When adding support for a new server vendor:

1. Create a new folder under `src/Apps/ServerMonitor/Providers/{VendorName}/`
2. Implement `IServerProviderCandidate` for auto-detection (reads DMI/SMBIOS to identify vendor)
3. Implement `IServerProvider` as the main provider class with `DisplayName` and `InitializeAsync`
4. Implement the relevant sub-interfaces as needed:
   - `ITemperatureMonitor` (set `SupportsCoreTemperatures` if lm-sensors is used)
   - `IFanController`
   - `IPowerMonitor`
   - `IGpuMonitor`
5. Inherit from `ServerProviderBase` for common logging and service resolution
6. Register the provider candidate in `Program.cs` before `builder.Build()`
7. Add the vendor to the `ServerVendor` enum in `Core/Enums/ServerVendor.cs`

### JavaScript/HTML/CSS Conventions

1. **JavaScript**: Use camelCase for variables and functions
2. **CSS**: Use kebab-case for class names
3. **HTML**: Use semantic HTML5 elements
4. **Indentation**: 2 spaces for HTML/CSS/JavaScript

---

## 🧪 Testing

### Unit Tests

Add unit tests for new functionality:

```csharp
[Fact]
public async Task GetTemperaturesAsync_ReturnsReadingsWithStatus()
{
    // Arrange
    var provider = new DellTemperatureMonitor(ipmiClient, lmSensorsParser, logger);

    // Act
    var result = await provider.GetTemperaturesAsync();

    // Assert
    Assert.NotNull(result);
    Assert.All(result, r => Assert.True(r.Celsius >= 0));
    Assert.All(result, r => Assert.NotNull(r.Status));
}
```

### Integration Tests

Test the application end-to-end:

```bash
# Run the application
sudo dotnet run --project src/Apps/ServerMonitor

# Test endpoints
curl http://localhost:5000/api/status
curl http://localhost:5000/api/provider
curl http://localhost:5000/api/stats
curl http://localhost:5000/api/stats/history
curl -X POST http://localhost:5000/api/fans/speed/30
curl -X POST http://localhost:5000/api/fans/auto

# Test with StatsCheck tool
dotnet run --project src/Tools/StatsCheck
```

### Manual Testing Checklist

Before submitting a PR, test:

- [ ] Application builds without errors (`dotnet build`)
- [ ] Web dashboard loads correctly
- [ ] System Stats page loads and displays CPU, memory, network, storage
- [ ] Temperature monitoring works (IPMI + lm-sensors core temps)
- [ ] Fan control responds correctly (manual speed and auto mode)
- [ ] GPU monitoring displays data (if applicable GPUs are present)
- [ ] API endpoints return expected results
- [ ] Provider auto-detection works correctly
- [ ] Error handling works as expected (e.g., ipmitool unavailable)
- [ ] Configuration is properly validated
- [ ] Logging captures appropriate information

---

## 📬 Submitting Changes

### Pull Request Checklist

Before submitting a PR, ensure:

- [ ] Your code follows the coding standards
- [ ] Code compiles without warnings
- [ ] Tests pass successfully
- [ ] Documentation is updated (README, DESIGN, CHANGELOG, etc.)
- [ ] Commit messages follow the conventional format
- [ ] Branch is up to date with upstream/main
- [ ] PR description clearly explains the changes
- [ ] Related issues are referenced

### Pull Request Template

When creating a PR, use this template:

```markdown
## Description
Brief description of what this PR changes and why.

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Testing
Describe how you tested this change.

## Screenshots (if applicable)
Add screenshots for UI changes.

## Checklist
- [ ] Code follows project style guidelines
- [ ] Self-review completed
- [ ] Code compiles without warnings
- [ ] Tests added/updated
- [ ] Documentation updated
```

---

## 🐛 Reporting Issues

### Before Creating an Issue

1. **Search existing issues** — Check if the issue has already been reported
2. **Check documentation** — Review README and DESIGN docs
3. **Test with latest version** — Ensure the issue exists in the latest release

### Issue Template

When creating an issue, include:

```markdown
## Description
Clear description of the issue

## Steps to Reproduce
1. Step one
2. Step two
3. Step three

## Expected Behavior
What you expected to happen

## Actual Behavior
What actually happened

## Environment
- OS: [e.g., Ubuntu 22.04]
- .NET Version: [e.g., 10.0]
- Server Model: [e.g., Dell PowerEdge R740XD]
- Provider: [e.g., Dell / auto-detected]
- Application Version: [e.g., 2.0.0]

## Logs
Relevant log messages or console output

## Additional Context
Any other relevant information, screenshots, etc.
```

---

## 💡 Feature Requests

We welcome feature requests! When requesting a feature:

1. **Clearly describe** the feature and its use case
2. **Explain** how it would benefit users
3. **Consider** whether you're willing to implement it
4. **Discuss** the implementation approach if possible

### Current Development Priorities

The project roadmap is tracked in DESIGN.md. Current focus areas:

- **Phase 3**: Additional vendor providers (SuperMicro, HPE, Lenovo)
- **Phase 4**: Thermal curve fan profiles
- **Phase 5**: Persistent configuration storage
- **Phase 6**: Native IPMI library

Contributions aligned with these phases are especially welcome.

---

## 📝 Documentation

### Writing Documentation

- Keep it simple and clear
- Use code examples where appropriate
- Include troubleshooting steps
- Update diagrams/screenshots when UI changes
- Ensure documentation reflects actual code (not aspirational features)

### Where to Document

- **README.md** — Project overview, quick start, main features, API reference
- **DESIGN.md** — Architecture, interfaces, data models, configuration schema, roadmap
- **CHANGELOG.md** — Version history with breaking changes noted
- **In-code XML documentation** — Public API documentation

---

## 🎯 Areas to Contribute

Looking for something to work on? Here are some ideas:

### Easy (Good for First-Time Contributors)
- Improve documentation
- Add more API examples to README
- Fix small UI bugs on dashboard or stats pages
- Add unit tests for existing providers

### Medium
- Add a new server vendor provider (SuperMicro, HPE, etc.)
- Improve error messages and logging
- Add more configuration options for fan control
- Enhance system stats collection

### Advanced
- Add Prometheus metrics export
- Implement thermal curve fan profiles
- Build a native IPMI library (replace ipmitool CLI wrapper)
- Add authentication/authorization
- Create mobile app or PWA

---

## 📞 Getting Help

- Check existing [issues](https://github.com/biodland/IPMIFanControl/issues)
- Start a [discussion](https://github.com/biodland/IPMIFanControl/discussions)
- Ask questions in PR comments

---

## 🙏 Thank You

Thank you for contributing to ServerMonitor! Your contributions help make this project better for everyone.

---

For more information, see:
- [Project README](README.md)
- [Design Document](DESIGN.md)
- [Changelog](CHANGELOG.md)
- [License](LICENSE)
- [MIT License](https://choosealicens.com/licenses/mit/)
