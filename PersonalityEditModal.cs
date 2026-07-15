using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using AliveNpcsPersonalityEditor.Models;

namespace AliveNpcsPersonalityEditor;

public class PersonalityEditModal : IClickableMenu
{
    private readonly PersonalityStore _store;
    private readonly PresetStore _presetStore;
    private readonly IAliveNpcsApi _api;
    private readonly EditorConfig _config;
    private readonly GalleryService? _galleryService;
    private readonly IMonitor _monitor;
    private readonly ITranslationHelper _i18n;
    private readonly string _npcName;
    private readonly string _defaultPersonality;
    private readonly Texture2D? _portrait;
    private readonly Action _onClose;

    // ── Text boxes ──
    private MultilineTextBox _personalityBox = null!;
    private MultilineTextBox _loreBox = null!;
    private MultilineTextBox _socialTagsBox = null!;
    private MultilineTextBox _submissionCreditBox = null!;
    private MultilineTextBox _birthDayBox = null!;
    private bool _textBoxSubscribed;
    private ActiveField _activeField;

    private enum ActiveField { None, Personality, Lore, SocialTags, SubmissionCredit, BirthDay }

    // ── Character Data ──
    private int _cdGender = -1, _cdAge = -1, _cdManner = -1, _cdSocialAnxiety = -1, _cdOptimism = -1;
    private int _cdBirthSeason = -1, _cdBirthDay = -1;
    private bool _cdCanSocialize, _cdCanReceiveGifts, _cdCanBeRomanced;
    private int _origGender = -1, _origAge = -1, _origManner = -1, _origSocialAnxiety = -1, _origOptimism = -1;
    private int _origBirthSeason = -1, _origBirthDay = -1;
    private bool _origCanSocialize, _origCanReceiveGifts, _origCanBeRomanced;

    // ── Scrolling ──
    private Rectangle _scrollArea;
    private int _scrollY;
    private int _contentH;
    private int _maxScroll;

    // ── Layout constants ──
    private const int Pad = 16;
    private const int FieldGap = 8;
    private const int LabelW = 160;
    private const int FieldH = 32;
    private const int RowGap = 10;
    private const int TextBoxH = 100;
    private const int TextBoxGap = 28;
    private const int PortraitDraw = 96;
    private const int PortraitSrc = 64;

    private static readonly Color ModalBg = new(160, 125, 80);
    private static readonly Color PanelBg = new(140, 110, 70);
    private static readonly Color BtnSave = new(90, 160, 70);
    private static readonly Color BtnReset = new(190, 90, 70);
    private static readonly Color BtnClose = new(120, 100, 80);
    private static readonly Color ToggleOn = new(90, 160, 70);
    private static readonly Color ToggleOff = new(120, 100, 80);
    private static readonly Color ToggleDisabled = new(90, 85, 75);

    private static readonly string[] Genders = { "field.gender.0", "field.gender.1" };
    private static readonly string[] Ages = { "field.age.0", "field.age.1", "field.age.2" };
    private static readonly string[] Manners = { "field.manner.0", "field.manner.1", "field.manner.2" };
    private static readonly string[] Anxieties = { "field.social_anxiety.0", "field.social_anxiety.1", "field.social_anxiety.2" };
    private static readonly string[] Optimisms = { "field.optimism.0", "field.optimism.1", "field.optimism.2" };
    private static readonly string[] Seasons = { "spring", "summer", "fall", "winter" };

    public PersonalityEditModal(
        string npcName,
        string defaultPersonality,
        Texture2D? portrait,
        PersonalityStore store,
        PresetStore presetStore,
        IAliveNpcsApi api,
        EditorConfig config,
        GalleryService? galleryService,
        IMonitor monitor,
        ITranslationHelper i18n,
        Action onClose)
        : base(0, 0, 0, 0)
    {
        _npcName = npcName;
        _defaultPersonality = defaultPersonality;
        _portrait = portrait;
        _store = store;
        _presetStore = presetStore;
        _api = api;
        _config = config;
        _galleryService = galleryService;
        _monitor = monitor;
        _i18n = i18n;
        _onClose = onClose;

        RecalculateLayout();
        InitTextBoxes();
        LoadEditState();
    }

    private void RecalculateLayout()
    {
        var vw = Game1.uiViewport.Width;
        var vh = Game1.uiViewport.Height;
        width = Math.Min(620, vw - 80);
        height = Math.Min(720, vh - 60);
        xPositionOnScreen = (vw - width) / 2;
        yPositionOnScreen = (vh - height) / 2;

        var innerX = xPositionOnScreen + 16;
        var innerW = width - 32;
        var headerH = 100;
        var footerH = 56;
        _scrollArea = new Rectangle(innerX, yPositionOnScreen + headerH, innerW, height - headerH - footerH);
    }

    private void InitTextBoxes()
    {
        var fieldW = _scrollArea.Width - 16;
        var bdW = 60;

        _personalityBox = new MultilineTextBox(
            new Rectangle(0, 0, fieldW, TextBoxH), Game1.smallFont, new Color(245, 235, 210)) { Text = "", TextLimit = 600 };
        _loreBox = new MultilineTextBox(
            new Rectangle(0, 0, fieldW, TextBoxH), Game1.smallFont, new Color(245, 235, 210)) { Text = "", TextLimit = 400 };
        _socialTagsBox = new MultilineTextBox(
            new Rectangle(0, 0, fieldW, TextBoxH), Game1.smallFont, new Color(245, 235, 210)) { Text = "", TextLimit = 200 };
        _submissionCreditBox = new MultilineTextBox(
            new Rectangle(0, 0, fieldW, TextBoxH), Game1.smallFont, new Color(245, 235, 210)) { Text = "", TextLimit = 200 };
        _birthDayBox = new MultilineTextBox(
            new Rectangle(0, 0, bdW, FieldH), Game1.smallFont, new Color(245, 235, 210)) { Text = "", TextLimit = 2 };
    }

    private void LayoutTextBoxes()
    {
        var fieldW = _scrollArea.Width - 16;
        var baseX = _scrollArea.X + 8;
        var bdW = 60;

        var cursor = _scrollArea.Y + 24 - _scrollY;
        RelocateBox(_personalityBox, baseX, cursor + 20, fieldW, TextBoxH);
        cursor += 20 + TextBoxH + TextBoxGap;
        RelocateBox(_loreBox, baseX, cursor + 20, fieldW, TextBoxH);
        cursor += 20 + TextBoxH + TextBoxGap;
        RelocateBox(_socialTagsBox, baseX, cursor + 20, fieldW, TextBoxH);
        cursor += 20 + TextBoxH + TextBoxGap;
        RelocateBox(_submissionCreditBox, baseX, cursor + 20, fieldW, TextBoxH);

        // BirthDay box — right-aligned in CD row 6
        var bdRowY = CdRowY(5);
        var bdAreaW = CdBtnAreaW;
        RelocateBox(_birthDayBox, baseX + 8 + LabelW + FieldGap + bdAreaW - bdW, bdRowY, bdW, FieldH);
    }

    private static void RelocateBox(MultilineTextBox box, int x, int y, int w, int h)
    {
        box.Bounds = new Rectangle(x, y, w, h);
    }

    private void LoadEditState()
    {
        var entry = _store.Get(_npcName);

        if (entry != null)
        {
            _personalityBox.Text = !string.IsNullOrWhiteSpace(entry.CanonicalPersonality)
                ? entry.CanonicalPersonality
                : _defaultPersonality;
            _loreBox.Text = entry.Lore ?? "";
            _socialTagsBox.Text = entry.SocialTags ?? "";
            _submissionCreditBox.Text = entry.SubmissionCredit ?? "";
            var cd = entry.CharacterData;
            if (cd != null && cd.HasAnyField)
            {
                _cdGender = cd.Gender ?? -1;
                _cdAge = cd.Age ?? -1;
                _cdManner = cd.Manner ?? -1;
                _cdSocialAnxiety = cd.SocialAnxiety ?? -1;
                _cdOptimism = cd.Optimism ?? -1;
                _cdBirthSeason = cd.BirthSeason != null ? Array.IndexOf(Seasons, cd.BirthSeason) : -1;
                _cdBirthDay = cd.BirthDay ?? -1;
                _cdCanSocialize = cd.CanSocialize ?? false;
                _cdCanReceiveGifts = cd.CanReceiveGifts ?? false;
                _cdCanBeRomanced = cd.CanBeRomanced ?? false;
            }
            else
                LoadCurrentCharacterData();
        }
        else
        {
            _personalityBox.Text = _defaultPersonality;
            _loreBox.Text = "";
            _socialTagsBox.Text = "";
            _submissionCreditBox.Text = "";
            LoadCurrentCharacterData();
        }

        _origGender = _cdGender; _origAge = _cdAge; _origManner = _cdManner;
        _origSocialAnxiety = _cdSocialAnxiety; _origOptimism = _cdOptimism;
        _origBirthSeason = _cdBirthSeason; _origBirthDay = _cdBirthDay;
        _origCanSocialize = _cdCanSocialize; _origCanReceiveGifts = _cdCanReceiveGifts;
        _origCanBeRomanced = _cdCanBeRomanced;

        _birthDayBox.Text = _cdBirthDay > 0 ? _cdBirthDay.ToString() : "";

        _activeField = ActiveField.None;
        UnsubscribeTextBox();
    }

    private void LoadCurrentCharacterData()
    {
        try
        {
            var data = Game1.content.Load<Dictionary<string, StardewValley.GameData.Characters.CharacterData>>("Data/Characters");
            if (data.TryGetValue(_npcName, out var cd))
            {
                _cdGender = (int)cd.Gender;
                _cdAge = (int)cd.Age;
                _cdManner = (int)cd.Manner;
                _cdSocialAnxiety = (int)cd.SocialAnxiety;
                _cdOptimism = (int)cd.Optimism;
                _cdBirthSeason = cd.BirthSeason != null ? Array.IndexOf(Seasons, cd.BirthSeason.ToString()) : -1;
                _cdBirthDay = cd.BirthDay;
                _cdCanSocialize = string.Equals(cd.CanSocialize, "TRUE", StringComparison.OrdinalIgnoreCase);
                _cdCanReceiveGifts = cd.CanReceiveGifts;
                _cdCanBeRomanced = cd.CanBeRomanced;
                return;
            }
        }
        catch { }
        _cdGender = -1; _cdAge = -1; _cdManner = -1; _cdSocialAnxiety = -1; _cdOptimism = -1;
        _cdBirthSeason = -1; _cdBirthDay = -1;
        _cdCanSocialize = false; _cdCanReceiveGifts = false; _cdCanBeRomanced = false;
    }

    // ═══════════════════════════════════════════════════════════
    //  TEXT BOX SUBSCRIPTION
    // ═══════════════════════════════════════════════════════════

    private void SubscribeActive()
    {
        if (_textBoxSubscribed) return;
        MultilineTextBox? box = _activeField switch
        {
            ActiveField.Personality => _personalityBox,
            ActiveField.Lore => _loreBox,
            ActiveField.SocialTags => _socialTagsBox,
            ActiveField.SubmissionCredit => _submissionCreditBox,
            ActiveField.BirthDay => _birthDayBox,
            _ => null
        };
        if (box != null)
        {
            _personalityBox.Selected = false;
            _loreBox.Selected = false;
            _socialTagsBox.Selected = false;
            _submissionCreditBox.Selected = false;
            _birthDayBox.Selected = false;
            box.Selected = true;
            Game1.keyboardDispatcher.Subscriber = box;
            _textBoxSubscribed = true;
        }
    }

    private void UnsubscribeTextBox()
    {
        _personalityBox.Selected = false;
        _loreBox.Selected = false;
        _socialTagsBox.Selected = false;
        _submissionCreditBox.Selected = false;
        _birthDayBox.Selected = false;
        Game1.keyboardDispatcher.Subscriber = null;
        _textBoxSubscribed = false;
    }

    private void SetActiveField(ActiveField field)
    {
        _activeField = field;
        UnsubscribeTextBox();
        SubscribeActive();
    }

    // ═══════════════════════════════════════════════════════════
    //  CONTENT LAYOUT (one row per field, full width)
    // ═══════════════════════════════════════════════════════════

    // All Y positions are relative to the scroll area top (before _scrollY offset)
    private const int TextStartRel = 24;
    private const int CdHeaderRel =
        TextStartRel + 4 * (20 + TextBoxH + TextBoxGap) - TextBoxGap + 16;

    // 9 character data fields (Age hidden from UI), one per row
    private const int CdRowRel = CdHeaderRel + 36; // first row Y
    private const int CdRowH = FieldH + RowGap;

    private int ComputeContentHeight()
    {
        var h = CdRowRel;        // start of first CD row
        h += 9 * CdRowH;         // 9 CD rows
        h += 16;                 // bottom padding
        return h;
    }

    // Get the absolute Y of CD row i (0-8)
    private int CdRowY(int row) => _scrollArea.Y + CdRowRel + row * CdRowH - _scrollY;

    // ═══════════════════════════════════════════════════════════
    //  DRAWING
    // ═══════════════════════════════════════════════════════════

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect,
            new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
            Color.Black * 0.45f);

        drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        DrawHeader(b);
        DrawScrollableContent(b);
        DrawFooter(b);
        drawMouse(b);
    }

    private void DrawHeader(SpriteBatch b)
    {
        var px = xPositionOnScreen + 16;
        var py = yPositionOnScreen + 12;
        if (_portrait != null)
            b.Draw(_portrait, new Rectangle(px, py, PortraitDraw, PortraitDraw),
                new Rectangle(0, 0, PortraitSrc, PortraitSrc), Color.White);
        else
            b.Draw(Game1.staminaRect, new Rectangle(px, py, PortraitDraw, PortraitDraw), Color.Black * 0.1f);

        var nameSize = Game1.dialogueFont.MeasureString(_npcName);
        Utility.drawTextWithShadow(b, _npcName, Game1.dialogueFont,
            new Vector2(px + PortraitDraw + 16, py + 12), Color.SaddleBrown);

        var entry = _store.Get(_npcName);
        if (entry != null && entry.HasAnyField)
        {
            var hasPers = !string.IsNullOrWhiteSpace(entry.CanonicalPersonality);
            var hasSupp = entry.HasOnlySupplementaryFields;
            var hasCD = entry.HasCharacterDataOverride;
            string ind = hasPers ? "*" : (hasSupp ? "+" : "");
            if (hasCD) ind += " CD";
            var indColor = hasPers ? new Color(70, 140, 50) : new Color(60, 120, 180);
            Utility.drawTextWithShadow(b, ind, Game1.smallFont,
                new Vector2(px + PortraitDraw + 16 + nameSize.X + 8, py + 18), indColor);
        }
    }

    private void DrawScrollableContent(SpriteBatch b)
    {
        _contentH = ComputeContentHeight();
        _maxScroll = Math.Max(0, _contentH - _scrollArea.Height);
        _scrollY = Math.Clamp(_scrollY, 0, _maxScroll);
        LayoutTextBoxes();

        drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            _scrollArea.X, _scrollArea.Y, _scrollArea.Width, _scrollArea.Height, ModalBg);

        var prevScissor = b.GraphicsDevice.ScissorRectangle;
        var prevRasterizer = b.GraphicsDevice.RasterizerState;
        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null,
            new RasterizerState { ScissorTestEnable = true });
        b.GraphicsDevice.ScissorRectangle = _scrollArea;

        var baseX = _scrollArea.X + 8;
        var fieldW = _scrollArea.Width - 16;

        // ── Text fields (full width, one per row) ──
        DrawTextField(b, baseX, _scrollArea.Y + TextStartRel - _scrollY, fieldW,
            "field.personality", _personalityBox);
        DrawTextField(b, baseX, _scrollArea.Y + TextStartRel + (20 + TextBoxH + TextBoxGap) - _scrollY, fieldW,
            "field.lore", _loreBox);
        DrawTextField(b, baseX, _scrollArea.Y + TextStartRel + 2 * (20 + TextBoxH + TextBoxGap) - _scrollY, fieldW,
            "field.social_tags", _socialTagsBox);
        DrawTextField(b, baseX, _scrollArea.Y + TextStartRel + 3 * (20 + TextBoxH + TextBoxGap) - _scrollY, fieldW,
            "field.submission_credit", _submissionCreditBox);

        // ── Character Data panel ──
        var cdHeaderY = _scrollArea.Y + CdHeaderRel - _scrollY;
        var cdPanelX = baseX;
        var cdPanelW = fieldW;
        var cdPanelH = ComputeContentHeight() - CdHeaderRel;

        drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            cdPanelX, cdHeaderY, cdPanelW, cdPanelH, PanelBg);

        Utility.drawTextWithShadow(b, _i18n.Get("field.character_data"), Game1.smallFont,
            new Vector2(cdPanelX + 8, cdHeaderY + 6), Color.Wheat);

        // CD field rows — full width, one per row
        var btnAreaX = cdPanelX + 8 + LabelW + FieldGap;
        var btnAreaW = cdPanelW - 16 - LabelW - FieldGap;

        DrawSelectorRow(b, btnAreaX, CdRowY(0), btnAreaW, "field.gender", _cdGender, Genders);
        DrawSelectorRow(b, btnAreaX, CdRowY(1), btnAreaW, "field.manner", _cdManner, Manners);
        DrawSelectorRow(b, btnAreaX, CdRowY(2), btnAreaW, "field.social_anxiety", _cdSocialAnxiety, Anxieties);
        DrawSelectorRow(b, btnAreaX, CdRowY(3), btnAreaW, "field.optimism", _cdOptimism, Optimisms);
        DrawSeasonRow(b, btnAreaX, CdRowY(4), btnAreaW);
        DrawBirthDayRow(b, btnAreaX, CdRowY(5), btnAreaW);
        DrawToggleRow(b, btnAreaX, CdRowY(6), btnAreaW, "field.can_socialize", _cdCanSocialize);
        DrawToggleRow(b, btnAreaX, CdRowY(7), btnAreaW, "field.can_receive_gifts", _cdCanReceiveGifts);
        DrawToggleRow(b, btnAreaX, CdRowY(8), btnAreaW, "field.can_be_romanced", _cdCanBeRomanced, IsBeRomancedLocked);

        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, prevRasterizer);
        b.GraphicsDevice.ScissorRectangle = prevScissor;

        if (_maxScroll > 0)
            DrawScrollbar(b);
    }

    private void DrawTextField(SpriteBatch b, int x, int y, int w, string labelKey, MultilineTextBox box)
    {
        Utility.drawTextWithShadow(b, _i18n.Get(labelKey), Game1.smallFont,
            new Vector2(x, y), Color.Wheat);
        box.Draw(b);
    }

    private void DrawSelectorRow(SpriteBatch b, int buttonsX, int rowY, int buttonsW, string labelKey, int current, string[] options)
    {
        var labelX = _scrollArea.X + 8 + 8;
        Utility.drawTextWithShadow(b, _i18n.Get(labelKey), Game1.smallFont,
            new Vector2(labelX, rowY + 8), Color.Wheat);

        var btnW = (buttonsW - (options.Length - 1) * 6) / options.Length;
        var totalBtnW = options.Length * btnW + (options.Length - 1) * 6;
        var startX = buttonsX + buttonsW - totalBtnW;

        for (int i = 0; i < options.Length; i++)
        {
            var r = new Rectangle(startX + i * (btnW + 6), rowY, btnW, FieldH);
            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                r.X, r.Y, r.Width, r.Height, current == i ? ToggleOn : ToggleOff);
            var lbl = _i18n.Get(options[i]);
            var sz = Game1.smallFont.MeasureString(lbl);
            Utility.drawTextWithShadow(b, lbl, Game1.smallFont,
                new Vector2(r.X + (r.Width - sz.X) / 2f, r.Y + (r.Height - sz.Y) / 2f), Color.White);
        }
    }

    private void DrawSeasonRow(SpriteBatch b, int buttonsX, int rowY, int buttonsW)
    {
        var labelX = _scrollArea.X + 8 + 8;
        Utility.drawTextWithShadow(b, _i18n.Get("field.birth_season"), Game1.smallFont,
            new Vector2(labelX, rowY + 8), Color.Wheat);

        var btnW = (buttonsW - 3 * 6) / 4;
        var totalBtnW = 4 * btnW + 3 * 6;
        var startX = buttonsX + buttonsW - totalBtnW;

        for (int i = 0; i < 4; i++)
        {
            var r = new Rectangle(startX + i * (btnW + 6), rowY, btnW, FieldH);
            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                r.X, r.Y, r.Width, r.Height, _cdBirthSeason == i ? ToggleOn : ToggleOff);
            var sz = Game1.smallFont.MeasureString(Seasons[i]);
            Utility.drawTextWithShadow(b, Seasons[i], Game1.smallFont,
                new Vector2(r.X + (r.Width - sz.X) / 2f, r.Y + (r.Height - sz.Y) / 2f), Color.White);
        }
    }

    private void DrawBirthDayRow(SpriteBatch b, int buttonsX, int rowY, int buttonsW)
    {
        var labelX = _scrollArea.X + 8 + 8;
        Utility.drawTextWithShadow(b, _i18n.Get("field.birth_day"), Game1.smallFont,
            new Vector2(labelX, rowY + 8), Color.Wheat);

        _birthDayBox.Draw(b);
    }

    private void DrawToggleRow(SpriteBatch b, int buttonsX, int rowY, int buttonsW, string labelKey, bool value, bool disabled = false)
    {
        var labelX = _scrollArea.X + 8 + 8;
        var labelColor = disabled ? Color.Gray : Color.Wheat;
        Utility.drawTextWithShadow(b, _i18n.Get(labelKey), Game1.smallFont,
            new Vector2(labelX, rowY + 8), labelColor);

        var r = new Rectangle(buttonsX + buttonsW - 80, rowY, 80, FieldH);
        var bg = disabled ? ToggleDisabled : (value ? ToggleOn : ToggleOff);
        drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            r.X, r.Y, r.Width, r.Height, bg);
        var lbl = disabled ? "OFF" : (value ? "ON" : "OFF");
        var textCol = disabled ? Color.DarkGray : Color.White;
        var sz = Game1.smallFont.MeasureString(lbl);
        Utility.drawTextWithShadow(b, lbl, Game1.smallFont,
            new Vector2(r.X + (r.Width - sz.X) / 2f, r.Y + (r.Height - sz.Y) / 2f), textCol);
    }

    private void DrawScrollbar(SpriteBatch b)
    {
        var barX = _scrollArea.Right - 8;
        var barH = _scrollArea.Height;
        var thumbH = Math.Max(30, barH * barH / (barH + _maxScroll));
        var thumbY = _scrollArea.Y + (int)((float)_scrollY / _maxScroll * (barH - thumbH));

        b.Draw(Game1.staminaRect, new Rectangle(barX, _scrollArea.Y, 6, barH), Color.Black * 0.15f);
        b.Draw(Game1.staminaRect, new Rectangle(barX, thumbY, 6, thumbH), Color.SaddleBrown * 0.6f);
    }

    private static readonly Color BtnPreset = new(100, 100, 160);
    private static readonly Color BtnBrowse = new(100, 130, 160);

    private void DrawFooter(SpriteBatch b)
    {
        var btnY = yPositionOnScreen + height - 48;
        var btnW = 90;
        var gap = 8;
        var totalBtnW = btnW * 3 + gap * 2;
        var startX = xPositionOnScreen + (width - totalBtnW) / 2;

        DrawButton(b, new Rectangle(startX, btnY, btnW, 40), _i18n.Get("button.save"), BtnSave);
        DrawButton(b, new Rectangle(startX + btnW + gap, btnY, btnW, 40), _i18n.Get("button.reset"), BtnReset);
        DrawButton(b, new Rectangle(startX + 2 * (btnW + gap), btnY, btnW, 40), _i18n.Get("button.close"), BtnClose);

        var presetW = 100;
        var presetX = xPositionOnScreen + 16;
        DrawButton(b, new Rectangle(presetX, btnY, presetW, 36), _i18n.Get("gallery.button.save_preset"), BtnPreset);
        DrawButton(b, new Rectangle(presetX + presetW + gap, btnY, presetW + 8, 36), _i18n.Get("gallery.button.browse_presets"), BtnBrowse);

    }

    private static void DrawButton(SpriteBatch b, Rectangle rect, string label, Color bg)
    {
        drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            rect.X, rect.Y, rect.Width, rect.Height, bg);
        var sz = Game1.smallFont.MeasureString(label);
        Utility.drawTextWithShadow(b, label, Game1.smallFont,
            new Vector2(rect.X + (rect.Width - sz.X) / 2f, rect.Y + (rect.Height - sz.Y) / 2f), Color.White);
    }

    // ═══════════════════════════════════════════════════════════
    //  RECT HELPERS
    // ═══════════════════════════════════════════════════════════

    private Rectangle GetSaveRect() { var bY = yPositionOnScreen + height - 48; var bw = 90; var g = 8; var tw = bw * 3 + g * 2; var sx = xPositionOnScreen + (width - tw) / 2; return new(sx, bY, bw, 40); }
    private Rectangle GetResetRect() { var bY = yPositionOnScreen + height - 48; var bw = 90; var g = 8; var tw = bw * 3 + g * 2; var sx = xPositionOnScreen + (width - tw) / 2; return new(sx + bw + g, bY, bw, 40); }
    private Rectangle GetCloseRect() { var bY = yPositionOnScreen + height - 48; var bw = 90; var g = 8; var tw = bw * 3 + g * 2; var sx = xPositionOnScreen + (width - tw) / 2; return new(sx + 2 * (bw + g), bY, bw, 40); }
    private Rectangle GetSavePresetRect() { var bY = yPositionOnScreen + height - 48; return new(xPositionOnScreen + 16, bY, 100, 36); }
    private Rectangle GetBrowsePresetsRect() { var bY = yPositionOnScreen + height - 48; return new(xPositionOnScreen + 16 + 100 + 8, bY, 108, 36); }

    // CD row button area X and W (for click testing)
    private int CdBtnAreaX => _scrollArea.X + 8 + 8 + LabelW + FieldGap;
    private int CdBtnAreaW => _scrollArea.Width - 16 - LabelW - FieldGap;

    private bool IsBeRomancedLocked => _cdAge == 2;

    // ═══════════════════════════════════════════════════════════
    //  INPUT
    // ═══════════════════════════════════════════════════════════

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        // Ensure text box bounds are current (LayoutTextBoxes runs during draw,
        // but click may happen before the first draw frame)
        LayoutTextBoxes();

        if (GetSaveRect().Contains(x, y))
        {
            CommitAndSave();
            Game1.playSound("coin");
            return;
        }
        if (GetResetRect().Contains(x, y))
        {
            _store.Delete(_npcName);
            _store.Save(_npcName);
            NotifyReload();
            LoadEditState();
            Game1.playSound("trashcan");
            return;
        }
        if (GetCloseRect().Contains(x, y))
        {
            CommitAndSave();
            Close();
            return;
        }

        // Save Preset button
        if (GetSavePresetRect().Contains(x, y))
        {
            SavePreset();
            return;
        }

        // Browse Presets button
        if (GetBrowsePresetsRect().Contains(x, y))
        {
            OpenPresetBrowser();
            return;
        }



        // Text box clicks
        if (_personalityBox.Bounds.Contains(x, y))
        {
            SetActiveField(ActiveField.Personality);
            _personalityBox.SetCursorFromClick(x, y);
            return;
        }
        if (_loreBox.Bounds.Contains(x, y))
        {
            SetActiveField(ActiveField.Lore);
            _loreBox.SetCursorFromClick(x, y);
            return;
        }
        if (_socialTagsBox.Bounds.Contains(x, y))
        {
            SetActiveField(ActiveField.SocialTags);
            _socialTagsBox.SetCursorFromClick(x, y);
            return;
        }
        if (_submissionCreditBox.Bounds.Contains(x, y))
        {
            SetActiveField(ActiveField.SubmissionCredit);
            _submissionCreditBox.SetCursorFromClick(x, y);
            return;
        }
        if (_birthDayBox.Bounds.Contains(x, y))
        {
            SetActiveField(ActiveField.BirthDay);
            _birthDayBox.SetCursorFromClick(x, y);
            return;
        }

        // Check if click is within scroll area for CD rows
        if (!_scrollArea.Contains(x, y))
        {
            UnsubscribeTextBox();
            return;
        }

        UnsubscribeTextBox();
        HandleCharacterDataClick(x, y);
    }

    private void HandleCharacterDataClick(int x, int y)
    {
        var btnAreaX = CdBtnAreaX;
        var btnAreaW = CdBtnAreaW;

        // Rows 0-3: selector rows (Gender, Manner, SocialAnxiety, Optimism) — Age hidden
        var selectors = new (int row, Action<int> select, string[] options)[]
        {
            (0, i => _cdGender = i, Genders),
            (1, i => _cdManner = i, Manners),
            (2, i => _cdSocialAnxiety = i, Anxieties),
            (3, i => _cdOptimism = i, Optimisms),
        };

        foreach (var (row, select, options) in selectors)
        {
            var rowY = CdRowY(row);
            var btnW = (btnAreaW - (options.Length - 1) * 6) / options.Length;
            var totalBtnW = options.Length * btnW + (options.Length - 1) * 6;
            var startX = btnAreaX + btnAreaW - totalBtnW;

            for (int i = 0; i < options.Length; i++)
            {
                var r = new Rectangle(startX + i * (btnW + 6), rowY, btnW, FieldH);
                if (r.Contains(x, y))
                {
                    select(i);
                    Game1.playSound("smallSelect");
                    return;
                }
            }
        }

        // Row 4: Season
        var seasonRowY = CdRowY(4);
        var seasonBtnW = (btnAreaW - 3 * 6) / 4;
        var seasonTotalW = 4 * seasonBtnW + 3 * 6;
        var seasonStartX = btnAreaX + btnAreaW - seasonTotalW;
        for (int i = 0; i < 4; i++)
        {
            var r = new Rectangle(seasonStartX + i * (seasonBtnW + 6), seasonRowY, seasonBtnW, FieldH);
            if (r.Contains(x, y))
            {
                _cdBirthSeason = i;
                Game1.playSound("smallSelect");
                return;
            }
        }

        // Row 5: BirthDay is a text box, handled in receiveLeftClick

        // Rows 6-8: Toggles (right-aligned 80px)
        for (int row = 6; row <= 8; row++)
        {
            var rowY = CdRowY(row);
            var r = new Rectangle(btnAreaX + btnAreaW - 80, rowY, 80, FieldH);
            if (r.Contains(x, y))
            {
                if (row == 6) _cdCanSocialize = !_cdCanSocialize;
                else if (row == 7) _cdCanReceiveGifts = !_cdCanReceiveGifts;
                else if (row == 8)
                {
                    if (IsBeRomancedLocked) return; // locked: can't toggle
                    _cdCanBeRomanced = !_cdCanBeRomanced;
                }
                Game1.playSound("smallSelect");
                return;
            }
        }
    }

    public override void receiveScrollWheelAction(int direction)
    {
        LayoutTextBoxes();
        var mouse = Game1.getMousePosition();

        // If hovering over a text box that has scrollable content, scroll that box
        var boxes = new[] { _personalityBox, _loreBox, _socialTagsBox, _submissionCreditBox };
        foreach (var box in boxes)
        {
            if (box.Bounds.Contains(mouse.X, mouse.Y) && box.NeedsScroll())
            {
                box.Scroll(direction);
                return;
            }
        }
        if (_birthDayBox.Bounds.Contains(mouse.X, mouse.Y) && _birthDayBox.NeedsScroll())
        {
            _birthDayBox.Scroll(direction);
            return;
        }

        // Otherwise scroll the modal
        _scrollY -= direction;
        _scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, _maxScroll));
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape)
        {
            CommitAndSave();
            Close();
            return;
        }

        // Forward navigation/editing keys to the active text box
        if (_textBoxSubscribed && _activeField != ActiveField.None)
        {
            var subscriber = Game1.keyboardDispatcher.Subscriber;
            if (subscriber is MultilineTextBox box)
            {
                box.RecieveSpecialInput(key);
            }
        }
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        var personText = _personalityBox?.Text ?? "";
        var loreText = _loreBox?.Text ?? "";
        var socialTagsText = _socialTagsBox?.Text ?? "";
        var submissionCreditText = _submissionCreditBox?.Text ?? "";
        var birthDayText = _birthDayBox?.Text ?? "";
        var resub = _textBoxSubscribed;
        UnsubscribeTextBox();
        RecalculateLayout();
        InitTextBoxes();
        _personalityBox!.Text = personText;
        _loreBox!.Text = loreText;
        _socialTagsBox!.Text = socialTagsText;
        _submissionCreditBox!.Text = submissionCreditText;
        _birthDayBox!.Text = birthDayText;
        if (resub) SubscribeActive();
    }

    // ═══════════════════════════════════════════════════════════
    //  COMMIT / SAVE / CLOSE
    // ═══════════════════════════════════════════════════════════

    private void CommitAndSave()
    {
        CommitCurrentEdit();
        _store.Save(_npcName);
        NotifyReload();
    }

    private void CommitCurrentEdit()
    {
        var personalityText = _personalityBox.Text.Trim();
        var loreText = _loreBox.Text.Trim();
        var socialTagsText = _socialTagsBox.Text.Trim();
        var submissionCreditText = _submissionCreditBox.Text.Trim();

        // Parse BirthDay from text box
        int parsedBirthDay = -1;
        if (int.TryParse(_birthDayBox.Text.Trim(), out var bd) && bd >= 1 && bd <= 28)
            parsedBirthDay = bd;

        bool personalityChanged = !string.IsNullOrWhiteSpace(personalityText)
            && !string.Equals(personalityText, _defaultPersonality.Trim(), StringComparison.OrdinalIgnoreCase);
        bool loreChanged = !string.IsNullOrWhiteSpace(loreText);
        bool socialTagsChanged = !string.IsNullOrWhiteSpace(socialTagsText);
        bool submissionCreditChanged = !string.IsNullOrWhiteSpace(submissionCreditText);
        bool charDataChanged =
            _cdGender != _origGender || _cdAge != _origAge || _cdManner != _origManner
            || _cdSocialAnxiety != _origSocialAnxiety || _cdOptimism != _origOptimism
            || _cdBirthSeason != _origBirthSeason
            || parsedBirthDay != _origBirthDay
            || _cdCanSocialize != _origCanSocialize || _cdCanReceiveGifts != _origCanReceiveGifts
            || _cdCanBeRomanced != _origCanBeRomanced;

        if (!personalityChanged && !loreChanged && !socialTagsChanged && !submissionCreditChanged && !charDataChanged)
        {
            _store.Set(_npcName, null);
            return;
        }

        var entry = new NpcOverrideEntry
        {
            CanonicalPersonality = personalityChanged ? personalityText : "",
            Lore = loreChanged ? loreText : "",
            SocialTags = socialTagsChanged ? socialTagsText : "",
            SubmissionCredit = submissionCreditChanged ? submissionCreditText : ""
        };

        if (charDataChanged)
        {
            entry.CharacterData = new CharacterDataOverride
            {
                Gender = _cdGender >= 0 ? _cdGender : null,
                Age = _cdAge >= 0 ? _cdAge : null,
                Manner = _cdManner >= 0 ? _cdManner : null,
                SocialAnxiety = _cdSocialAnxiety >= 0 ? _cdSocialAnxiety : null,
                Optimism = _cdOptimism >= 0 ? _cdOptimism : null,
                BirthSeason = _cdBirthSeason >= 0 ? Seasons[_cdBirthSeason] : null,
                BirthDay = parsedBirthDay > 0 ? parsedBirthDay : null,
                CanSocialize = _cdCanSocialize ? true : null,
                CanReceiveGifts = _cdCanReceiveGifts ? true : null,
                CanBeRomanced = _cdCanBeRomanced ? true : null
            };
        }

        _store.Set(_npcName, entry);
    }

    private void NotifyReload()
    {
        try { _api.ReloadCustomPersonalities(); }
        catch (Exception ex) { _monitor.Log($"Reload notify failed: {ex.Message}", LogLevel.Warn); }
    }

    private void Close()
    {
        UnsubscribeTextBox();
        _onClose();
        if (Game1.activeClickableMenu == this)
            Game1.activeClickableMenu = null;
    }

    private NpcOverrideEntry BuildCurrentEntry()
    {
        var entry = new NpcOverrideEntry { NpcName = _npcName };
        if (!string.IsNullOrWhiteSpace(_personalityBox.Text))
            entry.CanonicalPersonality = _personalityBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(_loreBox.Text))
            entry.Lore = _loreBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(_socialTagsBox.Text))
            entry.SocialTags = _socialTagsBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(_submissionCreditBox.Text))
            entry.SubmissionCredit = _submissionCreditBox.Text.Trim();

        if (_cdGender >= 0 || _cdManner >= 0 || _cdSocialAnxiety >= 0 || _cdOptimism >= 0
            || !string.IsNullOrWhiteSpace(_birthDayBox.Text) || _cdBirthSeason >= 0
            || _cdCanSocialize || _cdCanReceiveGifts || _cdCanBeRomanced)
        {
            entry.CharacterData = new CharacterDataOverride
            {
                Gender = _cdGender >= 0 ? _cdGender : null,
                Age = _cdAge >= 0 ? _cdAge : null,
                Manner = _cdManner >= 0 ? _cdManner : null,
                SocialAnxiety = _cdSocialAnxiety >= 0 ? _cdSocialAnxiety : null,
                Optimism = _cdOptimism >= 0 ? _cdOptimism : null,
                BirthSeason = _cdBirthSeason >= 0 ? Seasons[_cdBirthSeason] : null,
                BirthDay = int.TryParse(_birthDayBox.Text, out var bd) ? bd : null,
                CanSocialize = _origCanSocialize != _cdCanSocialize ? _cdCanSocialize : null,
                CanReceiveGifts = _origCanReceiveGifts != _cdCanReceiveGifts ? _cdCanReceiveGifts : null,
                CanBeRomanced = _origCanBeRomanced != _cdCanBeRomanced ? _cdCanBeRomanced : null
            };
        }
        return entry;
    }

    private void SavePreset()
    {
        try
        {
            var entry = BuildCurrentEntry();
            if (!entry.HasAnyField)
            {
                Game1.addHUDMessage(new HUDMessage(_i18n.Get("gallery.preset.none"), 3));
                return;
            }
            _presetStore.Save(_npcName, entry);
            Game1.addHUDMessage(new HUDMessage(_i18n.Get("gallery.preset.saved", new { npcName = _npcName }), 3));
            Game1.playSound("coin");
        }
        catch (Exception ex)
        {
            _monitor.Log($"Save preset failed: {ex.Message}", LogLevel.Warn);
        }
    }

    private void OpenPresetBrowser()
    {
        var presets = _presetStore.LoadAll();
        if (presets.Count == 0)
        {
            Game1.addHUDMessage(new HUDMessage(_i18n.Get("gallery.preset.none"), 3));
            return;
        }

        var browser = new LocalPresetBrowser(
            presets, _portrait, _npcName, _i18n,
            (npcName, entry) =>
            {
                ApplyPreset(entry);
            },
            (npcName) =>
            {
                _presetStore.Delete(npcName);
            },
            () =>
            {
                Game1.activeClickableMenu = this;
                if (_textBoxSubscribed)
                    ResubscribeActiveBox();
            });

        Game1.activeClickableMenu = browser;
    }

    private void ApplyPreset(NpcOverrideEntry entry)
    {
        _personalityBox.Text = entry.CanonicalPersonality ?? "";
        _loreBox.Text = entry.Lore ?? "";
        _socialTagsBox.Text = entry.SocialTags ?? "";
        _submissionCreditBox.Text = entry.SubmissionCredit ?? "";

        if (entry.CharacterData != null)
        {
            var cd = entry.CharacterData;
            _cdGender = cd.Gender ?? -1;
            _cdManner = cd.Manner ?? -1;
            _cdSocialAnxiety = cd.SocialAnxiety ?? -1;
            _cdOptimism = cd.Optimism ?? -1;
            _cdBirthSeason = cd.BirthSeason != null ? Array.IndexOf(Seasons, cd.BirthSeason) : -1;
            _cdBirthDay = cd.BirthDay ?? -1;
            _birthDayBox.Text = cd.BirthDay?.ToString() ?? "";
            _cdCanSocialize = cd.CanSocialize ?? false;
            _cdCanReceiveGifts = cd.CanReceiveGifts ?? false;
            _cdCanBeRomanced = cd.CanBeRomanced ?? false;
        }

        Game1.addHUDMessage(new HUDMessage(_i18n.Get("gallery.preset.applied", new { npcName = _npcName }), 3));
    }

    private void ResubscribeActiveBox()
    {
        switch (_activeField)
        {
            case ActiveField.Personality:
                SetActiveField(ActiveField.Personality);
                break;
            case ActiveField.Lore:
                SetActiveField(ActiveField.Lore);
                break;
            case ActiveField.SocialTags:
                SetActiveField(ActiveField.SocialTags);
                break;
            case ActiveField.SubmissionCredit:
                SetActiveField(ActiveField.SubmissionCredit);
                break;
            case ActiveField.BirthDay:
                SetActiveField(ActiveField.BirthDay);
                break;
        }
    }

    protected override void cleanupBeforeExit()
    {
        UnsubscribeTextBox();
        base.cleanupBeforeExit();
    }
}
