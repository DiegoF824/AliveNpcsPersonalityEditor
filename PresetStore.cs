using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AliveNpcsPersonalityEditor.Models;
using StardewModdingAPI;

namespace AliveNpcsPersonalityEditor;

public sealed class PresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string PresetsDirName = "presets";
    private readonly string _presetsDir;
    private readonly IMonitor _monitor;

    public string PresetsDirectory => _presetsDir;

    public PresetStore(string modDirectoryPath, IMonitor monitor)
    {
        _presetsDir = Path.Combine(modDirectoryPath, PresetsDirName);
        _monitor = monitor;
    }

    public Dictionary<string, NpcOverrideEntry> LoadAll()
    {
        var dict = new Dictionary<string, NpcOverrideEntry>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(_presetsDir))
            return dict;

        foreach (var file in Directory.EnumerateFiles(_presetsDir, "*.json"))
        {
            var npcName = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(npcName))
                continue;

            try
            {
                var json = File.ReadAllText(file);
                var entry = JsonSerializer.Deserialize<NpcOverrideEntry>(json, JsonOptions);
                if (entry != null && entry.HasAnyField)
                {
                    entry.NpcName = npcName;
                    dict[npcName] = entry;
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to read preset file '{Path.GetFileName(file)}': {ex.Message}", LogLevel.Warn);
            }
        }

        return dict;
    }

    public NpcOverrideEntry? Get(string npcName)
    {
        var filePath = Path.Combine(_presetsDir, $"{npcName}.json");
        if (!File.Exists(filePath)) return null;

        try
        {
            var json = File.ReadAllText(filePath);
            var entry = JsonSerializer.Deserialize<NpcOverrideEntry>(json, JsonOptions);
            if (entry != null) entry.NpcName = npcName;
            return entry;
        }
        catch { return null; }
    }

    public void Save(string npcName, NpcOverrideEntry data)
    {
        Directory.CreateDirectory(_presetsDir);
        data.NpcName = npcName;
        var filePath = Path.Combine(_presetsDir, $"{npcName}.json");
        var json = JsonSerializer.Serialize(data, JsonOptions);
        var tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, filePath, overwrite: true);
    }

    public void Delete(string npcName)
    {
        var filePath = Path.Combine(_presetsDir, $"{npcName}.json");
        try { if (File.Exists(filePath)) File.Delete(filePath); }
        catch { }
    }

    public bool HasPreset(string npcName)
    {
        return File.Exists(Path.Combine(_presetsDir, $"{npcName}.json"));
    }
}
