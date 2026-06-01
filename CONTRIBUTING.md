# Contributing to IPMIFanControl

Thank you for your interest in contributing to IPMIFanControl! This document provides guidelines and instructions for contributing to the project.

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

- .NET 8.0 SDK
- Git
- A text editor or IDE (Visual Studio, VS Code, Rider, etc.)
- IPMI-capable server for testing (optional but recommended)

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
   dotnet run
   ```

6. **Test Locally**
   - Configure your iDRAC settings in `appsettings.json`
   - Access the web dashboard at `http://localhost:5000`
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
dotnet run
```

### 4. Commit Your Changes

Use clear, descriptive commit messages:

```bash
git add .
git commit -m "feat: add Prometheus metrics endpoint"
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
feat(api): add endpoint for historical temperature data

fix(ipmi): resolve connection timeout issue

docs(readme): update installation instructions for Ubuntu 22.04

refactor(services): extract common IPMI logic into base class
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

- **Classes**: `PascalCase` (e.g., `IPMIService`)
- **Methods**: `PascalCase` (e.g., `GetTemperaturesAsync`)
- **Properties**: `PascalCase` (e.g., `HighestTempCelsius`)
- **Local variables**: `camelCase` (e.g., `highestTemp`)
- **Constants**: `PascalCase` (e.g., `MaxRetries`)
- **Private fields**: `_camelCase` (e.g., `_logger`)

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
   public async Task<TemperatureStatus> GetTemperaturesAsync()
   {
       // ...
   }
   ```

4. **Error Handling**: Always include proper error handling

   ```csharp
   try
   {
       // Code that might fail
   }
   catch (Exception ex)
   {
       _logger.LogError(ex, "Failed to do something");
       throw;
   }
   ```

5. **Comments**: Use XML documentation for public APIs

   ```csharp
   /// <summary>
   /// Gets current temperature readings from iDRAC
   /// </summary>
   /// <param name="cancellationToken">Cancellation token</param>
   /// <returns>Temperature status with highest temperature</returns>
   public async Task<TemperatureStatus> GetTemperaturesAsync(CancellationToken cancellationToken = default)
   {
       // ...
   }
   ```

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
public async Task GetTemperaturesAsync_ReturnsCorrectHighestTemp()
{
    // Arrange
    var service = new IPMIService();

    // Act
    var result = await service.GetTemperaturesAsync();

    // Assert
    Assert.NotNull(result);
    Assert.True(result.HighestTempCelsius >= 0);
}
```

### Integration Tests

Test the application end-to-end:

```bash
# Run the application
dotnet run

# Test endpoints
curl http://localhost:5000/api/status
curl -X POST http://localhost:5000/api/control/manual -H "Content-Type: application/json" -d '{"speed": 20}'
```

### Manual Testing Checklist

Before submitting a PR, test:

- [ ] Application builds without errors
- [ ] Web dashboard loads correctly
- [ ] Temperature monitoring works
- [ ] Fan control responds correctly
- [ ] API endpoints return expected results
- [ ] Error handling works as expected
- [ ] Configuration is properly validated
- [ ] Logging captures appropriate information

---

## 📤 Submitting Changes

### Pull Request Checklist

Before submitting a PR, ensure:

- [ ] Your code follows the coding standards
- [ ] Code compiles without warnings
- [ ] Tests pass successfully
- [ ] Documentation is updated (README, API docs, etc.)
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

1. **Search existing issues** - Check if the issue has already been reported
2. **Check documentation** - Review README and other docs
3. **Test with latest version** - Ensure the issue exists in the latest release

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
- .NET Version: [e.g., 8.0.2]
- Server Model: [e.g., Dell R720 XD]
- Application Version: [e.g., 1.0.0]

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

---

## 📝 Documentation

### Writing Documentation

- Keep it simple and clear
- Use code examples where appropriate
- Include troubleshooting steps
- Update diagrams/screenshots when UI changes

### Where to Document

- **README.md** - Project overview, quick start, main features
- **API docs** - In-code XML documentation
- **Wiki** - Detailed guides, tutorials, advanced topics
- **CHANGELOG.md** - Version history

---

## 🎯 Areas to Contribute

Looking for something to work on? Here are some ideas:

### Easy (Good for First-Time Contributors)
- Improve documentation
- Add more examples to README
- Fix small UI bugs
- Add unit tests

### Medium
- Add new temperature threshold options
- Improve error messages
- Add more configuration options
- Enhance logging

### Advanced
- Add Prometheus metrics export
- Implement authentication/authorization
- Add support for multiple servers
- Create mobile app or PWA
- Add machine learning for fan optimization

---

## 📞 Getting Help

- Check existing [issues](https://github.com/biodland/IPMIFanControl/issues)
- Start a [discussion](https://github.com/biodland/IPMIFanControl/discussions)
- Ask questions in PR comments

---

## 🙏 Thank You

Thank you for contributing to IPMIFanControl! Your contributions help make this project better for everyone.

---

For more information, see:
- [Project README](README.md)
- [License](LICENSE)
- [MIT License](https://choosealicense.com/licenses/mit/)