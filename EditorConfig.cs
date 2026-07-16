using StardewModdingAPI;

namespace AliveNpcsPersonalityEditor;

public sealed class EditorConfig
{
    public SButton OpenEditorKey { get; set; } = SButton.F10;
    public SButton OpenFarmerTabKey { get; set; } = SButton.F7;
    public bool OverrideCharacterSheet { get; set; } = true;
    public string GalleryServerUrl { get; set; } = "http://localhost:3000";
    public bool GalleryEnabled { get; set; } = true;
}
