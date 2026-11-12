namespace PowerHistory.Infrastructure.Discovery;

using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using PowerHistory.Core.Interfaces;
using PowerHistory.Core.Models;

/// <summary>
/// Discovers history files by asking the installed PowerShell hosts for their real
/// <c>Get-PSReadLineOption</c> configuration.
/// </summary>
/// <remarks>
/// The host profile is loaded on purpose: a custom <c>HistorySavePath</c> is normally set there,
/// and asking a host that skipped its profile would report the default path instead of the real one.
/// Hosts that are missing, fail to start or have no PSReadLine are skipped, never fatal.
/// </remarks>
public sealed class PowerShellHistoryDiscovery : IHistoryDiscovery
{
    private const string Script =
        "$ErrorActionPreference='Stop';" +
        "Import-Module PSReadLine -ErrorAction Stop;" +
        "$o=Get-PSReadLineOption;" +
        "[pscustomobject]@{p=[string]$o.HistorySavePath;s=[string]$o.HistorySaveStyle;m=[int]$o.MaximumHistoryCount}" +
        "|ConvertTo-Json -Compress";

    private static readonly (string Host, string Executable)[] Hosts =
    [
        ("PowerShell 7", "pwsh"),
        ("Windows PowerShell 5.1", "powershell.exe"),
    ];

    private readonly TimeSpan _timeout;

    public PowerShellHistoryDiscovery(TimeSpan? timeout = null)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(20);
    }

    public IReadOnlyList<DiscoveredHistory> Discover()
    {
        var byPath = new Dictionary<string, DiscoveredHistory>(StringComparer.OrdinalIgnoreCase);

        foreach (var (host, executable) in Hosts)
        {
            var options = Query(executable, loadProfile: true) ?? Query(executable, loadProfile: false);
            if (options is null || string.IsNullOrWhiteSpace(options.Value.Path))
            {
                continue;
            }

            var path = Path.GetFullPath(options.Value.Path);
            if (byPath.TryGetValue(path, out var existing))
            {
                byPath[path] = existing with { Host = $"{existing.Host}, {host}" };
            }
            else
            {
                byPath[path] = new DiscoveredHistory(host, path, options.Value.SaveStyle, options.Value.MaximumHistoryCount);
            }
        }

        return byPath.Values.ToList();
    }

    private (string Path, string? SaveStyle, int? MaximumHistoryCount)? Query(string executable, bool loadProfile)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (!loadProfile)
        {
            startInfo.ArgumentList.Add("-NoProfile");
        }

        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(Script);

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            // The output is read asynchronously so a host that hangs cannot block the timeout.
            var output = process.StandardOutput.ReadToEndAsync();

            if (!process.WaitForExit((int)_timeout.TotalMilliseconds))
            {
                TryKill(process);
                return null;
            }

            // Without this call the redirected output may still be buffered.
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                return null;
            }

            return Parse(output.GetAwaiter().GetResult());
        }
        catch (Win32Exception)
        {
            // The host is not installed.
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static (string Path, string? SaveStyle, int? MaximumHistoryCount)? Parse(string output)
    {
        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');
        if (start < 0 || end < start)
        {
            return null;
        }

        using var document = JsonDocument.Parse(output[start..(end + 1)]);
        var root = document.RootElement;

        if (!root.TryGetProperty("p", out var pathElement) || pathElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var path = pathElement.GetString();
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string? saveStyle = null;
        if (root.TryGetProperty("s", out var styleElement) && styleElement.ValueKind == JsonValueKind.String)
        {
            saveStyle = styleElement.GetString();
        }

        int? maximum = null;
        if (root.TryGetProperty("m", out var maximumElement) && maximumElement.ValueKind == JsonValueKind.Number)
        {
            maximum = maximumElement.GetInt32();
        }

        return (path, saveStyle, maximum);
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (SystemException)
        {
            // The process already exited.
        }
    }
}
