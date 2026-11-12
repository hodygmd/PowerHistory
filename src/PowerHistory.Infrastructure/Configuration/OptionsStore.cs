namespace PowerHistory.Infrastructure.Configuration;

using System.Text.Json;

/// <summary>
/// Loads and saves <see cref="PowerHistoryOptions"/> from a JSON file in the user configuration folder.
/// </summary>
public sealed class OptionsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _path;

    public OptionsStore(string? path = null)
    {
        _path = path ?? DefaultPath;
    }

    /// <summary>Default configuration file location.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PowerHistory",
        "config.json");

    /// <summary>
    /// Reads the configuration. A missing or unreadable file yields the defaults instead of an error.
    /// </summary>
    public PowerHistoryOptions Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new PowerHistoryOptions();
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<PowerHistoryOptions>(json, SerializerOptions) ?? new PowerHistoryOptions();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new PowerHistoryOptions();
        }
    }

    public void Save(PowerHistoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(options, SerializerOptions));
    }
}
