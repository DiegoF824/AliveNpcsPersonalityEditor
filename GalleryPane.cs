using AliveNpcsPersonalityEditor.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace AliveNpcsPersonalityEditor;

/// <summary>Community catalog rendered inside the Catalog tab.</summary>
public sealed class GalleryPane
{
    private readonly GalleryService _service;
    private readonly PersonalityStore _store;
    private readonly Dictionary<string, Texture2D?> _portraits;
    private readonly ITranslationHelper _i18n;
    private readonly IMonitor _monitor;
    private readonly Func<Rectangle> _getContentArea;
    private readonly Action _onImported;
    private readonly List<PresetMetadata> _presets = new();
    private readonly SearchSubscriber _search = new();

    private int _currentPage;
    private bool _loading;
    private bool _hasMore = true;
    private string? _searchQuery;
    private int _scrollY;
    private int _maxScroll;
    private Rectangle _discoverButton;
    private Rectangle _allButton;
    private Rectangle _searchBox;
    private Rectangle _searchButton;
    private GalleryPreviewModal? _preview;

    private const int DiscoverH = 44;
    private const int SearchH = 40;
    private const int CardSize = 194;
    private const int CardRowGap = 32;
    private static readonly Color Active = new(235, 155, 45);
    private static readonly Color Paper = new(255, 248, 234);
    private static readonly Color Border = new(125, 60, 40);

    public GalleryPane(
        GalleryService service,
        PersonalityStore store,
        Dictionary<string, Texture2D?> portraits,
        ITranslationHelper i18n,
        IMonitor monitor,
        Func<Rectangle> getContentArea,
        string serverUrl,
        PresetStore? presetStore = null,
        Action? onImported = null)
    {
        _service = service;
        _store = store;
        _portraits = portraits;
        _i18n = i18n;
        _monitor = monitor;
        _getContentArea = getContentArea;
        _onImported = onImported ?? (() => { });
        _ = serverUrl;
        _ = presetStore;
        _ = FetchPageAsync();
    }

    private async Task FetchPageAsync()
    {
        if (_loading || !_hasMore)
            return;
        _loading = true;
        try
        {
            var response = await _service.SearchPresetsAsync(_searchQuery, _currentPage + 1);
            if (response == null)
            {
                if (_currentPage == 0)
                    _hasMore = false;
                return;
            }

            _presets.AddRange(response.Presets);
            _currentPage = response.Page;
            _hasMore = response.Presets.Count >= response.Limit;
            UpdateMaxScroll();
        }
        catch (Exception ex)
        {
            _monitor.Log($"Gallery fetch error: {ex.Message}", LogLevel.Warn);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task SearchAsync()
    {
        _loading = true;
        _presets.Clear();
        _currentPage = 0;
        _hasMore = true;
        _scrollY = 0;
        _searchQuery = string.IsNullOrWhiteSpace(_search.Text) ? null : _search.Text.Trim();
        try
        {
            var response = await _service.SearchPresetsAsync(_searchQuery, 1);
            if (response != null)
            {
                _presets.AddRange(response.Presets);
                _currentPage = response.Page;
                _hasMore = response.Presets.Count >= response.Limit;
            }
            else
            {
                _hasMore = false;
            }
            UpdateMaxScroll();
        }
        catch (Exception ex)
        {
            _monitor.Log($"Gallery search error: {ex.Message}", LogLevel.Warn);
        }
        finally
        {
            _loading = false;
        }
    }

    private (Rectangle GridArea, int Columns, int Gap, int StartX) GetGridLayout()
    {
        var area = _getContentArea();
        var gridTop = area.Y + DiscoverH + 92;
        var gridArea = new Rectangle(area.X, gridTop, area.Width, area.Bottom - gridTop);
        var columns = Math.Clamp((gridArea.Width + 40) / (CardSize + 40), 1, 4);
        var gap = columns > 1 ? (gridArea.Width - columns * CardSize) / (columns - 1) : 0;
        var used = columns * CardSize + Math.Max(0, columns - 1) * gap;
        return (gridArea, columns, gap, gridArea.X + (gridArea.Width - used) / 2);
    }

    private Rectangle GetCardRect(int index, (Rectangle GridArea, int Columns, int Gap, int StartX) layout)
    {
        var column = index % layout.Columns;
        var row = index / layout.Columns;
        return new Rectangle(
            layout.StartX + column * (CardSize + layout.Gap),
            layout.GridArea.Y + row * (CardSize + CardRowGap) - _scrollY,
            CardSize,
            CardSize);
    }

    public void Draw(SpriteBatch b)
    {
        var area = _getContentArea();
        _discoverButton = new Rectangle(area.X, area.Y, 252, DiscoverH);
        DrawButton(b, _discoverButton, _i18n.Get("tab.discover"), Active);

        var controlsY = area.Y + DiscoverH + 26;
        _allButton = new Rectangle(area.X, controlsY, 60, SearchH);
        DrawButton(b, _allButton, _i18n.Get("filter.all"), Active);

        _searchButton = new Rectangle(area.Right - 120, controlsY, 120, SearchH);
        _searchBox = new Rectangle(_allButton.Right + 16, controlsY,
            _searchButton.X - 16 - (_allButton.Right + 16), SearchH);
        b.Draw(Game1.staminaRect, _searchBox, Paper);
        PersonalityEditorMenu.DrawBorder(b, _searchBox, Border, _search.Selected ? 4 : 3);

        var searchText = _search.Text;
        if (string.IsNullOrEmpty(searchText) && !_search.Selected)
        {
            searchText = _i18n.Get("gallery.browse.search_placeholder");
            b.DrawString(Game1.smallFont, searchText,
                new Vector2(_searchBox.X + 10, _searchBox.Y + 8), Color.Gray);
        }
        else
        {
            b.DrawString(Game1.smallFont, searchText,
                new Vector2(_searchBox.X + 10, _searchBox.Y + 8), Color.Black);
        }

        if (_search.Selected && DateTime.UtcNow.Millisecond < 500)
        {
            var cursorX = _searchBox.X + 10 + (int)Game1.smallFont.MeasureString(_search.Text).X;
            b.Draw(Game1.staminaRect, new Rectangle(cursorX, _searchBox.Y + 7, 2, SearchH - 14), Color.Black);
        }
        DrawButton(b, _searchButton, _i18n.Get("gallery.browse.search"), Paper);

        DrawCards(b);
        _preview?.Draw(b);
    }

    private void DrawCards(SpriteBatch b)
    {
        var layout = GetGridLayout();
        if (_presets.Count == 0)
        {
            var key = _loading ? "gallery.browse.loading" : "gallery.browse.empty";
            var message = _i18n.Get(key).ToString();
            var size = Game1.smallFont.MeasureString(message);
            Utility.drawTextWithShadow(b, message, Game1.smallFont,
                new Vector2(layout.GridArea.X + (layout.GridArea.Width - size.X) / 2f, layout.GridArea.Y + 40),
                Color.SaddleBrown);
            return;
        }

        var previousScissor = b.GraphicsDevice.ScissorRectangle;
        var previousRasterizer = b.GraphicsDevice.RasterizerState;
        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null,
            new RasterizerState { ScissorTestEnable = true });
        b.GraphicsDevice.ScissorRectangle = layout.GridArea;

        for (var i = 0; i < _presets.Count; i++)
        {
            var rect = GetCardRect(i, layout);
            if (rect.Bottom < layout.GridArea.Top || rect.Top > layout.GridArea.Bottom)
                continue;
            DrawCard(b, _presets[i], rect);
        }

        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, previousRasterizer);
        b.GraphicsDevice.ScissorRectangle = previousScissor;

        if (_maxScroll > 0)
            PersonalityEditorMenu.DrawScrollbar(b, layout.GridArea, _scrollY, _maxScroll);
    }

    private void DrawCard(SpriteBatch b, PresetMetadata preset, Rectangle rect)
    {
        b.Draw(Game1.staminaRect, rect, Paper);
        PersonalityEditorMenu.DrawBorder(b, rect, Border, 4);

        var nameSize = Game1.smallFont.MeasureString(preset.NpcName);
        Utility.drawTextWithShadow(b, preset.NpcName, Game1.smallFont,
            new Vector2(rect.X + (rect.Width - nameSize.X) / 2f, rect.Y + 7), Color.Black);

        var author = _i18n.Get("gallery.card.by", new { author = preset.Author }).ToString();
        var authorSize = Game1.smallFont.MeasureString(author);
        b.DrawString(Game1.smallFont, author,
            new Vector2(rect.X + (rect.Width - authorSize.X) / 2f, rect.Y + 35), Color.Gray);

        const int portraitSize = 70;
        var portraitRect = new Rectangle(rect.X + (rect.Width - portraitSize) / 2, rect.Bottom - portraitSize - 5, portraitSize, portraitSize);
        var portrait = _portraits.GetValueOrDefault(preset.NpcName);
        if (portrait != null)
            b.Draw(portrait, portraitRect, new Rectangle(0, 0, 64, 64), Color.White);
        else
            b.Draw(Game1.staminaRect, portraitRect, Color.Black * 0.08f);
    }

    private static void DrawButton(SpriteBatch b, Rectangle rect, string label, Color background)
    {
        b.Draw(Game1.staminaRect, rect, background);
        PersonalityEditorMenu.DrawBorder(b, rect, Border, 4);
        var size = Game1.smallFont.MeasureString(label);
        Utility.drawTextWithShadow(b, label, Game1.smallFont,
            new Vector2(rect.X + (rect.Width - size.X) / 2f, rect.Y + (rect.Height - size.Y) / 2f), Color.Black);
    }

    public void receiveLeftClick(int x, int y)
    {
        if (_preview != null)
        {
            _preview.receiveLeftClick(x, y);
            return;
        }

        if (_allButton.Contains(x, y))
        {
            _search.Text = "";
            _ = SearchAsync();
            Game1.playSound("smallSelect");
            return;
        }
        if (_searchBox.Contains(x, y))
        {
            _search.Selected = true;
            Game1.keyboardDispatcher.Subscriber = _search;
            return;
        }
        if (_searchButton.Contains(x, y))
        {
            Unsubscribe();
            _ = SearchAsync();
            Game1.playSound("smallSelect");
            return;
        }

        Unsubscribe();
        var layout = GetGridLayout();
        for (var i = 0; i < _presets.Count; i++)
        {
            var rect = GetCardRect(i, layout);
            if (!layout.GridArea.Contains(x, y) || !rect.Contains(x, y))
                continue;
            _ = OpenPreviewAsync(_presets[i]);
            Game1.playSound("smallSelect");
            return;
        }
    }

    private async Task OpenPreviewAsync(PresetMetadata preset)
    {
        var data = await _service.DownloadPresetAsync(preset.Id);
        if (data == null)
        {
            Game1.addHUDMessage(new HUDMessage(_i18n.Get("gallery.upload.failed"), 3));
            return;
        }

        _preview = new GalleryPreviewModal(
            preset,
            data,
            _store,
            _portraits,
            _i18n,
            () => _preview = null,
            _service,
            async () =>
            {
                _presets.Clear();
                _currentPage = 0;
                _hasMore = true;
                await FetchPageAsync();
            },
            _onImported);
    }

    public void receiveScrollWheelAction(int direction)
    {
        if (_preview != null)
        {
            _preview.receiveScrollWheelAction(direction);
            return;
        }
        _scrollY = Math.Clamp(_scrollY - direction, 0, Math.Max(0, _maxScroll));
        var layout = GetGridLayout();
        if (_hasMore && !_loading && _presets.Count > 0)
        {
            var lastRect = GetCardRect(_presets.Count - 1, layout);
            if (lastRect.Top < layout.GridArea.Bottom + 200)
                _ = FetchPageAsync();
        }
    }

    public bool receiveKeyPress(Keys key)
    {
        if (_preview != null)
        {
            _preview.receiveKeyPress(key);
            return true;
        }
        if (!_search.Selected)
            return false;

        if (key == Keys.Enter)
        {
            Unsubscribe();
            _ = SearchAsync();
        }
        else if (key == Keys.Escape)
        {
            Unsubscribe();
        }
        return true;
    }

    public void Unsubscribe()
    {
        _search.Selected = false;
        if (Game1.keyboardDispatcher.Subscriber == _search)
            Game1.keyboardDispatcher.Subscriber = null;
    }

    private void UpdateMaxScroll()
    {
        var layout = GetGridLayout();
        var rows = (_presets.Count + layout.Columns - 1) / layout.Columns;
        var contentHeight = rows * CardSize + Math.Max(0, rows - 1) * CardRowGap;
        _maxScroll = Math.Max(0, contentHeight - layout.GridArea.Height);
        _scrollY = Math.Clamp(_scrollY, 0, _maxScroll);
    }

    private sealed class SearchSubscriber : IKeyboardSubscriber
    {
        public bool Selected { get; set; }
        public string Text { get; set; } = "";

        public void RecieveTextInput(char inputChar)
        {
            if (Selected && !char.IsControl(inputChar) && Text.Length < 80)
                Text += inputChar;
        }

        public void RecieveTextInput(string text)
        {
            if (!Selected || string.IsNullOrEmpty(text))
                return;
            var clean = new string(text.Where(c => !char.IsControl(c)).ToArray());
            Text += clean[..Math.Min(clean.Length, Math.Max(0, 80 - Text.Length))];
        }
        if (_npcFilterOpen)
        {
            _npcDropScroll = Math.Max(0, _npcDropScroll - direction);
            return;
        }
        _scrollY = Math.Clamp(_scrollY - direction, 0, Math.Max(0, _maxScroll));
        if (_mode != 0)
            return;
        var layout = GetGridLayout();
        if (_hasMore && !_loading && _presets.Count > 0)
        {
            var lastRect = GetCardRect(_presets.Count - 1, layout);
            if (lastRect.Top < layout.GridArea.Bottom + 200)
                _ = FetchPageAsync();
        }
    }

        public void RecieveCommandInput(char command)
        {
            if (Selected && command == '\b' && Text.Length > 0)
                Text = Text[..^1];
        }

        public void RecieveSpecialInput(Keys key) { }
    }
}

public sealed class GalleryPreviewModal
{
    private readonly PresetMetadata _meta;
    private readonly PresetDownload _data;
    private readonly PersonalityStore _store;
    private readonly Dictionary<string, Texture2D?> _portraits;
    private readonly ITranslationHelper _i18n;
    private readonly Action _onClose;
    private readonly GalleryService _service;
    private readonly Func<Task> _onDeleted;
    private readonly Action _onImported;

    private Rectangle _modal;
    private Rectangle _textArea;
    private Rectangle _closeButton;
    private Rectangle _deleteButton;
    private Rectangle _importButton;
    private int _scrollY;
    private int _maxScroll;

    public GalleryPreviewModal(
        PresetMetadata meta,
        PresetDownload data,
        PersonalityStore store,
        Dictionary<string, Texture2D?> portraits,
        ITranslationHelper i18n,
        Action onClose,
        GalleryService service,
        Func<Task> onDeleted,
        Action onImported)
    {
        _npcName = npcName;
        _subtitle = subtitle;
        _entry = entry;
        _portraits = portraits;
        _i18n = i18n;
        _onClose = onClose;
        _service = service;
        _onDeleted = onDeleted;
        _onImported = onImported;
    }

    public void Draw(SpriteBatch b)
    {
        var viewport = Game1.uiViewport;
        _modal = new Rectangle(
            (viewport.Width - Math.Min(744, viewport.Width - 60)) / 2,
            (viewport.Height - Math.Min(604, viewport.Height - 60)) / 2,
            Math.Min(744, viewport.Width - 60),
            Math.Min(604, viewport.Height - 60));

        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.62f);
        b.Draw(Game1.staminaRect, _modal, new Color(255, 217, 150));
        PersonalityEditorMenu.DrawBorder(b, _modal, new Color(125, 60, 40), 6);

        const int portraitSize = 120;
        var portraitRect = new Rectangle(_modal.X + 42, _modal.Y + 38, portraitSize, portraitSize);
        var portrait = _portraits.GetValueOrDefault(_meta.NpcName);
        if (portrait != null)
            b.Draw(portrait, portraitRect, new Rectangle(0, 0, 64, 64), Color.White);
        PersonalityEditorMenu.DrawBorder(b, portraitRect, new Color(125, 60, 40), 4);

        Utility.drawTextWithShadow(b, _meta.NpcName, Game1.dialogueFont,
            new Vector2(portraitRect.Right + 20, _modal.Y + 42), Color.Black);
        var by = _i18n.Get("gallery.card.by", new { author = _meta.Author }).ToString();
        b.DrawString(Game1.smallFont, by, new Vector2(portraitRect.Right + 20, _modal.Y + 76), Color.Gray);

        _textArea = new Rectangle(_modal.X + 42, _modal.Y + 178, _modal.Width - 84, _modal.Height - 252);
        DrawFields(b);
        DrawFooter(b);
    }

    private void DrawFields(SpriteBatch b)
    {
        var previousScissor = b.GraphicsDevice.ScissorRectangle;
        var previousRasterizer = b.GraphicsDevice.RasterizerState;
        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null,
            new RasterizerState { ScissorTestEnable = true });
        b.GraphicsDevice.ScissorRectangle = _textArea;

        var y = _textArea.Y - _scrollY;
        var data = _data.Data;
        y = DrawField(b, _i18n.Get("field.appearance"), data.Appearance, y);
        y = DrawField(b, _i18n.Get("field.personality"), data.CanonicalPersonality, y);
        y = DrawField(b, _i18n.Get("field.lore"), data.Lore, y);
        y = DrawField(b, _i18n.Get("field.social_tags"), data.SocialTags, y);

        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, previousRasterizer);
        b.GraphicsDevice.ScissorRectangle = previousScissor;

        var contentHeight = y + _scrollY - _textArea.Y;
        _maxScroll = Math.Max(0, contentHeight - _textArea.Height);
        _scrollY = Math.Clamp(_scrollY, 0, _maxScroll);
        if (_maxScroll > 0)
            PersonalityEditorMenu.DrawScrollbar(b, _textArea, _scrollY, _maxScroll);
    }

    private int DrawField(SpriteBatch b, string label, string text, int y)
    {
        if (string.IsNullOrWhiteSpace(text))
            return y;
        Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(_textArea.X, y), Color.Black);
        y += 32;
        var wrapped = Game1.parseText(text, Game1.smallFont, _textArea.Width - 12);
        b.DrawString(Game1.smallFont, wrapped, new Vector2(_textArea.X + 4, y), new Color(80, 75, 80));
        y += wrapped.Split('\n').Length * (int)Game1.smallFont.MeasureString("A").Y + 18;
        return y;
    }

    private void DrawFooter(SpriteBatch b)
    {
        var y = _modal.Bottom - 76;
        _closeButton = new Rectangle(_modal.X + 16, y, 200, 58);
        _importButton = new Rectangle(_modal.Right - 216, y, 200, 58);
        DrawModalButton(b, _closeButton, _i18n.Get("button.close"), new Color(255, 248, 234));
        DrawModalButton(b, _importButton, _i18n.Get("gallery.button.import"), new Color(125, 200, 105));

        _deleteButton = Rectangle.Empty;
        if (_meta.CanDelete)
        {
            _deleteButton = new Rectangle(_modal.X + (_modal.Width - 200) / 2, y, 200, 58);
            DrawModalButton(b, _deleteButton, _i18n.Get("gallery.button.delete"), new Color(225, 125, 85));
        }
    }

    private static void DrawModalButton(SpriteBatch b, Rectangle rect, string label, Color color)
    {
        b.Draw(Game1.staminaRect, rect, color);
        PersonalityEditorMenu.DrawBorder(b, rect, new Color(125, 60, 40), 4);
        var size = Game1.smallFont.MeasureString(label);
        Utility.drawTextWithShadow(b, label, Game1.smallFont,
            new Vector2(rect.X + (rect.Width - size.X) / 2f, rect.Y + (rect.Height - size.Y) / 2f), Color.Black);
    }

    public async void receiveLeftClick(int x, int y)
    {
        if (_closeButton.Contains(x, y))
        {
            _onClose();
            return;
        }
        if (_importButton.Contains(x, y))
        {
            _store.Set(_meta.NpcName, _data.Data);
            _store.Save(_meta.NpcName);
            _onImported();
            Game1.addHUDMessage(new HUDMessage(_i18n.Get("gallery.import.success", new { npcName = _meta.NpcName }), 3));
            Game1.playSound("coin");
            _onClose();
            return;
        }
        if (!_deleteButton.IsEmpty && _deleteButton.Contains(x, y))
        {
            var deleted = await _service.DeletePresetAsync(_meta.Id);
            if (deleted)
                await _onDeleted();
            Game1.addHUDMessage(new HUDMessage(_i18n.Get(deleted ? "gallery.button.delete" : "gallery.upload.failed"), deleted ? 4 : 3));
            _onClose();
        }
    }

    public void receiveScrollWheelAction(int direction)
    {
        _scrollY = Math.Clamp(_scrollY - direction, 0, Math.Max(0, _maxScroll));
    }

    public void receiveLeftClick(int x, int y)
    {
        if (key == Keys.Escape)
            _onClose();
    }
}
