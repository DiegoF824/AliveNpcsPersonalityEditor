using System.Text.Json;
using System.Text.Json.Serialization;

namespace AliveNpcsPersonalityEditor;

/// <summary>Shared JSON settings for local personality overrides and presets.</summary>
internal static class EditorJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
