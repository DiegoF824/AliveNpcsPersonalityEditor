using StardewModdingAPI;

namespace AliveNpcsPersonalityEditor;

public sealed class EditorConfig
{
    public SButton OpenEditorKey { get; set; } = SButton.F10;
    public string GalleryServerUrl { get; set; } = "http://localhost:3000";
    public bool GalleryEnabled { get; set; } = true;
}
