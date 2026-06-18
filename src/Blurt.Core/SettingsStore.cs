using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Blurt.Core;

/// <summary>
/// Persists Blurt's settings under <c>&lt;appDataRoot&gt;\Blurt</c>: the
/// non-secret <see cref="BlurtConfig"/> as readable JSON (<c>config.json</c>),
/// and the API key separately in an encrypted blob (<c>apikey.dat</c>) via the
/// injected <see cref="ISecretProtector"/> — never in plaintext. The app-data
/// root is injected (like <see cref="ModelProvisioner"/>) so tests can point it
/// at a temp directory and never touch real <c>%APPDATA%</c>.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _directory;
    private readonly ISecretProtector _protector;

    public SettingsStore(string appDataRoot, ISecretProtector protector)
    {
        _directory = Path.Combine(appDataRoot, AppInfo.Name);
        _protector = protector;
    }

    /// <summary>Absolute path of the human-readable JSON config file.</summary>
    public string ConfigPath => Path.Combine(_directory, "config.json");

    /// <summary>Absolute path of the DPAPI-encrypted API-key blob (separate from the config).</summary>
    public string ApiKeyPath => Path.Combine(_directory, "apikey.dat");

    /// <summary>
    /// Reads the persisted config, or returns <see cref="BlurtConfig.Default"/>
    /// when no config file exists yet (first run).
    /// </summary>
    public BlurtConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return BlurtConfig.Default;

        var json = File.ReadAllText(ConfigPath);
        try
        {
            return JsonSerializer.Deserialize<BlurtConfig>(json, JsonOptions) ?? BlurtConfig.Default;
        }
        catch (JsonException)
        {
            // F19/F20: a malformed config.json (a crash/power-loss mid-save, or a
            // tampered file) or an out-of-range enum string would otherwise throw an
            // uncaught JsonException and crash every startup. Fall back to defaults
            // so the app still launches — the user can re-save from Settings.
            return BlurtConfig.Default;
        }
    }

    /// <summary>Writes <paramref name="config"/> as indented JSON, creating the directory if needed.</summary>
    public void Save(BlurtConfig config)
    {
        Directory.CreateDirectory(_directory);
        var json = JsonSerializer.Serialize(config, JsonOptions);

        // F19: write atomically — a crash/power-loss partway through a direct write
        // would leave a truncated config.json that fails to parse on next launch.
        // Write to a temp file, then move it over the target in one step.
        var temp = ConfigPath + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, ConfigPath, overwrite: true);
    }

    /// <summary>
    /// Encrypts <paramref name="apiKey"/> via the secret protector and writes it
    /// to <see cref="ApiKeyPath"/> — never in plaintext.
    /// </summary>
    public void SaveApiKey(string apiKey)
    {
        Directory.CreateDirectory(_directory);
        var cipher = _protector.Protect(Encoding.UTF8.GetBytes(apiKey));
        File.WriteAllBytes(ApiKeyPath, cipher);
    }

    /// <summary>
    /// Decrypts and returns the stored API key, or <c>null</c> when none has been
    /// saved yet.
    /// </summary>
    public string? LoadApiKey()
    {
        if (!File.Exists(ApiKeyPath))
            return null;

        var cipher = File.ReadAllBytes(ApiKeyPath);
        return Encoding.UTF8.GetString(_protector.Unprotect(cipher));
    }
}
