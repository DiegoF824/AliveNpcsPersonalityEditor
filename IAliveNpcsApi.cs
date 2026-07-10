namespace AliveNpcsPersonalityEditor;

/// <summary>
/// Public API exposed by AliveNpcs for inter-mod communication.
/// The editor uses this to fetch default personality data.
/// </summary>
public interface IAliveNpcsApi
{
    /// <summary>Get the default personality for an NPC, without a saved editor override.</summary>
    string GetDefaultPersonality(string npcName);

    /// <summary>Get all vanilla NPC names that have defined personalities.</summary>
    IEnumerable<string> GetVanillaNpcNames();

    /// <summary>Get all SVE NPC names that have defined personalities.</summary>
    IEnumerable<string> GetSveNpcNames();

    /// <summary>
    /// Get NPCs that the current AliveNpcs save allows the editor to customize.
    /// This respects compatibility settings and community AI opt-out rules.
    /// </summary>
    IEnumerable<string> GetEditableNpcNames();

    /// <summary>Check if a personality override is active for this NPC.</summary>
    bool HasCustomPersonality(string npcName);

    /// <summary>Register this editor instance's data file with AliveNpcs.</summary>
    bool RegisterCustomPersonalityFile(string filePath);

    /// <summary>Reload custom personalities from disk (called after editor saves).</summary>
    void ReloadCustomPersonalities();
}
