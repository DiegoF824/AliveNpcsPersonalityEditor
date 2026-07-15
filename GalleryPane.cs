using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AliveNpcsPersonalityEditor.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace AliveNpcsPersonalityEditor;

public sealed class GalleryPane
{
    private readonly GalleryService _service;
    private readonly PersonalityStore _store;
    private readonly PresetStore? _presetStore;
    private readonly Dictionary<string, Texture2D?> _portraits;
    private readonly ITranslationHelper _i18n;
    private readonly IMonitor _monitor;
    private readonly System.Func<Rectangle> _getContentArea;
    private readonly string _serverUrl;

    private readonly List<PresetMetadata> _presets = new();
    private int _currentPage;
    private bool _loading;
    private bool _hasMore = true;
    private string? _searchQuery;
    private string _searchInput = "";
    private bool _searchFocused;
    private int _scrollY;
    private int _maxScroll;

    private const int MinCardW = 180;
    private const int CardH = 180;
    private const int CardGap = 10;
    private const int TitleH = 32;
    private const int UrlH = 24;
    private const int SwitchH = 32;
    private const int FilterH = 34;
    private const int SearchH = 32;

    private Rectangle _searchBox;
    private Rectangle _searchBtn;
    private Rectangle _refreshBtn;
    private Rectangle _npcFilterBtn;
    private Rectangle _browseSwitchBtn;
    private Rectangle _uploadSwitchBtn;
    private bool _npcFilterOpen;
    private int _npcFilterIndex = -1; // -1 = All NPCs
    private List<string> _npcFilterNames = new();
    private int _galleryMode = 0; // 0 = Browse, 1 = Upload
    private GalleryPreviewModal? _previewModal;

    // Upload mode state
    private List<(string NpcName, NpcOverrideEntry Entry)> _localPresets = new();
    private List<(string NpcName, NpcOverrideEntry Entry)> _filteredLocal = new();
    private string _localSearchInput = "";
    private int _localNpcFilterIndex = -1;
    private int _localScrollY;
    private int _localMaxScroll;
    private readonly List<Rectangle> _uploadBtnRects = new();
    private readonly List<Rectangle> _deleteBtnRects = new();

    public GalleryPane(
        GalleryService service,
        PersonalityStore store,
        Dictionary<string, Texture2D?> portraits,
        ITranslationHelper i18n,
        IMonitor monitor,
        System.Func<Rectangle> getContentArea,
        string serverUrl,
        PresetStore? presetStore = null)
    {
        _service = service;
        _store = store;
        _presetStore = presetStore;
        _portraits = portraits;
        _i18n = i18n;
        _monitor = monitor;
        _getContentArea = getContentArea;
        _serverUrl = serverUrl;

        _npcFilterNames = new List<string> { _i18n.Get("gallery.filter.all_npcs") };
        _npcFilterNames.AddRange(portraits.Keys.OrderBy(n => n));

        _ = FetchPageAsync();
    }

    private async Task FetchPageAsync(string? query = null)
    {
        _loading = true;
        try
        {
            var npcFilter = _npcFilterIndex > 0 ? _npcFilterNames[_npcFilterIndex] : null;
            var response = await _service.SearchPresetsAsync(query, _currentPage + 1, npcFilter: npcFilter);
            if (response != null)
            {
                _presets.AddRange(response.Presets);
                _hasMore = response.Presets.Count >= response.Limit;
                _currentPage = response.Page;
                UpdateMaxScroll();
            }
            else if (_currentPage == 0)
            {
                _hasMore = false;
            }
        }
        catch (System.Exception ex)
        {
            _monitor.Log($"Gallery fetch error: {ex.Message}", LogLevel.Warn);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task SearchAsync(string? query)
    {
        _loading = true;
        _presets.Clear();
        _currentPage = 0;
        _hasMore = true;
        _scrollY = 0;
        _searchQuery = query;

        var npcFilter = _npcFilterIndex > 0 ? _npcFilterNames[_npcFilterIndex] : null;

        try
        {
            var response = await _service.SearchPresetsAsync(
                !string.IsNullOrWhiteSpace(query) ? query : null,
                1, npcFilter: npcFilter);
            if (response != null)
            {
                _presets.AddRange(response.Presets);
                _hasMore = response.Presets.Count >= response.Limit;
                _currentPage = response.Page;
                UpdateMaxScroll();
            }
        }
        catch (System.Exception ex)
        {
            _monitor.Log($"Gallery search error: {ex.Message}", LogLevel.Warn);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task RefreshAsync()
    {
        _loading = true;
        _presets.Clear();
        _currentPage = 0;
        _hasMore = true;
        _scrollY = 0;

        var npcFilter = _npcFilterIndex > 0 ? _npcFilterNames[_npcFilterIndex] : null;

        try
        {
            var response = await _service.SearchPresetsAsync(_searchQuery, 1, npcFilter: npcFilter);
            if (response != null)
            {
                _presets.AddRange(response.Presets);
                _hasMore = response.Presets.Count >= response.Limit;
                _currentPage = response.Page;
                UpdateMaxScroll();
            }
        }
        catch (System.Exception ex)
        {
            _monitor.Log($"Gallery refresh error: {ex.Message}", LogLevel.Warn);
        }
        finally
        {
            _loading = false;
        }
    }

    private (int cols, int cardW, int startX, int gridTop) GetGridLayout()
    {
        var area = _getContentArea();
        var gridTop = area.Y + TitleH + UrlH + SwitchH + FilterH + SearchH + 20;
        var gridArea = new Rectangle(area.X, gridTop, area.Width, area.Bottom - gridTop);

        var cols = System.Math.Max(2, (gridArea.Width + CardGap) / (MinCardW + CardGap));
        var cardW = (gridArea.Width - (cols - 1) * CardGap) / cols;
        var contentW = cols * cardW + (cols - 1) * CardGap;
        var startX = gridArea.X + (gridArea.Width - contentW) / 2;

        return (cols, cardW, startX, gridTop);
    }

    public void Draw(SpriteBatch b)
    {
        var area = _getContentArea();

        // Title
        var title = _i18n.Get("gallery.title");
        var titleSize = Game1.dialogueFont.MeasureString(title);
        Utility.drawTextWithShadow(b, title, Game1.dialogueFont,
            new Vector2(area.X + (area.Width - titleSize.X) / 2f, area.Y), Color.SaddleBrown);

        // Server URL display
        var urlLabel = _i18n.Get("gallery.server_url");
        var urlY = area.Y + TitleH;
        Utility.drawTextWithShadow(b, $"{urlLabel}: {_serverUrl}", Game1.smallFont,
            new Vector2(area.X + 8, urlY), Color.Gray);

        // Browse / Manage switch buttons
        var switchY = urlY + UrlH;
        var switchW = 100;
        var switchH = SwitchH;
        var browseRect = new Rectangle(area.X + 8, switchY, switchW, switchH);
        var uploadRect = new Rectangle(area.X + 8 + switchW + 6, switchY, switchW, switchH);
        _browseSwitchBtn = browseRect;
        _uploadSwitchBtn = uploadRect;

        DrawTabSwitch(b, browseRect, _i18n.Get("gallery.mode.browse"), _galleryMode == 0);
        DrawTabSwitch(b, uploadRect, _i18n.Get("gallery.mode.manage"), _galleryMode == 1);

        // NPC filter dropdown + Refresh button on same row
        var filterY = switchY + SwitchH + 6;
        var filterRect = new Rectangle(area.X + 8, filterY, 180, FilterH);
        _npcFilterBtn = filterRect;
        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            filterRect.X, filterRect.Y, filterRect.Width, filterRect.Height,
            _npcFilterOpen ? new Color(190, 160, 100) : new Color(200, 170, 130));
        var filterLabelIndex = _galleryMode == 1 ? _localNpcFilterIndex : _npcFilterIndex;
        var filterLabel = filterLabelIndex >= 0 && filterLabelIndex < _npcFilterNames.Count
            ? _npcFilterNames[filterLabelIndex] : _i18n.Get("gallery.filter.all_npcs");
        var fSize = Game1.smallFont.MeasureString(filterLabel);
        Utility.drawTextWithShadow(b, filterLabel, Game1.smallFont,
            new Vector2(filterRect.X + 8, filterRect.Y + (filterRect.Height - fSize.Y) / 2f),
            Color.Black * 0.85f);
        Utility.drawTextWithShadow(b, "v", Game1.smallFont,
            new Vector2(filterRect.Right - 16, filterRect.Y + (filterRect.Height - fSize.Y) / 2f), Color.Gray);

        // Refresh button (same row, right-aligned)
        var refBtnRect = new Rectangle(area.Right - 90, filterY, 82, FilterH);
        _refreshBtn = refBtnRect;
        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            refBtnRect.X, refBtnRect.Y, refBtnRect.Width, refBtnRect.Height, new Color(170, 120, 60));
        var refText = _i18n.Get("gallery.button.refresh");
        var refSize = Game1.smallFont.MeasureString(refText);
        Utility.drawTextWithShadow(b, refText, Game1.smallFont,
            new Vector2(refBtnRect.X + (refBtnRect.Width - refSize.X) / 2f, refBtnRect.Y + (refBtnRect.Height - refSize.Y) / 2f),
            Color.White);

        // Search field + button on next row
        var searchY = filterY + FilterH + 6;
        var searchRect = new Rectangle(area.X + 8, searchY, area.Width - 100, SearchH);
        _searchBox = searchRect;
        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            searchRect.X, searchRect.Y, searchRect.Width, searchRect.Height,
            _searchFocused ? new Color(190, 160, 100) : new Color(200, 170, 130));

        var displaySearchInput = _galleryMode == 1 ? _localSearchInput : _searchInput;
        if (string.IsNullOrEmpty(displaySearchInput) && !_searchFocused)
        {
            var ph = _galleryMode == 1
                ? _i18n.Get("gallery.upload.search_placeholder")
                : _i18n.Get("gallery.browse.search_placeholder");
            Utility.drawTextWithShadow(b, ph, Game1.smallFont,
                new Vector2(searchRect.X + 8, searchRect.Y + (searchRect.Height - Game1.smallFont.MeasureString(ph).Y) / 2f), Color.Gray);
        }
        else
        {
            var inputSize = Game1.smallFont.MeasureString(displaySearchInput);
            b.DrawString(Game1.smallFont, displaySearchInput,
                new Vector2(searchRect.X + 8, searchRect.Y + (searchRect.Height - inputSize.Y) / 2f), Color.Black * 0.85f);
        }

        if (_searchFocused && (Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 500) % 2 == 0)
        {
            var cursorX = searchRect.X + 8 + (int)Game1.smallFont.MeasureString(displaySearchInput).X;
            b.Draw(Game1.staminaRect, new Rectangle(cursorX, searchRect.Y + 6, 1, SearchH - 12), Color.Black * 0.6f);
        }

        var sBtnRect = new Rectangle(area.Right - 90, searchY, 82, SearchH);
        _searchBtn = sBtnRect;
        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            sBtnRect.X, sBtnRect.Y, sBtnRect.Width, sBtnRect.Height, new Color(170, 120, 60));
        var sText = _i18n.Get("gallery.browse.search");
        var sSize = Game1.smallFont.MeasureString(sText);
        Utility.drawTextWithShadow(b, sText, Game1.smallFont,
            new Vector2(sBtnRect.X + (sBtnRect.Width - sSize.X) / 2f, sBtnRect.Y + (sBtnRect.Height - sSize.Y) / 2f),
            Color.White);

        // NPC filter dropdown popup (drawn on top of everything)
        if (_npcFilterOpen)
        {
            var ddItemH = 26;
            var ddH = System.Math.Min(_npcFilterNames.Count * ddItemH, 220);
            var ddRect = new Rectangle(_npcFilterBtn.X, _npcFilterBtn.Bottom + 2, _npcFilterBtn.Width, ddH);
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                ddRect.X, ddRect.Y, ddRect.Width, ddRect.Height, new Color(245, 230, 200));

            for (int i = 0; i < _npcFilterNames.Count; i++)
            {
                var itemY = ddRect.Y + i * ddItemH;
                if (itemY + ddItemH > ddRect.Bottom) break;
                var selected = i == (_galleryMode == 1 ? _localNpcFilterIndex : _npcFilterIndex);
                if (selected)
                    b.Draw(Game1.staminaRect, new Rectangle(ddRect.X + 2, itemY, ddRect.Width - 4, ddItemH), new Color(180, 150, 80) * 0.5f);
                b.DrawString(Game1.smallFont, _npcFilterNames[i],
                    new Vector2(ddRect.X + 8, itemY + (ddItemH - Game1.smallFont.MeasureString(_npcFilterNames[i]).Y) / 2f),
                    selected ? Color.SaddleBrown : Color.Black * 0.7f);
            }
        }

        if (_previewModal != null)
        {
            _previewModal.Draw(b);
            return;
        }

        if (_galleryMode == 1)
        {
            DrawUploadView(b, area, searchY);
            return;
        }

        // Grid area
        var (cols, cardW, startX, gridTop) = GetGridLayout();
        var gridArea = new Rectangle(area.X, gridTop, area.Width, area.Bottom - gridTop);

        if (_loading && _presets.Count == 0)
        {
            var loadingText = _i18n.Get("gallery.browse.loading");
            var size = Game1.smallFont.MeasureString(loadingText);
            Utility.drawTextWithShadow(b, loadingText, Game1.smallFont,
                new Vector2(gridArea.X + (gridArea.Width - size.X) / 2f, gridArea.Y + 40), Color.Wheat);
            return;
        }

        if (_presets.Count == 0)
        {
            var emptyText = _i18n.Get("gallery.browse.empty");
            var size = Game1.smallFont.MeasureString(emptyText);
            Utility.drawTextWithShadow(b, emptyText, Game1.smallFont,
                new Vector2(gridArea.X + (gridArea.Width - size.X) / 2f, gridArea.Y + 40), Color.Wheat);
            return;
        }

        var prevScissor = b.GraphicsDevice.ScissorRectangle;
        var prevRasterizer = b.GraphicsDevice.RasterizerState;
        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null,
            new RasterizerState { ScissorTestEnable = true });
        b.GraphicsDevice.ScissorRectangle = gridArea;

        for (int i = 0; i < _presets.Count; i++)
        {
            var col = i % cols;
            var row = i / cols;
            var cx = startX + col * (cardW + CardGap);
            var cy = gridArea.Y + row * (CardH + CardGap) - _scrollY;

            if (cy + CardH < gridArea.Y || cy > gridArea.Bottom)
                continue;

            DrawCard(b, _presets[i], cx, cy, cardW);
        }

        if (_loading && _presets.Count > 0)
        {
            var loadingText = _i18n.Get("gallery.browse.loading");
            var lastRow = _presets.Count / cols;
            var ly = gridArea.Y + lastRow * (CardH + CardGap) - _scrollY + CardH + 8;
            Utility.drawTextWithShadow(b, loadingText, Game1.smallFont,
                new Vector2(gridArea.X + (gridArea.Width - Game1.smallFont.MeasureString(loadingText).X) / 2f, ly),
                Color.Wheat);
        }

        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, prevRasterizer);
        b.GraphicsDevice.ScissorRectangle = prevScissor;
    }

    private void DrawTabSwitch(SpriteBatch b, Rectangle rect, string label, bool active)
    {
        var color = active ? new Color(170, 120, 60) : new Color(200, 170, 130);
        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            rect.X, rect.Y, rect.Width, rect.Height, color);
        var size = Game1.smallFont.MeasureString(label);
        Utility.drawTextWithShadow(b, label, Game1.smallFont,
            new Vector2(rect.X + (rect.Width - size.X) / 2f, rect.Y + (rect.Height - size.Y) / 2f),
            active ? Color.White : Color.Wheat);
    }

    private void DrawUploadView(SpriteBatch b, Rectangle area, int searchY)
    {
        _uploadBtnRects.Clear();
        _deleteBtnRects.Clear();

        if (_localPresets.Count == 0)
            ReloadLocalPresets();

        FilterLocalPresets();

        var (cols, cardW, startX, gridTop) = GetGridLayout();
        var gridArea = new Rectangle(area.X, gridTop, area.Width, area.Bottom - gridTop);

        if (_filteredLocal.Count == 0)
        {
            var emptyText = _localPresets.Count == 0
                ? _i18n.Get("gallery.upload.no_presets")
                : _i18n.Get("gallery.browse.empty");
            var size = Game1.smallFont.MeasureString(emptyText);
            Utility.drawTextWithShadow(b, emptyText, Game1.smallFont,
                new Vector2(gridArea.X + (gridArea.Width - size.X) / 2f, gridArea.Y + 40), Color.Wheat);
            return;
        }

        var prevScissor = b.GraphicsDevice.ScissorRectangle;
        var prevRasterizer = b.GraphicsDevice.RasterizerState;
        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null,
            new RasterizerState { ScissorTestEnable = true });
        b.GraphicsDevice.ScissorRectangle = gridArea;

        for (int i = 0; i < _filteredLocal.Count; i++)
        {
            var col = i % cols;
            var row = i / cols;
            var cx = startX + col * (cardW + CardGap);
            var cy = gridArea.Y + row * (CardH + CardGap) - _localScrollY;

            if (cy + CardH < gridArea.Y || cy > gridArea.Bottom)
                continue;

            DrawLocalPresetCard(b, _filteredLocal[i], cx, cy, cardW, i);
        }

        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, prevRasterizer);
        b.GraphicsDevice.ScissorRectangle = prevScissor;
    }

    private void DrawLocalPresetCard(SpriteBatch b, (string NpcName, NpcOverrideEntry Entry) preset, int cx, int cy, int cardW, int index)
    {
        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            cx, cy, cardW, CardH, new Color(222, 195, 153));

        var portrait = _portraits.GetValueOrDefault(preset.NpcName);
        var portraitSize = 36;
        var portraitX = cx + (cardW - portraitSize) / 2;
        if (portrait != null)
            b.Draw(portrait, new Rectangle(portraitX, cy + 8, portraitSize, portraitSize),
                new Rectangle(0, 0, 64, 64), Color.White);
        else
            b.Draw(Game1.staminaRect,
                new Rectangle(portraitX, cy + 8, portraitSize, portraitSize), Color.Black * 0.1f);

        var nameSize = Game1.smallFont.MeasureString(preset.NpcName);
        Utility.drawTextWithShadow(b, preset.NpcName, Game1.smallFont,
            new Vector2(cx + (cardW - nameSize.X) / 2f, cy + portraitSize + 12), Color.SaddleBrown);

        var preview = !string.IsNullOrWhiteSpace(preset.Entry.SubmissionCredit)
            ? preset.Entry.SubmissionCredit
            : (!string.IsNullOrWhiteSpace(preset.Entry.CanonicalPersonality)
                ? preset.Entry.CanonicalPersonality : "(no preview)");
        var wrapped = Game1.parseText(preview, Game1.tinyFont, (int)((cardW - 12) / 0.75f));
        b.DrawString(Game1.tinyFont, wrapped, new Vector2(cx + 6, cy + portraitSize + 28), Color.Black * 0.6f, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);

        var btnW = 64;
        var btnH = 20;
        var btnY = cy + CardH - btnH - 6;
        var uploadRect = new Rectangle(cx + 8, btnY, btnW, btnH);
        var deleteRect = new Rectangle(cx + cardW - btnW - 8, btnY, btnW, btnH);

        if (_uploadBtnRects.Count <= index) _uploadBtnRects.Add(uploadRect); else _uploadBtnRects[index] = uploadRect;
        if (_deleteBtnRects.Count <= index) _deleteBtnRects.Add(deleteRect); else _deleteBtnRects[index] = deleteRect;

        DrawSmallButton(b, uploadRect, _i18n.Get("gallery.button.upload"), new Color(100, 140, 60));
        DrawSmallButton(b, deleteRect, _i18n.Get("gallery.button.delete"), new Color(140, 80, 80));
    }

    private void DrawSmallButton(SpriteBatch b, Rectangle rect, string text, Color color)
    {
        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            rect.X, rect.Y, rect.Width, rect.Height, color);
        var size = Game1.tinyFont.MeasureString(text);
        b.DrawString(Game1.tinyFont, text,
            new Vector2(rect.X + (rect.Width - size.X) / 2f, rect.Y + (rect.Height - size.Y) / 2f),
            Color.White);
    }

    private void ReloadLocalPresets()
    {
        _localPresets.Clear();
        if (_presetStore == null) return;
        var all = _presetStore.LoadAll();
        foreach (var kv in all.OrderBy(k => k.Key))
            _localPresets.Add((kv.Key, kv.Value));
    }

    private void FilterLocalPresets()
    {
        _filteredLocal.Clear();

        var filtered = _localPresets.AsEnumerable();

        if (_localNpcFilterIndex > 0 && _localNpcFilterIndex < _npcFilterNames.Count)
        {
            var npcName = _npcFilterNames[_localNpcFilterIndex];
            filtered = filtered.Where(p => string.Equals(p.NpcName, npcName, System.StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(_localSearchInput))
        {
            var q = _localSearchInput.ToLower();
            filtered = filtered.Where(p =>
                p.NpcName.ToLower().Contains(q) ||
                (p.Entry.CanonicalPersonality?.ToLower().Contains(q) == true) ||
                (p.Entry.Lore?.ToLower().Contains(q) == true) ||
                (p.Entry.SocialTags?.ToLower().Contains(q) == true) ||
                (p.Entry.SubmissionCredit?.ToLower().Contains(q) == true));
        }

        _filteredLocal = filtered.ToList();

        var (_, _, _, gridTop) = GetGridLayout();
        var area = _getContentArea();
        var gridH = area.Bottom - gridTop;
        var cols = System.Math.Max(2, (area.Width + CardGap) / (MinCardW + CardGap));
        var rows = (_filteredLocal.Count + cols - 1) / cols;
        var totalH = rows * (CardH + CardGap);
        _localMaxScroll = System.Math.Max(0, totalH - gridH);
        _localScrollY = System.Math.Clamp(_localScrollY, 0, _localMaxScroll);
    }

    private async Task UploadPresetAsync(string npcName, NpcOverrideEntry entry)
    {
        Game1.addHUDMessage(new HUDMessage(_i18n.Get("gallery.upload.progress"), 3));
        var success = await _service.UploadPresetAsync(npcName, entry, "Anonymous");
        if (success)
            Game1.addHUDMessage(new HUDMessage(_i18n.Get("gallery.upload.success"), 4));
        else
            Game1.addHUDMessage(new HUDMessage(_i18n.Get("gallery.upload.failed"), 3));
    }

    private void DrawCard(SpriteBatch b, PresetMetadata preset, int cx, int cy, int cardW)
    {
        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            cx, cy, cardW, CardH, new Color(222, 195, 153));

        var portrait = _portraits.GetValueOrDefault(preset.NpcName);
        var portraitSize = 40;
        var portraitX = cx + (cardW - portraitSize) / 2;
        if (portrait != null)
            b.Draw(portrait, new Rectangle(portraitX, cy + 8, portraitSize, portraitSize),
                new Rectangle(0, 0, 64, 64), Color.White);
        else
            b.Draw(Game1.staminaRect,
                new Rectangle(portraitX, cy + 8, portraitSize, portraitSize), Color.Black * 0.1f);

        var nameSize = Game1.smallFont.MeasureString(preset.NpcName);
        Utility.drawTextWithShadow(b, preset.NpcName, Game1.smallFont,
            new Vector2(cx + (cardW - nameSize.X) / 2f, cy + portraitSize + 12), Color.SaddleBrown);

        var author = $"by {preset.Author}";
        var aSize = Game1.tinyFont.MeasureString(author);
        b.DrawString(Game1.tinyFont, author,
            new Vector2(cx + (cardW - aSize.X * 0.75f) / 2f, cy + portraitSize + 28),
            Color.Gray, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);

        var preview = string.IsNullOrEmpty(preset.Preview) ? "(no preview)" : preset.Preview;
        var wrapped = Game1.parseText(preview, Game1.tinyFont, (int)((cardW - 12) / 0.75f));
        b.DrawString(Game1.tinyFont, wrapped, new Vector2(cx + 6, cy + portraitSize + 42), Color.Black * 0.6f, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);
    }

    public void receiveLeftClick(int x, int y)
    {
        if (_previewModal != null)
        {
            _previewModal.receiveLeftClick(x, y);
            return;
        }

        // Browse / Upload switch
        if (_browseSwitchBtn.Contains(x, y))
        {
            _galleryMode = 0;
            Game1.playSound("smallSelect");
            return;
        }
        if (_uploadSwitchBtn.Contains(x, y))
        {
            _galleryMode = 1;
            ReloadLocalPresets();
            Game1.playSound("smallSelect");
            return;
        }

        // NPC filter dropdown (shared for browse, upload uses same dropdown position)
        if (_npcFilterOpen)
        {
            _npcFilterOpen = false;
            var ddItemH = 24;
            var ddH = System.Math.Min(_npcFilterNames.Count * ddItemH, 200);
            var ddRect = new Rectangle(_npcFilterBtn.X, _npcFilterBtn.Bottom + 2, _npcFilterBtn.Width, ddH);
            if (ddRect.Contains(x, y))
            {
                var idx = (y - ddRect.Y) / ddItemH;
                if (idx >= 0 && idx < _npcFilterNames.Count)
                {
                    if (_galleryMode == 0)
                    {
                        _npcFilterIndex = idx;
                        _ = SearchAsync(_searchInput.Length > 0 ? _searchInput : null);
                    }
                    else
                    {
                        _localNpcFilterIndex = idx;
                    }
                    Game1.playSound("smallSelect");
                }
            }
            return;
        }

        if (_npcFilterBtn.Contains(x, y))
        {
            _npcFilterOpen = true;
            Game1.playSound("smallSelect");
            return;
        }

        if (_refreshBtn.Contains(x, y))
        {
            if (_galleryMode == 0)
                _ = RefreshAsync();
            else
                ReloadLocalPresets();
            Game1.playSound("smallSelect");
            return;
        }

        if (_searchBtn.Contains(x, y))
        {
            if (_galleryMode == 0)
                _ = SearchAsync(_searchInput);
            Game1.playSound("smallSelect");
            return;
        }

        if (_searchBox.Contains(x, y))
        {
            _searchFocused = true;
            Game1.keyboardDispatcher.Subscriber = null;
            return;
        }

        _searchFocused = false;

        if (_galleryMode == 1)
        {
            // Click on Upload / Delete buttons in local preset grid
            for (int i = 0; i < _uploadBtnRects.Count && i < _filteredLocal.Count; i++)
            {
                if (_uploadBtnRects[i].Contains(x, y))
                {
                    var preset = _filteredLocal[i];
                    _ = UploadPresetAsync(preset.NpcName, preset.Entry);
                    Game1.playSound("smallSelect");
                    return;
                }
            }
            for (int i = 0; i < _deleteBtnRects.Count && i < _filteredLocal.Count; i++)
            {
                if (_deleteBtnRects[i].Contains(x, y))
                {
                    var preset = _filteredLocal[i];
                    if (_presetStore != null)
                    {
                        _presetStore.Delete(preset.NpcName);
                        ReloadLocalPresets();
                    }
                    Game1.playSound("trashcan");
                    return;
                }
            }
            return; // No card clicks in upload mode
        }

        // Browse mode: grid clicks
        var (cols, cardW, startX, gridTop) = GetGridLayout();
        var area = _getContentArea();
        var gridArea = new Rectangle(area.X, gridTop, area.Width, area.Bottom - gridTop);

        for (int i = 0; i < _presets.Count; i++)
        {
            var col = i % cols;
            var row = i / cols;
            var cx = startX + col * (cardW + CardGap);
            var cy = gridArea.Y + row * (CardH + CardGap) - _scrollY;

            if (cy + CardH < gridArea.Y || cy > gridArea.Bottom)
                continue;

            if (new Rectangle(cx, cy, cardW, CardH).Contains(x, y))
            {
                _ = OpenPreviewAsync(_presets[i]);
                Game1.playSound("smallSelect");
                return;
            }
        }

        if (_hasMore && !_loading)
        {
            var lastRow = (_presets.Count - 1) / cols;
            var loadMoreY = gridArea.Y + (lastRow + 1) * (CardH + CardGap) - _scrollY;
            if (y > loadMoreY - 200)
            {
                _ = FetchPageAsync(_searchQuery);
            }
        }
    }

    private async Task OpenPreviewAsync(PresetMetadata preset)
    {
        var data = await _service.DownloadPresetAsync(preset.Id);
        _previewModal = new GalleryPreviewModal(
            preset, data, _store, _portraits, _i18n,
            () => _previewModal = null,
            _getContentArea);
    }

    public void receiveScrollWheelAction(int direction)
    {
        if (_previewModal != null) return;

        if (_galleryMode == 1)
        {
            _localScrollY -= direction;
            _localScrollY = System.Math.Clamp(_localScrollY, 0, Math.Max(0, _localMaxScroll));
            return;
        }

        _scrollY -= direction;
        _scrollY = System.Math.Clamp(_scrollY, 0, Math.Max(0, _maxScroll));

        var area = _getContentArea();
        var (_, _, _, gridTop) = GetGridLayout();
        var gridArea = new Rectangle(area.X, gridTop, area.Width, area.Bottom - gridTop);

        if (_hasMore && !_loading)
        {
            var cols = System.Math.Max(2, (gridArea.Width + CardGap) / (MinCardW + CardGap));
            var lastRow = (_presets.Count - 1) / cols;
            var lastY = gridArea.Y + lastRow * (CardH + CardGap) - _scrollY;
            if (lastY < gridArea.Bottom + 200)
            {
                _ = FetchPageAsync(_searchQuery);
            }
        }
    }

    public void receiveKeyPress(Keys key)
    {
        if (_previewModal != null)
        {
            _previewModal.receiveKeyPress(key);
            return;
        }

        if (_searchFocused)
        {
            var target = _galleryMode == 1 ? ref _localSearchInput : ref _searchInput;
            if (key == Keys.Escape)
            {
                _searchFocused = false;
            }
            else if (key == Keys.Enter)
            {
                if (_galleryMode == 0)
                    _ = SearchAsync(_searchInput);
                _searchFocused = false;
            }
            else if (key == Keys.Back && target.Length > 0)
            {
                target = target[..^1];
            }
            else if (key >= Keys.A && key <= Keys.Z)
            {
                target += key.ToString().ToLower();
            }
            else if (key == Keys.Space)
            {
                target += " ";
            }
        }
    }

    private void UpdateMaxScroll()
    {
        var (_, _, _, gridTop) = GetGridLayout();
        var area = _getContentArea();
        var gridH = area.Bottom - gridTop;
        var cols = System.Math.Max(2, (area.Width + CardGap) / (MinCardW + CardGap));
        var rows = (_presets.Count + cols - 1) / cols;
        var totalH = rows * (CardH + CardGap);
        _maxScroll = System.Math.Max(0, totalH - gridH);
        _scrollY = System.Math.Clamp(_scrollY, 0, _maxScroll);
    }
}

public sealed class GalleryPreviewModal
{
    private readonly PresetMetadata _meta;
    private readonly PresetDownload? _data;
    private readonly PersonalityStore _store;
    private readonly Dictionary<string, Texture2D?> _portraits;
    private readonly ITranslationHelper _i18n;
    private readonly System.Action _onClose;
    private readonly System.Func<Rectangle> _getContentArea;

    private Rectangle _importBtn;
    private Rectangle _saveLocalBtn;
    private Rectangle _closeBtn;

    public GalleryPreviewModal(
        PresetMetadata meta,
        PresetDownload? data,
        PersonalityStore store,
        Dictionary<string, Texture2D?> portraits,
        ITranslationHelper i18n,
        System.Action onClose,
        System.Func<Rectangle> getContentArea)
    {
        _meta = meta;
        _data = data;
        _store = store;
        _portraits = portraits;
        _i18n = i18n;
        _onClose = onClose;
        _getContentArea = getContentArea;
    }

    public void Draw(SpriteBatch b)
    {
        var area = _getContentArea();
        var vw = Game1.uiViewport.Width;
        var vh = Game1.uiViewport.Height;

        var modalW = System.Math.Min(600, vw - 80);
        var modalH = System.Math.Min(500, vh - 80);
        var mx = (vw - modalW) / 2;
        var my = (vh - modalH) / 2;

        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, vw, vh), Color.Black * 0.6f);
        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            mx, my, modalW, modalH, Color.White);

        var portrait = _portraits.GetValueOrDefault(_meta.NpcName);
        var portraitSize = 64;
        if (portrait != null)
            b.Draw(portrait, new Rectangle(mx + 16, my + 16, portraitSize, portraitSize),
                new Rectangle(0, 0, 64, 64), Color.White);

        Utility.drawTextWithShadow(b, _meta.NpcName, Game1.dialogueFont,
            new Vector2(mx + portraitSize + 28, my + 20), Color.SaddleBrown);

        var metaLine = $"by {_meta.Author} — {_meta.CreatedAt}";
        Utility.drawTextWithShadow(b, metaLine, Game1.smallFont,
            new Vector2(mx + portraitSize + 28, my + 50), Color.Gray);

        var textX = mx + 16;
        var textW = modalW - 32;
        var textY = my + 90;

        if (_data != null)
        {
            var d = _data.Data;
            if (!string.IsNullOrWhiteSpace(d.CanonicalPersonality))
                textY = DrawField(b, _i18n.Get("field.personality"), d.CanonicalPersonality, textX, textY, textW);
            if (!string.IsNullOrWhiteSpace(d.Lore))
                textY = DrawField(b, _i18n.Get("field.lore"), d.Lore, textX, textY, textW);
            if (!string.IsNullOrWhiteSpace(d.SocialTags))
                textY = DrawField(b, _i18n.Get("field.social_tags"), d.SocialTags, textX, textY, textW);
            if (!string.IsNullOrWhiteSpace(d.SubmissionCredit))
                textY = DrawField(b, _i18n.Get("field.submission_credit"), d.SubmissionCredit, textX, textY, textW);
        }

        var btnY = my + modalH - 44;
        var btnW = 120;
        var btnH = 32;

        _importBtn = new Rectangle(mx + 16, btnY, btnW, btnH);
        DrawButton(b, _importBtn, _i18n.Get("gallery.button.import"), new Color(100, 140, 60));

        _saveLocalBtn = new Rectangle(mx + 16 + btnW + 8, btnY, btnW + 20, btnH);
        DrawButton(b, _saveLocalBtn, _i18n.Get("gallery.button.save_local"), new Color(100, 100, 140));

        _closeBtn = new Rectangle(mx + modalW - btnW - 16, btnY, btnW, btnH);
        DrawButton(b, _closeBtn, _i18n.Get("button.close"), new Color(140, 80, 80));
    }

    private int DrawField(SpriteBatch b, string label, string text, int x, int y, int maxW)
    {
        var labelSize = Game1.smallFont.MeasureString(label);
        Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(x, y), new Color(170, 120, 60));
        y += (int)labelSize.Y + 2;

        var wrapped = Game1.parseText(text, Game1.smallFont, maxW);
        b.DrawString(Game1.smallFont, wrapped, new Vector2(x + 4, y), Color.Black * 0.85f);
        var lines = wrapped.Split('\n');
        y += lines.Length * (int)Game1.smallFont.MeasureString("A").Y + 8;
        return y;
    }

    private void DrawButton(SpriteBatch b, Rectangle rect, string text, Color color)
    {
        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            rect.X, rect.Y, rect.Width, rect.Height, color);
        var size = Game1.smallFont.MeasureString(text);
        Utility.drawTextWithShadow(b, text, Game1.smallFont,
            new Vector2(rect.X + (rect.Width - size.X) / 2f, rect.Y + (rect.Height - size.Y) / 2f),
            Color.White);
    }

    public void receiveLeftClick(int x, int y)
    {
        if (_importBtn.Contains(x, y))
        {
            if (_data != null)
            {
                _store.Set(_meta.NpcName, _data.Data);
                _store.Save(_meta.NpcName);
                Game1.addHUDMessage(new HUDMessage(_i18n.Get("gallery.import.success"), 3));
            }
            _onClose();
            return;
        }

        if (_closeBtn.Contains(x, y))
        {
            _onClose();
            return;
        }

        _onClose();
    }

    public void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape) _onClose();
    }
}
