using System.Text.Json;
using AliveNpcsPersonalityEditor.Models;
using StardewModdingAPI;

namespace AliveNpcsPersonalityEditor;

/// <summary>Reads and writes the custom_personalities.json file.</summary>
public sealed class PersonalityStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;
    private readonly IMonitor _monitor;

    /// <summary>Current custom overrides (NPC name -> personality text). Only contains edited NPCs.</summary>
    public Dictionary<string, string> Overrides { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Absolute path of the data file consumed by AliveNpcs.</summary>
    public string FilePath => _filePath;

    public PersonalityStore(string modDirectoryPath, IMonitor monitor)
    {
        _filePath = Path.Combine(modDirectoryPath, "custom_personalities.json");
        _monitor = monitor;
    }

    public void Load()
    {
        if (!File.Exists(_filePath))
        {
            Overrides = new(StringComparer.OrdinalIgnoreCase);
            return;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<CustomPersonalityData>(json, JsonOptions);
            Overrides = data?.Personalities?
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && !string.IsNullOrWhiteSpace(entry.Value))
                .ToDictionary(entry => entry.Key.Trim(), entry => entry.Value.Trim(), StringComparer.OrdinalIgnoreCase)
                ?? new(StringComparer.OrdinalIgnoreCase);
            _monitor.Log($"Loaded {Overrides.Count} custom personality override(s).", LogLevel.Info);
        }
        catch (Exception ex)
        {
            _monitor.Log($"Failed to load custom personalities: {ex.Message}", LogLevel.Warn);
            Overrides = new(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void Save()
    {
        try
        {
            var data = new CustomPersonalityData
            {
                SchemaVersion = 1,
                LastModified = DateTime.UtcNow.ToString("o"),
                Personalities = new Dictionary<string, string>(Overrides, StringComparer.OrdinalIgnoreCase)
            };
            var json = JsonSerializer.Serialize(data, JsonOptions);
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
            _monitor.Log($"Saved {Overrides.Count} custom personality override(s).", LogLevel.Info);
        }
        catch (Exception ex)
        {
            TryDeleteTempFile();
            _monitor.Log($"Failed to save custom personalities: {ex.Message}", LogLevel.Error);
        }
    }

    /// <summary>Set or clear a custom override. Pass null to remove.</summary>
    public void Set(string npcName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            Overrides.Remove(npcName);
        else
            Overrides[npcName] = value;
    }

    /// <summary>Get the custom override for an NPC, or null if not overridden.</summary>
    public string? Get(string npcName)
    {
        return Overrides.TryGetValue(npcName, out var val) ? val : null;
    }

    public bool HasOverride(string npcName) => Overrides.ContainsKey(npcName);

    private void TryDeleteTempFile()
    {
        try
        {
            var tempPath = _filePath + ".tmp";
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
        catch
        {
            // The original file is still intact; a future save can replace the temp file.
        }
    }
}
