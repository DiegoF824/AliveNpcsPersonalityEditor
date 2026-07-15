using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace AliveNpcsPersonalityEditor;

public sealed class ModEntry : Mod
{
    private PersonalityStore _store = null!;
    private PresetStore _presetStore = null!;
    private EditorConfig _config = null!;
    private IAliveNpcsApi? _api;
    private GalleryService? _galleryService;

    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<EditorConfig>();
        _store = new PersonalityStore(helper.DirectoryPath, Monitor);
        _store.Load();
        _presetStore = new PresetStore(helper.DirectoryPath, Monitor);

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
    }

    public override object GetApi() => new PersonalityEditorApi(this);

    public void OpenEditorMenu()
    {
        if (_api == null || !Context.IsWorldReady) return;
        Game1.activeClickableMenu = new PersonalityEditorMenu(
            _store, _presetStore, _api, _config, _galleryService, Monitor, Helper.Translation);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        _api = Helper.ModRegistry.GetApi<IAliveNpcsApi>("Lucas.AliveNpcs");
        if (_api is null)
        {
            Monitor.Log("AliveNpcs not found — personality editor cannot function.", LogLevel.Error);
            return;
        }

        try
        {
            if (!_api.RegisterCustomPersonalityDirectory(_store.OverridesDirectory))
                Monitor.Log("AliveNpcs did not accept the overrides directory path. The directory must be named 'overrides'.", LogLevel.Warn);
        }
        catch (Exception ex)
        {
            Monitor.Log($"AliveNpcs API error during directory registration ({ex.GetType().Name}): {ex.Message}", LogLevel.Trace);
        }

        if (_config.GalleryEnabled && !string.IsNullOrWhiteSpace(_config.GalleryServerUrl))
        {
            _galleryService = new GalleryService(_config.GalleryServerUrl);
            Monitor.Log($"Gallery service initialized — server: {_config.GalleryServerUrl}", LogLevel.Info);
        }

        RegisterGmcmConfig();

        Monitor.Log($"Personality Editor ready. Press {_config.OpenEditorKey} to open.", LogLevel.Info);
    }

    private void RegisterGmcmConfig()
    {
        try
        {
            var gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm is null) return;

            var modManifest = ModManifest;

            gmcm.Register(modManifest, reset: () =>
            {
                _config = Helper.ReadConfig<EditorConfig>();
            }, save: () =>
            {
                Helper.WriteConfig(_config);
                if (_config.GalleryEnabled && !string.IsNullOrWhiteSpace(_config.GalleryServerUrl))
                    _galleryService = new GalleryService(_config.GalleryServerUrl);
                else
                    _galleryService = null;
            });

            gmcm.AddSectionTitle(modManifest, () => Helper.Translation.Get("gallery.config.enabled"));
            gmcm.AddParagraph(modManifest, () => Helper.Translation.Get("gallery.config.description"));
            gmcm.AddBoolOption(modManifest,
                () => _config.GalleryEnabled,
                v => _config.GalleryEnabled = v,
                () => Helper.Translation.Get("gallery.config.enabled"),
                () => Helper.Translation.Get("gallery.config.enabled.tooltip"));

            gmcm.AddTextOption(modManifest,
                () => _config.GalleryServerUrl,
                v => _config.GalleryServerUrl = v,
                () => Helper.Translation.Get("gallery.config.server_url"),
                () => Helper.Translation.Get("gallery.config.server_url.tooltip"));
        }
        catch (Exception ex)
        {
            Monitor.Log($"GMCM registration failed: {ex.Message}", LogLevel.Trace);
        }
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (_api == null) return;
        if (!Context.IsWorldReady) return;
        if (e.Button != _config.OpenEditorKey) return;
        if (Game1.activeClickableMenu != null) return;

        Game1.activeClickableMenu = new PersonalityEditorMenu(
            _store, _presetStore, _api, _config, _galleryService, Monitor, Helper.Translation);
    }
}
