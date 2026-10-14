using System.Net.Http.Json;
using System.Runtime.InteropServices;

namespace ServerMonitor.Services;

public static class OdlandUpdateCheck
{
    public static string ApiBaseUrl { get; set; } =
        Environment.GetEnvironmentVariable("ODLAND_API_URL") ?? "https://api.odland.im";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public static async Task<UpdateInfo?> CheckForUpdateAsync(
        string app,
        string currentVersion,
        string channel = "stable",
        CancellationToken ct = default)
    {
        var url = $"{ApiBaseUrl.TrimEnd('/')}/api/v1/apps/{Uri.EscapeDataString(app)}/latest" +
                  $"?channel={Uri.EscapeDataString(channel)}&current={Uri.EscapeDataString(currentVersion)}";
        return await Http.GetFromJsonAsync<UpdateInfo>(url, ct);
    }

    public static async Task ReportRunAsync(string app, string version, CancellationToken ct = default)
    {
        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos" : "unknown";

        using var response = await Http.PostAsJsonAsync(
            $"{ApiBaseUrl.TrimEnd('/')}/api/v1/events",
            new { app, version, os }, ct);
        response.EnsureSuccessStatusCode();
    }

    public sealed record UpdateInfo(
        string App,
        string Channel,
        string? Current,
        string Latest,
        bool UpdateAvailable,
        string DownloadUrl,
        string Notes,
        bool Mandatory);
}
