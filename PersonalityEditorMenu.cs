using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace AliveNpcsPersonalityEditor;

public class PersonalityEditorMenu : IClickableMenu
{
    private readonly PersonalityStore _store;
    private readonly PresetStore _presetStore;
    private readonly IAliveNpcsApi _api;
    private readonly EditorConfig _config;
    private readonly GalleryService? _galleryService;
    private readonly IMonitor _monitor;
    private readonly ITranslationHelper _i18n;
    private readonly Dictionary<string, string> _defaults = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D?> _portraits = new(StringComparer.OrdinalIgnoreCase);

    private readonly (string Key, string[] Npcs)[] _categories;
    private int _activeTab;
    private int _topLevelTab; // 0 = Personalities, 1 = Gallery
    private GalleryPane? _galleryPane;

    private static readonly HashSet<string> KnownBachelors = new(StringComparer.OrdinalIgnoreCase)
        { "Alex", "Elliott", "Harvey", "Sam", "Sebastian", "Shane" };
    private static readonly HashSet<string> KnownBachelorettes = new(StringComparer.OrdinalIgnoreCase)
        { "Abigail", "Emily", "Haley", "Leah", "Maru", "Penny" };
    private static readonly HashSet<string> KnownSpecial = new(StringComparer.OrdinalIgnoreCase)
        { "Dwarf", "Krobus", "Sandy", "Wizard", "Leo" };

    private static readonly Dictionary<string, string> SveFallbackPersonalities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sophia"] = "A shy and emotionally sensitive vineyard owner from Blue Moon Vineyard. Gentle, anxious, and kind-hearted, she appreciates empathy and calm support.",
        ["Victor"] = "An intelligent and thoughtful young man from a wealthy family. Polite, reserved, and academically inclined, often introspective and idealistic.",
        ["Olivia"] = "A refined and confident businesswoman with sophisticated tastes. Elegant, direct, and protective of her family, with a warm side as trust grows.",
        ["Andy"] = "A hardworking and stubborn farmer who values practical effort and loyalty. Gruff at first, but dependable and sincere underneath.",
        ["Susan"] = "An independent and straightforward woman focused on her own routine and priorities. Practical, no-nonsense, but fair and authentic.",
        ["Claire"] = "A polite, overworked employee trying to stay positive under pressure. Friendly, humble, and appreciative of small acts of kindness.",
        ["Martin"] = "A gentle and enthusiastic younger villager, curious and sincere. Friendly, a little awkward, and eager to connect.",
        ["Lance"] = "A seasoned adventurer tied to the Adventurer's Guild. Brave, composed, and strategic, with understated humor and strong duty.",
        ["Morris"] = "An ambitious and image-conscious executive personality. Calculating and persuasive, though capable of nuance depending on the situation.",
        ["Scarlett"] = "An energetic and social villager with a modern, expressive style. Warm, lively, and quick to engage in conversation.",
        ["Morgan"] = "A magical child with curious and innocent worldview. Imaginative, playful, and prone to wonder.",
        ["Apples"] = "A mysterious Junimo-like being with whimsical and curious mannerisms, expressing emotions in a playful and unusual way.",
    };

    private int _scrollY;
    private int _maxScroll;

    private const int CardH = 170;
    private const int CardGap = 10;
    private const int PortraitSrc = 64;
    private const int PortraitDraw = 108;
    private const int TabH = 44;
    private const int Pad = 16;

    private Rectangle _contentArea;

    private static readonly Color CardBg = new(222, 195, 153);
    private static readonly Color TabActive = new(170, 120, 60);
    private static readonly Color TabInactive = new(200, 170, 130);

    public PersonalityEditorMenu(
        PersonalityStore store,
        PresetStore presetStore,
        IAliveNpcsApi api,
        EditorConfig config,
        GalleryService? galleryService,
        IMonitor monitor,
        ITranslationHelper i18n)
        : base(0, 0, 0, 0)
    {
        _store = store;
        _presetStore = presetStore;
        _api = api;
        _config = config;
        _galleryService = galleryService;
        _monitor = monitor;
        _i18n = i18n;

        _categories = BuildCategories(api);

        foreach (var (_, npcs) in _categories)
            foreach (var npc in npcs)
            {
                var defaultPersonality = api.GetDefaultPersonality(npc);
                if (SveFallbackPersonalities.TryGetValue(npc, out var sveFallback)
                    && IsGenericFallback(defaultPersonality, npc))
                {
                    defaultPersonality = sveFallback;
                }
                _defaults.TryAdd(npc, defaultPersonality ?? "");
                try
                {
                    var npcObj = Game1.getCharacterFromName(npc);
                    _portraits[npc] = npcObj?.Portrait
                        ?? Game1.content.Load<Texture2D>($"Portraits/{npc}");
                }
                catch
                {
                    _portraits[npc] = null;
                }
            }

        RecalculateLayout();

        if (_config.GalleryEnabled && _galleryService != null)
        {
            _galleryPane = new GalleryPane(
                _galleryService, _store, _portraits, _i18n, _monitor, GetGalleryContentArea,
                _config.GalleryServerUrl, _presetStore);
        }
    }

    private static (string Key, string[] Npcs)[] BuildCategories(IAliveNpcsApi api)
    {
        try
        {
            var editableNpcs = api.GetEditableNpcNames()
                .Where(npc => !string.IsNullOrWhiteSpace(npc))
                .Select(npc => npc.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return BuildCategories(api, editableNpcs);
        }
        catch (Exception)
        {
            return BuildLegacyCategories(api);
        }
    }

    private static (string Key, string[] Npcs)[] BuildCategories(IAliveNpcsApi api, IEnumerable<string> editableNpcs)
    {
        var editable = new HashSet<string>(editableNpcs, StringComparer.OrdinalIgnoreCase);
        var vanillaSet = new HashSet<string>(api.GetVanillaNpcNames(), StringComparer.OrdinalIgnoreCase);
        var sveSet = new HashSet<string>(api.GetSveNpcNames(), StringComparer.OrdinalIgnoreCase);
        var vanillaNpcs = editable.Where(vanillaSet.Contains).OrderBy(npc => npc).ToArray();
        var sveNpcs = editable.Where(sveSet.Contains).OrderBy(npc => npc).ToArray();
        var otherNpcs = editable
            .Where(npc => !vanillaSet.Contains(npc) && !sveSet.Contains(npc))
            .OrderBy(npc => npc)
            .ToArray();

        return CreateCategories(vanillaNpcs, sveNpcs, otherNpcs);
    }

    private static (string Key, string[] Npcs)[] BuildLegacyCategories(IAliveNpcsApi api)
    {
        var vanillaNpcs = api.GetVanillaNpcNames().ToArray();
        var sveNpcs = api.GetSveNpcNames().ToArray();
        var knownNames = new HashSet<string>(vanillaNpcs.Concat(sveNpcs), StringComparer.OrdinalIgnoreCase);

        var otherNpcs = new List<string>();

        try
        {
            var characterData = Game1.content.Load<Dictionary<string, StardewValley.GameData.Characters.CharacterData>>("Data/Characters");
            otherNpcs = characterData
                .Where(kvp => !knownNames.Contains(kvp.Key) && !string.Equals(kvp.Value.CanSocialize, "FALSE", StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Key)
                .OrderBy(n => n)
                .ToList();
        }
        catch { }

        return CreateCategories(vanillaNpcs, sveNpcs, otherNpcs);
    }

    private static (string Key, string[] Npcs)[] CreateCategories(
        IEnumerable<string> vanillaNpcs,
        IEnumerable<string> sveNpcs,
        IEnumerable<string> otherNpcs)
    {
        var vanilla = vanillaNpcs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var sve = sveNpcs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var other = otherNpcs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var bachelors = vanilla.Where(n => KnownBachelors.Contains(n)).ToArray();
        var bachelorettes = vanilla.Where(n => KnownBachelorettes.Contains(n)).ToArray();
        var townspeople = vanilla
            .Where(n => !KnownBachelors.Contains(n) && !KnownBachelorettes.Contains(n) && !KnownSpecial.Contains(n))
            .ToArray();

        var special = vanilla.Where(n => KnownSpecial.Contains(n))
            .Concat(other.Where(n => KnownSpecial.Contains(n)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        other = other.Where(n => !KnownSpecial.Contains(n)).ToList();

        var categories = new List<(string Key, string[] Npcs)>();
        if (bachelors.Length > 0) categories.Add(("page.bachelors", bachelors));
        if (bachelorettes.Length > 0) categories.Add(("page.bachelorettes", bachelorettes));
        if (townspeople.Length > 0) categories.Add(("page.townspeople", townspeople));
        if (special.Length > 0) categories.Add(("page.special", special));
        if (sve.Length > 0) categories.Add(("page.sve", sve));
        if (other.Count > 0) categories.Add(("page.other", other.ToArray()));

        return categories.ToArray();
    }

    private static bool IsGenericFallback(string? personality, string npcName)
    {
        return string.Equals(
            personality?.Trim(),
            $"A villager in Stardew Valley named {npcName}. Friendly and part of the community.",
            StringComparison.Ordinal);
    }

    private void RecalculateLayout()
    {
        var vw = Game1.uiViewport.Width;
        var vh = Game1.uiViewport.Height;
        width = Math.Min(1100, vw - 80);
        height = Math.Min(860, vh - 40);
        xPositionOnScreen = (vw - width) / 2;
        yPositionOnScreen = (vh - height) / 2;

        var innerX = xPositionOnScreen + 24;
        var innerW = width - 48;
        var tabsBottom = yPositionOnScreen + 68 + TabH;
        var contentH = height - 68 - TabH - 32;

        _contentArea = new Rectangle(innerX, tabsBottom + 4, innerW, contentH);
    }

    private Rectangle GetGalleryContentArea()
    {
        var innerX = xPositionOnScreen + 24;
        var innerW = width - 48;
        var galleryTop = yPositionOnScreen + 12;
        var galleryH = height - 24;
        return new Rectangle(innerX, galleryTop, innerW, galleryH);
    }

    // ═══════════════════════════════════════════════════════════
    //  DRAWING
    // ═══════════════════════════════════════════════════════════

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect,
            new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
            Color.Black * 0.75f);

        drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        DrawTopLevelTabs(b);

        if (_topLevelTab == 0)
        {
            DrawTitle(b);
            DrawTabs(b);
            DrawCards(b);
        }
        else if (_topLevelTab == 1 && _galleryPane != null)
        {
            _galleryPane.Draw(b);
        }

        drawMouse(b);
    }

    private void DrawTopLevelTabs(SpriteBatch b)
    {
        var tabH = 36;
        var tabY = yPositionOnScreen - tabH + 4;
        var tabW = 200;
        var labels = new[] { _i18n.Get("tab.personalities"), _i18n.Get("tab.gallery") };
        var startX = xPositionOnScreen + 20;

        for (int i = 0; i < labels.Length; i++)
        {
            var rect = new Rectangle(startX + i * (tabW + 6), tabY, tabW, tabH);
            var active = i == _topLevelTab;
            var color = active ? TabActive : TabInactive;

            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                rect.X, rect.Y, rect.Width, rect.Height, color);

            if (active)
            {
                b.Draw(Game1.menuTexture,
                    new Rectangle(rect.X, rect.Bottom - 6, rect.Width, 6),
                    new Rectangle(0, 256 + 54, 60, 6), color);
            }

            var labelSize = Game1.smallFont.MeasureString(labels[i]);
            Utility.drawTextWithShadow(b, labels[i], Game1.smallFont,
                new Vector2(rect.X + (rect.Width - labelSize.X) / 2f, rect.Y + (rect.Height - labelSize.Y) / 2f),
                active ? Color.White : Color.Wheat);
        }
    }

    private void DrawTitle(SpriteBatch b)
    {
        var title = _i18n.Get("editor.title");
        var size = Game1.dialogueFont.MeasureString(title);
        Utility.drawTextWithShadow(b, title, Game1.dialogueFont,
            new Vector2(xPositionOnScreen + (width - size.X) / 2f, yPositionOnScreen + 18),
            Color.SaddleBrown);
    }

    private void DrawTabs(SpriteBatch b)
    {
        var tabY = yPositionOnScreen + 64;
        var totalW = width - 48;
        var tabW = totalW / _categories.Length;

        for (int i = 0; i < _categories.Length; i++)
        {
            var rect = new Rectangle(xPositionOnScreen + 24 + i * tabW, tabY, tabW - 4, TabH);
            var active = i == _activeTab;
            var color = active ? TabActive : TabInactive;

            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                rect.X, rect.Y, rect.Width, rect.Height, color);

            var label = _i18n.Get(_categories[i].Key);
            var labelSize = Game1.smallFont.MeasureString(label);
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(rect.X + (rect.Width - labelSize.X) / 2f, rect.Y + (rect.Height - labelSize.Y) / 2f),
                active ? Color.White : Color.Wheat);
        }
    }

    private void DrawCards(SpriteBatch b)
    {
        var npcs = _categories[_activeTab].Npcs;
        var totalH = npcs.Length * (CardH + CardGap) - CardGap;
        _maxScroll = Math.Max(0, totalH - _contentArea.Height);
        _scrollY = Math.Clamp(_scrollY, 0, _maxScroll);

        var prevScissor = b.GraphicsDevice.ScissorRectangle;
        var prevRasterizer = b.GraphicsDevice.RasterizerState;
        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null,
            new RasterizerState { ScissorTestEnable = true });
        b.GraphicsDevice.ScissorRectangle = _contentArea;

        for (int i = 0; i < npcs.Length; i++)
        {
            var npc = npcs[i];
            var cardY = _contentArea.Y + i * (CardH + CardGap) - _scrollY;

            if (cardY + CardH < _contentArea.Y || cardY > _contentArea.Bottom)
                continue;

            DrawSingleCard(b, npc, _contentArea.X, cardY, _contentArea.Width);
        }

        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, prevRasterizer);
        b.GraphicsDevice.ScissorRectangle = prevScissor;

        if (_maxScroll > 0)
            DrawScrollbar(b);
    }

    private void DrawSingleCard(SpriteBatch b, string npc, int cx, int cy, int cw)
    {
        var entry = _store.Get(npc);
        var hasOverride = entry != null && entry.HasAnyField;
        var hasCharData = entry?.HasCharacterDataOverride == true;
        var hasPersonalityOverride = entry != null && !string.IsNullOrWhiteSpace(entry.CanonicalPersonality);
        var hasSupplementaryOnly = entry != null && entry.HasOnlySupplementaryFields;

        drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            cx, cy, cw, CardH, CardBg);

        // ── Portrait (left side) ──
        var portraitX = cx + Pad;
        var portraitY = cy + 10;
        var portrait = _portraits.GetValueOrDefault(npc);
        if (portrait != null)
            b.Draw(portrait, new Rectangle(portraitX, portraitY, PortraitDraw, PortraitDraw),
                new Rectangle(0, 0, PortraitSrc, PortraitSrc), Color.White);
        else
            b.Draw(Game1.staminaRect,
                new Rectangle(portraitX, portraitY, PortraitDraw, PortraitDraw),
                Color.Black * 0.1f);

        // ── Name below portrait ──
        string indicator = hasPersonalityOverride ? " *" : (hasSupplementaryOnly ? " +" : "");
        if (hasCharData) indicator += " CD";
        var nameStr = hasOverride ? $"{npc}{indicator}" : npc;
        var nameColor = hasOverride ? new Color(70, 140, 50) : Color.SaddleBrown;
        var nameSize = Game1.smallFont.MeasureString(nameStr);
        var nameX = portraitX + (PortraitDraw - nameSize.X) / 2f;
        var nameY = portraitY + PortraitDraw + 4;
        Utility.drawTextWithShadow(b, nameStr, Game1.smallFont, new Vector2(nameX, nameY), nameColor);

        // ── Personality text (right of portrait) ──
        var textX = portraitX + PortraitDraw + Pad;
        var textW = cw - PortraitDraw - Pad * 3 - 12;
        var personality = GetCurrentPersonalityPreview(npc);
        var wrapped = Game1.parseText(personality, Game1.smallFont, textW);

        var lines = wrapped.Split('\n');
        var maxLines = (CardH - 24) / (int)Game1.smallFont.MeasureString("A").Y;
        if (lines.Length > maxLines)
            wrapped = string.Join("\n", lines.Take(maxLines - 1)) + "\n...";

        b.DrawString(Game1.smallFont, wrapped, new Vector2(textX, cy + 14), Color.Black * 0.85f);
    }

    private void DrawScrollbar(SpriteBatch b)
    {
        var barX = _contentArea.Right - 8;
        var barH = _contentArea.Height;
        var thumbH = Math.Max(30, barH * barH / (barH + _maxScroll));
        var thumbY = _contentArea.Y + (int)((float)_scrollY / _maxScroll * (barH - thumbH));

        b.Draw(Game1.staminaRect, new Rectangle(barX, _contentArea.Y, 6, barH), Color.Black * 0.15f);
        b.Draw(Game1.staminaRect, new Rectangle(barX, thumbY, 6, thumbH), Color.SaddleBrown * 0.6f);
    }

    // ═══════════════════════════════════════════════════════════
    //  INPUT
    // ═══════════════════════════════════════════════════════════

    private Rectangle GetTabRect(int i)
    {
        var tabW = (width - 48) / _categories.Length;
        return new Rectangle(xPositionOnScreen + 24 + i * tabW, yPositionOnScreen + 64, tabW - 4, TabH);
    }

    private Rectangle GetCardRect(int i)
    {
        var cardY = _contentArea.Y + i * (CardH + CardGap) - _scrollY;
        return new Rectangle(_contentArea.X, cardY, _contentArea.Width, CardH);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        // Top-level tabs (above the window)
        var tabH = 36;
        var tabY = yPositionOnScreen - tabH + 4;
        var tabW = 200;
        var startX = xPositionOnScreen + 20;
        for (int i = 0; i < 2; i++)
        {
            var rect = new Rectangle(startX + i * (tabW + 6), tabY, tabW, tabH);
            if (rect.Contains(x, y))
            {
                _topLevelTab = i;
                _scrollY = 0;
                Game1.playSound("smallSelect");
                return;
            }
        }

        if (_topLevelTab == 1 && _galleryPane != null)
        {
            _galleryPane.receiveLeftClick(x, y);
            return;
        }

        // NPC category tabs (Personalities tab only)
        for (int i = 0; i < _categories.Length; i++)
        {
            if (GetTabRect(i).Contains(x, y))
            {
                _activeTab = i;
                _scrollY = 0;
                Game1.playSound("smallSelect");
                return;
            }
        }

        // Cards — open modal on click
        var npcs = _categories[_activeTab].Npcs;
        for (int i = 0; i < npcs.Length; i++)
        {
            var cardRect = GetCardRect(i);
            if (cardRect.Contains(x, y) && _contentArea.Contains(x, y))
            {
                OpenEditModal(npcs[i]);
                Game1.playSound("smallSelect");
                return;
            }
        }
    }

    private void OpenEditModal(string npcName)
    {
        var portrait = _portraits.GetValueOrDefault(npcName);
        var defaultPersonality = _defaults.GetValueOrDefault(npcName, "");

        var modal = new PersonalityEditModal(
            npcName,
            defaultPersonality,
            portrait,
            _store,
            _presetStore,
            _api,
            _config,
            _galleryService,
            _monitor,
            _i18n,
            OnModalClosed);

        Game1.activeClickableMenu = modal;
    }

    private void OnModalClosed()
    {
        Game1.activeClickableMenu = this;
    }

    public override void receiveScrollWheelAction(int direction)
    {
        if (_topLevelTab == 1 && _galleryPane != null)
        {
            _galleryPane.receiveScrollWheelAction(direction);
            return;
        }

        _scrollY -= direction;
        _scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, _maxScroll));
    }

    public override void receiveKeyPress(Keys key)
    {
        if (_topLevelTab == 1 && _galleryPane != null)
        {
            _galleryPane.receiveKeyPress(key);
            return;
        }

        if (key == Keys.Escape)
        {
            exitThisMenu();
        }
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        RecalculateLayout();
    }

    // ═══════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════

    private string GetCurrentPersonalityPreview(string npcName)
    {
        var entry = _store.Get(npcName);
        if (entry != null && entry.HasAnyField)
        {
            if (!string.IsNullOrWhiteSpace(entry.CanonicalPersonality))
                return entry.CanonicalPersonality;
            return _defaults.GetValueOrDefault(npcName, "");
        }
        return _defaults.GetValueOrDefault(npcName, "");
    }
}
