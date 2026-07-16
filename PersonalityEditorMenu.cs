using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace AliveNpcsPersonalityEditor;

/// <summary>Main three-tab editor shown in the UI mockups.</summary>
public sealed class PersonalityEditorMenu : IClickableMenu
{
    private readonly PersonalityStore _store;
    private readonly PresetStore _presetStore;
    private readonly FarmerStore _farmerStore;
    private readonly IAliveNpcsApi _api;
    private readonly EditorConfig _config;
    private readonly GalleryService? _galleryService;
    private readonly IMonitor _monitor;
    private readonly ITranslationHelper _i18n;

    private readonly string[] _npcs;
    private readonly Dictionary<string, string> _defaults = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _displayNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D?> _portraits = new(StringComparer.OrdinalIgnoreCase);

    private readonly FarmerFormPanel _farmerPanel;
    private readonly GalleryPane? _galleryPane;
    private int _tab;
    private int _scrollY;
    private int _maxScroll;
    private Rectangle _npcGridArea;

    private const int HeaderTitleH = 70;
    private const int TopTabH = 46;
    private const int CardSize = 194;
    private const int CardRowGap = 64;
    private const int PortraitSourceSize = 64;

    private static readonly Color TabActive = new(235, 155, 45);
    private static readonly Color TabInactive = new(255, 240, 215);
    private static readonly Color CardBackground = new(255, 248, 234);
    private static readonly Color Border = new(125, 60, 40);

    public PersonalityEditorMenu(
        PersonalityStore store,
        PresetStore presetStore,
        FarmerStore farmerStore,
        IAliveNpcsApi api,
        EditorConfig config,
        GalleryService? galleryService,
        IMonitor monitor,
        ITranslationHelper i18n,
        int initialTab = 0)
        : base(0, 0, 0, 0)
    {
        _store = store;
        _presetStore = presetStore;
        _farmerStore = farmerStore;
        _api = api;
        _config = config;
        _galleryService = galleryService;
        _monitor = monitor;
        _i18n = i18n;
        _tab = Math.Clamp(initialTab, 0, 2);

        _npcs = GetEditableNpcNames(api);
        LoadNpcPresentationData();
        RecalculateLayout();

        _farmerPanel = new FarmerFormPanel(
            _farmerStore, _presetStore, _galleryService, _monitor, _i18n, GetFarmerContentArea, () => exitThisMenu());

        if (_config.GalleryEnabled && _galleryService != null)
        {
            _galleryPane = new GalleryPane(
                _galleryService,
                _store,
                _portraits,
                _i18n,
                _monitor,
                GetCatalogContentArea,
                _config.GalleryServerUrl,
                _presetStore,
                NotifyAliveNpcsReload);
        }
    }

    public int InitialTab => _tab;

    private static string[] GetEditableNpcNames(IAliveNpcsApi api)
    {
        try
        {
            return api.GetEditableNpcNames()
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        catch
        {
            return api.GetVanillaNpcNames()
                .Concat(api.GetSveNpcNames())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    private void LoadNpcPresentationData()
    {
        foreach (var npcName in _npcs)
        {
            try { _defaults[npcName] = _api.GetDefaultPersonality(npcName) ?? ""; }
            catch { _defaults[npcName] = ""; }

            try
            {
                var npc = Game1.getCharacterFromName(npcName);
                _displayNames[npcName] = npc?.displayName ?? npcName;
                _portraits[npcName] = npc?.Portrait ?? Game1.content.Load<Texture2D>($"Portraits/{npcName}");
            }
            catch
            {
                _displayNames[npcName] = npcName;
                _portraits[npcName] = null;
            }
        }
    }

    private void RecalculateLayout()
    {
        var viewport = Game1.uiViewport;
        width = Math.Min(1104, viewport.Width - 24);
        height = Math.Min(940, viewport.Height - 24);
        xPositionOnScreen = (viewport.Width - width) / 2;
        yPositionOnScreen = (viewport.Height - height) / 2;

        var top = yPositionOnScreen + HeaderTitleH + TopTabH + 60;
        _npcGridArea = new Rectangle(
            xPositionOnScreen + 40,
            top,
            width - 80,
            yPositionOnScreen + height - 44 - top);
        _scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, _maxScroll));
    }

    private Rectangle GetFarmerContentArea()
    {
        var top = yPositionOnScreen + HeaderTitleH + TopTabH + 48;
        return new Rectangle(xPositionOnScreen + 24, top, width - 48, yPositionOnScreen + height - 16 - top);
    }

    private Rectangle GetCatalogContentArea()
    {
        var top = yPositionOnScreen + HeaderTitleH + TopTabH + 44;
        return new Rectangle(xPositionOnScreen + 40, top, width - 80, yPositionOnScreen + height - 28 - top);
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect,
            new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
            Color.Black * 0.72f);
        drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        DrawTitle(b);
        DrawTopTabs(b);

        switch (_tab)
        {
            case 0:
                _farmerPanel.Draw(b);
                break;
            case 1:
                DrawNpcGrid(b);
                break;
            case 2 when _galleryPane != null:
                _galleryPane.Draw(b);
                break;
            case 2:
                DrawGalleryUnavailable(b);
                break;
        }

        drawMouse(b);
    }

    private void DrawTitle(SpriteBatch b)
    {
        var title = _i18n.Get("editor.title").ToString();
        var size = Game1.dialogueFont.MeasureString(title);
        Utility.drawTextWithShadow(b, title, Game1.dialogueFont,
            new Vector2(xPositionOnScreen + (width - size.X) / 2f, yPositionOnScreen + 18),
            Color.Black);
    }

    private void DrawTopTabs(SpriteBatch b)
    {
        var labels = new[]
        {
            _i18n.Get("tab.farmer").ToString(),
            _i18n.Get("tab.npcs").ToString(),
            _i18n.Get("tab.catalog").ToString()
        };

        for (var i = 0; i < labels.Length; i++)
        {
            var rect = GetTopTabRect(i);
            DrawButton(b, rect, labels[i], i == _tab ? TabActive : TabInactive, i == _tab ? Color.Black : Color.SaddleBrown);
        }
    }

    private Rectangle GetTopTabRect(int index)
    {
        var tabWidth = Math.Min(252, (width - 160) / 3);
        var y = yPositionOnScreen + HeaderTitleH + 14;
        return index switch
        {
            0 => new Rectangle(xPositionOnScreen + 40, y, tabWidth, TopTabH),
            1 => new Rectangle(xPositionOnScreen + (width - tabWidth) / 2, y, tabWidth, TopTabH),
            _ => new Rectangle(xPositionOnScreen + width - 40 - tabWidth, y, tabWidth, TopTabH)
        };
    }

    private void DrawNpcGrid(SpriteBatch b)
    {
        var layout = GetNpcGridLayout();
        var rows = (_npcs.Length + layout.Columns - 1) / layout.Columns;
        var totalHeight = Math.Max(0, rows * CardSize + Math.Max(0, rows - 1) * CardRowGap);
        _maxScroll = Math.Max(0, totalHeight - _npcGridArea.Height);
        _scrollY = Math.Clamp(_scrollY, 0, _maxScroll);

        var previousScissor = b.GraphicsDevice.ScissorRectangle;
        var previousRasterizer = b.GraphicsDevice.RasterizerState;
        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null,
            new RasterizerState { ScissorTestEnable = true });
        b.GraphicsDevice.ScissorRectangle = _npcGridArea;

        for (var i = 0; i < _npcs.Length; i++)
        {
            var rect = GetNpcCardRect(i, layout);
            if (rect.Bottom < _npcGridArea.Top || rect.Top > _npcGridArea.Bottom)
                continue;
            DrawNpcCard(b, _npcs[i], rect);
        }

        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, previousRasterizer);
        b.GraphicsDevice.ScissorRectangle = previousScissor;

        if (_maxScroll > 0)
            DrawScrollbar(b, _npcGridArea, _scrollY, _maxScroll);
    }

    private (int Columns, int Gap, int StartX) GetNpcGridLayout()
    {
        var columns = Math.Clamp((_npcGridArea.Width + 40) / (CardSize + 40), 1, 4);
        var gap = columns > 1 ? (_npcGridArea.Width - columns * CardSize) / (columns - 1) : 0;
        var used = columns * CardSize + Math.Max(0, columns - 1) * gap;
        return (columns, gap, _npcGridArea.X + (_npcGridArea.Width - used) / 2);
    }

    private Rectangle GetNpcCardRect(int index, (int Columns, int Gap, int StartX) layout)
    {
        var column = index % layout.Columns;
        var row = index / layout.Columns;
        return new Rectangle(
            layout.StartX + column * (CardSize + layout.Gap),
            _npcGridArea.Y + row * (CardSize + CardRowGap) - _scrollY,
            CardSize,
            CardSize);
    }

    private void DrawNpcCard(SpriteBatch b, string npcName, Rectangle rect)
    {
        b.Draw(Game1.staminaRect, rect, CardBackground);
        DrawBorder(b, rect, Border, 4);

        var displayName = _displayNames.GetValueOrDefault(npcName, npcName);
        var nameColor = _store.HasOverride(npcName) ? new Color(55, 120, 45) : Color.Black;
        var nameSize = Game1.smallFont.MeasureString(displayName);
        Utility.drawTextWithShadow(b, displayName, Game1.smallFont,
            new Vector2(rect.X + (rect.Width - nameSize.X) / 2f, rect.Y + 8), nameColor);

        var portraitSize = Math.Min(148, rect.Width - 24);
        var portraitRect = new Rectangle(rect.X + (rect.Width - portraitSize) / 2, rect.Bottom - portraitSize, portraitSize, portraitSize);
        var portrait = _portraits.GetValueOrDefault(npcName);
        if (portrait != null)
            b.Draw(portrait, portraitRect, new Rectangle(0, 0, PortraitSourceSize, PortraitSourceSize), Color.White);
        else
            b.Draw(Game1.staminaRect, portraitRect, Color.Black * 0.08f);
    }

    private void DrawGalleryUnavailable(SpriteBatch b)
    {
        var area = GetCatalogContentArea();
        var message = _i18n.Get("gallery.browse.empty").ToString();
        var size = Game1.smallFont.MeasureString(message);
        Utility.drawTextWithShadow(b, message, Game1.smallFont,
            new Vector2(area.X + (area.Width - size.X) / 2f, area.Y + 80), Color.SaddleBrown);
    }

    private static void DrawButton(SpriteBatch b, Rectangle rect, string label, Color background, Color text)
    {
        b.Draw(Game1.staminaRect, rect, background);
        DrawBorder(b, rect, Border, 4);
        var size = Game1.smallFont.MeasureString(label);
        Utility.drawTextWithShadow(b, label, Game1.smallFont,
            new Vector2(rect.X + (rect.Width - size.X) / 2f, rect.Y + (rect.Height - size.Y) / 2f), text);
    }

    internal static void DrawBorder(SpriteBatch b, Rectangle rect, Color color, int thickness)
    {
        b.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        b.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        b.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        b.Draw(Game1.staminaRect, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    internal static void DrawScrollbar(SpriteBatch b, Rectangle area, int scroll, int maxScroll)
    {
        var track = new Rectangle(area.Right + 14, area.Y, 16, area.Height);
        b.Draw(Game1.staminaRect, track, new Color(255, 246, 220));
        DrawBorder(b, track, Border, 1);
        var thumbHeight = Math.Max(58, track.Height * track.Height / (track.Height + maxScroll));
        var thumbY = track.Y + (int)((float)scroll / Math.Max(1, maxScroll) * (track.Height - thumbHeight));
        b.Draw(Game1.staminaRect, new Rectangle(track.X + 3, thumbY, track.Width - 6, thumbHeight), new Color(145, 65, 38));
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        for (var i = 0; i < 3; i++)
        {
            if (!GetTopTabRect(i).Contains(x, y))
                continue;
            SetTab(i);
            Game1.playSound("smallSelect");
            return;
        }

        if (_tab == 0)
        {
            _farmerPanel.receiveLeftClick(x, y);
            return;
        }

        if (_tab == 2)
        {
            _galleryPane?.receiveLeftClick(x, y);
            return;
        }

        var layout = GetNpcGridLayout();
        for (var i = 0; i < _npcs.Length; i++)
        {
            var rect = GetNpcCardRect(i, layout);
            if (!rect.Contains(x, y) || !_npcGridArea.Contains(x, y))
                continue;
            OpenNpcEditor(_npcs[i]);
            Game1.playSound("smallSelect");
            return;
        }
    }

    private void SetTab(int tab)
    {
        if (_tab == tab)
            return;
        _farmerPanel.Unsubscribe();
        _galleryPane?.Unsubscribe();
        _tab = tab;
        _scrollY = 0;
    }

    private void OpenNpcEditor(string npcName)
    {
        Game1.activeClickableMenu = new PersonalityEditModal(
            npcName,
            _displayNames.GetValueOrDefault(npcName, npcName),
            _defaults.GetValueOrDefault(npcName, ""),
            _portraits.GetValueOrDefault(npcName),
            _store,
            _presetStore,
            _api,
            _config,
            _galleryService,
            _monitor,
            _i18n,
            () => Game1.activeClickableMenu = this);
    }

    public override void receiveScrollWheelAction(int direction)
    {
        if (_tab == 0)
        {
            _farmerPanel.receiveScrollWheelAction(direction);
            return;
        }
        if (_tab == 2)
        {
            _galleryPane?.receiveScrollWheelAction(direction);
            return;
        }
        _scrollY = Math.Clamp(_scrollY - direction, 0, Math.Max(0, _maxScroll));
    }

    public override void receiveKeyPress(Keys key)
    {
        if (_tab == 0)
        {
            if (key == Keys.Escape)
            {
                _farmerPanel.Unsubscribe();
                exitThisMenu();
                return;
            }
            _farmerPanel.receiveKeyPress(key);
            return;
        }

        if (_tab == 2 && _galleryPane?.receiveKeyPress(key) == true)
            return;

        if (key == Keys.Escape)
            exitThisMenu();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        RecalculateLayout();
    }

    private void NotifyAliveNpcsReload()
    {
        try { _api.ReloadCustomPersonalities(); }
        catch (Exception ex) { _monitor.Log($"Reload notify failed: {ex.Message}", LogLevel.Warn); }
    }

    protected override void cleanupBeforeExit()
    {
        _farmerPanel.Unsubscribe();
        _galleryPane?.Unsubscribe();
        base.cleanupBeforeExit();
    }
}
