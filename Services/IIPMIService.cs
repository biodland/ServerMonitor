namespace DellFanControl.Services;

/// <summary>
/// Interface for IPMI service implementations
/// </summary>
public interface IIPMIService : IDisposable
{
    Task<List<TemperatureReading>> GetTemperaturesAsync();
    Task<List<FanReading>> GetFanStatusAsync();
    Task<bool> SetFanSpeedAsync(int percentage);
    Task<bool> RestoreDynamicControlAsync();
    Task<bool> TestConnectionAsync();
}

public record TemperatureReading
{
    public string Name { get; init; } = string.Empty;
    public double Value { get; init; }
    public string Unit { get; init; } = string.Empty;
}

public record FanReading
{
    public string Name { get; init; } = string.Empty;
    public int SpeedRPM { get; init; }
}