using AliveNpcsPersonalityEditor.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace AliveNpcsPersonalityEditor;

/// <summary>Full-screen NPC editor matching the supplied two-stage scrolling layout.</summary>
public sealed class PersonalityEditModal : IClickableMenu
{
    private readonly string _npcName;
    private readonly string _displayName;
    private readonly string _defaultPersonality;
    private readonly Texture2D? _portrait;
    private readonly PersonalityStore _store;
    private readonly PresetStore _presetStore;
    private readonly IAliveNpcsApi _api;
    private readonly GalleryService? _galleryService;
    private readonly IMonitor _monitor;
    private readonly ITranslationHelper _i18n;
    private readonly Action _onClose;

    private MultilineTextBox _appearanceBox = null!;
    private MultilineTextBox _personalityBox = null!;
    private MultilineTextBox _loreBox = null!;
    private MultilineTextBox _socialTagsBox = null!;
    private ActiveField _activeField;
    private string _submissionCredit = "";

    private CharacterDataOverride? _existingCharacterData;
    private int _gender = -1;
    private int _manner = -1;
    private int _socialAnxiety = -1;
    private int _optimism = -1;
    private int _age = -1;
    private bool _canSocialize;
    private bool _canBeRomanced;
    private int _originalGender = -1;
    private int _originalManner = -1;
    private int _originalSocialAnxiety = -1;
    private int _originalOptimism = -1;
    private bool _originalCanSocialize;
    private bool _originalCanBeRomanced;

    private Rectangle _portraitCard;
    private Rectangle _scrollArea;
    private Rectangle _cancelButton;
    private Rectangle _galleryButton;
    private Rectangle _saveButton;
    private int _scrollY;
    private int _maxScroll;

    private const int PortraitSize = 194;
    private const int SelectorH = 44;
    private const int TextBoxH = 144;
    private const int ContentHeight = 1410;
    private const int LabelColumnW = 236;

    private const int GenderY = 8;
    private const int MannerY = 84;
    private const int AnxietyY = 170;
    private const int OptimismY = 270;
    private const int SocializeY = 372;
    private const int RomanceY = 482;
    private const int AppearanceY = 584;
    private const int PersonalityY = 794;
    private const int LoreY = 1004;
    private const int SocialTagsY = 1214;

    private static readonly Color Paper = new(255, 248, 234);
    private static readonly Color Border = new(125, 60, 40);
    private static readonly Color Active = new(75, 135, 50);
    private static readonly Color Inactive = new(255, 248, 234);
    private static readonly Color SaveColor = new(125, 200, 105);
    private static readonly Color CancelColor = new(225, 125, 85);

    private static readonly string[] Genders = { "field.gender.0", "field.gender.1" };
    private static readonly string[] Manners = { "field.manner.0", "field.manner.1", "field.manner.2" };
    private static readonly string[] Anxieties = { "field.social_anxiety.0", "field.social_anxiety.1", "field.social_anxiety.2" };
    private static readonly string[] Optimisms = { "field.optimism.0", "field.optimism.1", "field.optimism.2" };

    private enum ActiveField
    {
        None,
        Appearance,
        Personality,
        Lore,
        SocialTags
    }

    public PersonalityEditModal(
        string npcName,
        string displayName,
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
        _displayName = displayName;
        _defaultPersonality = defaultPersonality;
        _portrait = portrait;
        _store = store;
        _presetStore = presetStore;
        _api = api;
        _galleryService = galleryService;
        _monitor = monitor;
        _i18n = i18n;
        _onClose = onClose;
        _ = config;

        RecalculateLayout();
        InitializeTextBoxes();
        LoadState();
    }

    private void RecalculateLayout()
    {
        var viewport = Game1.uiViewport;
        width = Math.Min(1104, viewport.Width - 24);
        height = Math.Min(940, viewport.Height - 24);
        xPositionOnScreen = (viewport.Width - width) / 2;
        yPositionOnScreen = (viewport.Height - height) / 2;

        _portraitCard = new Rectangle(xPositionOnScreen + 40, yPositionOnScreen + 120, PortraitSize, PortraitSize);
        var footerTop = yPositionOnScreen + height - 72;
        _scrollArea = new Rectangle(
            xPositionOnScreen + 284,
            yPositionOnScreen + 120,
            width - 328,
            footerTop - 16 - (yPositionOnScreen + 120));
        _scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, _maxScroll));
    }

    private void InitializeTextBoxes()
    {
        var textWidth = Math.Max(220, _scrollArea.Width - 34);
        _appearanceBox = CreateTextBox(textWidth, 500);
        _personalityBox = CreateTextBox(textWidth, 700);
        _loreBox = CreateTextBox(textWidth, 600);
        _socialTagsBox = CreateTextBox(textWidth, 300);
        LayoutTextBoxes();
    }

    private static MultilineTextBox CreateTextBox(int width, int limit)
    {
        return new MultilineTextBox(new Rectangle(0, 0, width, TextBoxH), Game1.smallFont, new Color(80, 70, 75))
        {
            TextLimit = limit,
            BackgroundColor = Paper,
            BorderColor = Border
        };
    }

    private void LayoutTextBoxes()
    {
        var x = _scrollArea.X;
        var width = _scrollArea.Width - 34;
        Relocate(_appearanceBox, x, ContentY(AppearanceY + 36), width);
        Relocate(_personalityBox, x, ContentY(PersonalityY + 36), width);
        Relocate(_loreBox, x, ContentY(LoreY + 36), width);
        Relocate(_socialTagsBox, x, ContentY(SocialTagsY + 36), width);
    }

    private static void Relocate(MultilineTextBox box, int x, int y, int width)
    {
        box.Bounds = new Rectangle(x, y, width, TextBoxH);
    }

    private int ContentY(int relativeY) => _scrollArea.Y + relativeY - _scrollY;

    private void LoadState()
    {
        LoadBaseCharacterData();
        var entry = _store.Get(_npcName);
        _existingCharacterData = Clone(entry?.CharacterData);
        _appearanceBox.Text = entry?.Appearance ?? "";
        _personalityBox.Text = !string.IsNullOrWhiteSpace(entry?.CanonicalPersonality)
            ? entry!.CanonicalPersonality
            : _defaultPersonality;
        _loreBox.Text = entry?.Lore ?? "";
        _socialTagsBox.Text = entry?.SocialTags ?? "";
        _submissionCredit = entry?.SubmissionCredit ?? "";

        if (entry?.CharacterData is { } data)
        {
            _gender = data.Gender ?? _gender;
            _manner = data.Manner ?? _manner;
            _socialAnxiety = data.SocialAnxiety ?? _socialAnxiety;
            _optimism = data.Optimism ?? _optimism;
            _age = data.Age ?? _age;
            _canSocialize = data.CanSocialize ?? _canSocialize;
            _canBeRomanced = data.CanBeRomanced ?? _canBeRomanced;
        }

        _originalGender = _gender;
        _originalManner = _manner;
        _originalSocialAnxiety = _socialAnxiety;
        _originalOptimism = _optimism;
        _originalCanSocialize = _canSocialize;
        _originalCanBeRomanced = _canBeRomanced;
        SetActiveField(ActiveField.None);
    }

    private void LoadBaseCharacterData()
    {
        try
        {
            var characters = Game1.content.Load<Dictionary<string, StardewValley.GameData.Characters.CharacterData>>("Data/Characters");
            if (characters.TryGetValue(_npcName, out var data))
            {
                _gender = (int)data.Gender;
                _manner = (int)data.Manner;
                _socialAnxiety = (int)data.SocialAnxiety;
                _optimism = (int)data.Optimism;
                _age = (int)data.Age;
                _canSocialize = string.Equals(data.CanSocialize, "TRUE", StringComparison.OrdinalIgnoreCase);
                _canBeRomanced = data.CanBeRomanced;
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"Could not read CharacterData for {_npcName}: {ex.Message}", LogLevel.Trace);
        }
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect,
            new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.72f);
        drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        DrawTitle(b);
        DrawPortraitCard(b);
        DrawScrollableContent(b);
        DrawFooter(b);
        drawMouse(b);
    }

    private void DrawTitle(SpriteBatch b)
    {
        var title = _i18n.Get("editor.title").ToString();
        var size = Game1.dialogueFont.MeasureString(title);
        Utility.drawTextWithShadow(b, title, Game1.dialogueFont,
            new Vector2(xPositionOnScreen + (width - size.X) / 2f, yPositionOnScreen + 28), Color.Black);
    }

    private void DrawPortraitCard(SpriteBatch b)
    {
        b.Draw(Game1.staminaRect, _portraitCard, Paper);
        PersonalityEditorMenu.DrawBorder(b, _portraitCard, Border, 4);
        var nameSize = Game1.smallFont.MeasureString(_displayName);
        Utility.drawTextWithShadow(b, _displayName, Game1.smallFont,
            new Vector2(_portraitCard.X + (_portraitCard.Width - nameSize.X) / 2f, _portraitCard.Y + 8), Color.Black);

        var portraitRect = new Rectangle(_portraitCard.X + 18, _portraitCard.Y + 43, _portraitCard.Width - 36, _portraitCard.Height - 43);
        if (_portrait != null)
            b.Draw(_portrait, portraitRect, new Rectangle(0, 0, 64, 64), Color.White);
        else
            b.Draw(Game1.staminaRect, portraitRect, Color.Black * 0.08f);
    }

    private void DrawScrollableContent(SpriteBatch b)
    {
        _maxScroll = Math.Max(0, ContentHeight - _scrollArea.Height);
        _scrollY = Math.Clamp(_scrollY, 0, _maxScroll);
        LayoutTextBoxes();

        var previousScissor = b.GraphicsDevice.ScissorRectangle;
        var previousRasterizer = b.GraphicsDevice.RasterizerState;
        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null,
            new RasterizerState { ScissorTestEnable = true });
        b.GraphicsDevice.ScissorRectangle = _scrollArea;

        DrawSelectorRow(b, GenderY, "field.gender", _gender, Genders);
        DrawSelectorRow(b, MannerY, "field.manner", _manner, Manners);
        DrawSelectorRow(b, AnxietyY, "field.social_anxiety", _socialAnxiety, Anxieties, wrapLabel: true);
        DrawSelectorRow(b, OptimismY, "field.optimism", _optimism, Optimisms);
        DrawToggleRow(b, SocializeY, "field.can_socialize", _canSocialize);
        DrawToggleRow(b, RomanceY, "field.can_be_romanced", _canBeRomanced, _age == 2);
        DrawTextField(b, AppearanceY, "field.appearance", _appearanceBox);
        DrawTextField(b, PersonalityY, "field.personality", _personalityBox);
        DrawTextField(b, LoreY, "field.lore", _loreBox);
        DrawTextField(b, SocialTagsY, "field.social_tags", _socialTagsBox);

        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, previousRasterizer);
        b.GraphicsDevice.ScissorRectangle = previousScissor;

        if (_maxScroll > 0)
            PersonalityEditorMenu.DrawScrollbar(b, _scrollArea, _scrollY, _maxScroll);
    }

    private void DrawSelectorRow(SpriteBatch b, int relativeY, string labelKey, int selected, string[] options, bool wrapLabel = false)
    {
        var y = ContentY(relativeY);
        var label = _i18n.Get(labelKey).ToString();
        if (wrapLabel)
            label = Game1.parseText(label, Game1.smallFont, LabelColumnW - 12);
        Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(_scrollArea.X, y + 5), Color.Black);

        var area = GetSelectorArea(y);
        var gap = 24;
        var buttonWidth = (area.Width - gap * (options.Length - 1)) / options.Length;
        for (var i = 0; i < options.Length; i++)
        {
            var rect = new Rectangle(area.X + i * (buttonWidth + gap), area.Y, buttonWidth, area.Height);
            DrawChoiceButton(b, rect, _i18n.Get(options[i]), selected == i, false);
        }
    }

    private void DrawToggleRow(SpriteBatch b, int relativeY, string labelKey, bool value, bool disabled = false)
    {
        var y = ContentY(relativeY);
        var label = Game1.parseText(_i18n.Get(labelKey), Game1.smallFont, LabelColumnW - 10);
        Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(_scrollArea.X, y), disabled ? Color.Gray : Color.Black);
        var rect = new Rectangle(_scrollArea.Right - 90, y, 58, SelectorH);
        DrawChoiceButton(b, rect, value ? "ON" : "OFF", value, disabled);
    }

    private void DrawTextField(SpriteBatch b, int relativeY, string labelKey, MultilineTextBox box)
    {
        Utility.drawTextWithShadow(b, _i18n.Get(labelKey), Game1.smallFont,
            new Vector2(_scrollArea.X, ContentY(relativeY)), Color.Black);
        box.Draw(b);
    }

    private Rectangle GetSelectorArea(int absoluteY)
    {
        return new Rectangle(_scrollArea.X + LabelColumnW, absoluteY, _scrollArea.Width - LabelColumnW - 34, SelectorH);
    }

    private static void DrawChoiceButton(SpriteBatch b, Rectangle rect, string label, bool selected, bool disabled)
    {
        var background = disabled ? new Color(195, 190, 175) : selected ? Active : Inactive;
        b.Draw(Game1.staminaRect, rect, background);
        PersonalityEditorMenu.DrawBorder(b, rect, disabled ? Color.Gray : Border, 4);
        var size = Game1.smallFont.MeasureString(label);
        Utility.drawTextWithShadow(b, label, Game1.smallFont,
            new Vector2(rect.X + (rect.Width - size.X) / 2f, rect.Y + (rect.Height - size.Y) / 2f),
            disabled ? Color.DarkGray : Color.Black);
    }

    private void DrawFooter(SpriteBatch b)
    {
        var y = yPositionOnScreen + height - 70;
        var sideWidth = Math.Min(234, width / 4);
        var galleryWidth = Math.Min(406, width / 3 + 40);
        _cancelButton = new Rectangle(xPositionOnScreen + 28, y, sideWidth, 58);
        _galleryButton = new Rectangle(xPositionOnScreen + (width - galleryWidth) / 2, y, galleryWidth, 58);
        _saveButton = new Rectangle(xPositionOnScreen + width - 28 - sideWidth, y, sideWidth, 58);

        DrawFooterButton(b, _cancelButton, _i18n.Get("button.cancel"), CancelColor);
        DrawFooterButton(b, _galleryButton, _i18n.Get("button.save_gallery"), Paper);
        DrawFooterButton(b, _saveButton, _i18n.Get("button.save"), SaveColor);
    }

    private static void DrawFooterButton(SpriteBatch b, Rectangle rect, string label, Color color)
    {
        b.Draw(Game1.staminaRect, rect, color);
        PersonalityEditorMenu.DrawBorder(b, rect, Border, 4);
        var size = Game1.smallFont.MeasureString(label);
        Utility.drawTextWithShadow(b, label, Game1.smallFont,
            new Vector2(rect.X + (rect.Width - size.X) / 2f, rect.Y + (rect.Height - size.Y) / 2f), Color.Black);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        LayoutTextBoxes();
        if (_cancelButton.Contains(x, y))
        {
            Game1.playSound("bigDeSelect");
            Close();
            return;
        }
        if (_galleryButton.Contains(x, y))
        {
            SaveToGallery();
            return;
        }
        if (_saveButton.Contains(x, y))
        {
            CommitAndSave();
            Game1.playSound("coin");
            Close();
            return;
        }

        if (!_scrollArea.Contains(x, y))
        {
            SetActiveField(ActiveField.None);
            return;
        }

        foreach (var (field, box) in TextBoxes())
        {
            if (!box.Bounds.Contains(x, y))
                continue;
            SetActiveField(field);
            box.SetCursorFromClick(x, y);
            return;
        }

        SetActiveField(ActiveField.None);
        if (HandleSelectorClick(x, y))
            Game1.playSound("smallSelect");
    }

    private bool HandleSelectorClick(int x, int y)
    {
        var rows = new (int RelativeY, string[] Options, Action<int> Set)[]
        {
            (GenderY, Genders, value => _gender = value),
            (MannerY, Manners, value => _manner = value),
            (AnxietyY, Anxieties, value => _socialAnxiety = value),
            (OptimismY, Optimisms, value => _optimism = value)
        };

        foreach (var row in rows)
        {
            var area = GetSelectorArea(ContentY(row.RelativeY));
            var gap = 24;
            var width = (area.Width - gap * (row.Options.Length - 1)) / row.Options.Length;
            for (var i = 0; i < row.Options.Length; i++)
            {
                if (!new Rectangle(area.X + i * (width + gap), area.Y, width, area.Height).Contains(x, y))
                    continue;
                row.Set(i);
                return true;
            }
        }

        var socialize = new Rectangle(_scrollArea.Right - 90, ContentY(SocializeY), 58, SelectorH);
        if (socialize.Contains(x, y))
        {
            _canSocialize = !_canSocialize;
            return true;
        }
        var romance = new Rectangle(_scrollArea.Right - 90, ContentY(RomanceY), 58, SelectorH);
        if (romance.Contains(x, y) && _age != 2)
        {
            _canBeRomanced = !_canBeRomanced;
            return true;
        }
        return false;
    }

    private IEnumerable<(ActiveField Field, MultilineTextBox Box)> TextBoxes()
    {
        yield return (ActiveField.Appearance, _appearanceBox);
        yield return (ActiveField.Personality, _personalityBox);
        yield return (ActiveField.Lore, _loreBox);
        yield return (ActiveField.SocialTags, _socialTagsBox);
    }

    private void SetActiveField(ActiveField field)
    {
        _activeField = field;
        foreach (var (candidate, box) in TextBoxes())
            box.Selected = candidate == field;
        var selected = TextBoxes().FirstOrDefault(pair => pair.Field == field).Box;
        Game1.keyboardDispatcher.Subscriber = selected;
    }

    public override void receiveScrollWheelAction(int direction)
    {
        LayoutTextBoxes();
        var mouse = Game1.getMousePosition();
        foreach (var (_, box) in TextBoxes())
        {
            if (box.Bounds.Contains(mouse.X, mouse.Y) && box.NeedsScroll())
            {
                box.Scroll(direction);
                return;
            }
        }
        _scrollY = Math.Clamp(_scrollY - direction, 0, Math.Max(0, _maxScroll));
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape)
        {
            Close();
            return;
        }
        if (_activeField != ActiveField.None && Game1.keyboardDispatcher.Subscriber is MultilineTextBox box)
            box.RecieveSpecialInput(key);
    }

    private void CommitAndSave()
    {
        var entry = BuildEntry(includeDefaultPersonality: false);
        _store.Set(_npcName, entry.HasAnyField ? entry : null);
        _store.Save(_npcName);
        NotifyReload();
    }

    private NpcOverrideEntry BuildEntry(bool includeDefaultPersonality)
    {
        var personality = _personalityBox.Text.Trim();
        var personalityChanged = !string.IsNullOrWhiteSpace(personality)
            && !string.Equals(personality, _defaultPersonality.Trim(), StringComparison.OrdinalIgnoreCase);

        var entry = new NpcOverrideEntry
        {
            NpcName = _npcName,
            Appearance = _appearanceBox.Text.Trim(),
            CanonicalPersonality = includeDefaultPersonality || personalityChanged ? personality : "",
            Lore = _loreBox.Text.Trim(),
            SocialTags = _socialTagsBox.Text.Trim(),
            SubmissionCredit = _submissionCredit,
            CharacterData = BuildCharacterDataOverride()
        };
        return entry;
    }

    private CharacterDataOverride? BuildCharacterDataOverride()
    {
        var existing = _existingCharacterData;
        var result = new CharacterDataOverride
        {
            DisplayName = existing?.DisplayName,
            Age = existing?.Age,
            BirthSeason = existing?.BirthSeason,
            BirthDay = existing?.BirthDay,
            CanReceiveGifts = existing?.CanReceiveGifts,
            Gender = _gender != _originalGender ? _gender : existing?.Gender,
            Manner = _manner != _originalManner ? _manner : existing?.Manner,
            SocialAnxiety = _socialAnxiety != _originalSocialAnxiety ? _socialAnxiety : existing?.SocialAnxiety,
            Optimism = _optimism != _originalOptimism ? _optimism : existing?.Optimism,
            CanSocialize = _canSocialize != _originalCanSocialize ? _canSocialize : existing?.CanSocialize,
            CanBeRomanced = _canBeRomanced != _originalCanBeRomanced ? _canBeRomanced : existing?.CanBeRomanced
        };
        return result.HasAnyField ? result : null;
    }

    private async void SaveToGallery()
    {
        try
        {
            var entry = BuildEntry(includeDefaultPersonality: true);
            if (!entry.HasAnyField)
            {
                Game1.addHUDMessage(new HUDMessage(_i18n.Get("gallery.preset.none"), 3));
                return;
            }

            _presetStore.Save(_npcName, entry);
            Game1.addHUDMessage(new HUDMessage(_i18n.Get("gallery.preset.saved", new { npcName = _displayName }), 3));
            if (_galleryService != null)
            {
                var author = Game1.player?.Name ?? "Anonymous";
                var uploaded = await _galleryService.UploadPresetAsync(_npcName, entry, author);
                Game1.addHUDMessage(new HUDMessage(_i18n.Get(uploaded ? "gallery.upload.success" : "gallery.upload.failed"), uploaded ? 4 : 3));
            }
            Game1.playSound("coin");
        }
        catch (Exception ex)
        {
            _monitor.Log($"Save to gallery failed: {ex.Message}", LogLevel.Warn);
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
        Game1.activeClickableMenu = new LocalPresetBrowser(
            presets,
            _portrait,
            _npcName,
            _i18n,
            (_, entry) => ApplyPreset(entry),
            name => _presetStore.Delete(name),
            () => Game1.activeClickableMenu = this);
    }

    private void ApplyPreset(NpcOverrideEntry entry)
    {
        _appearanceBox.Text = entry.Appearance ?? "";
        _personalityBox.Text = entry.CanonicalPersonality ?? "";
        _loreBox.Text = entry.Lore ?? "";
        _socialTagsBox.Text = entry.SocialTags ?? "";
        _submissionCredit = entry.SubmissionCredit ?? "";
        _existingCharacterData = Clone(entry.CharacterData);
        if (entry.CharacterData is { } data)
        {
            _gender = data.Gender ?? _gender;
            _manner = data.Manner ?? _manner;
            _socialAnxiety = data.SocialAnxiety ?? _socialAnxiety;
            _optimism = data.Optimism ?? _optimism;
            _age = data.Age ?? _age;
            _canSocialize = data.CanSocialize ?? _canSocialize;
            _canBeRomanced = data.CanBeRomanced ?? _canBeRomanced;
        }
    }

    private static CharacterDataOverride? Clone(CharacterDataOverride? source)
    {
        if (source == null)
            return null;
        return new CharacterDataOverride
        {
            DisplayName = source.DisplayName,
            Gender = source.Gender,
            Age = source.Age,
            Manner = source.Manner,
            SocialAnxiety = source.SocialAnxiety,
            Optimism = source.Optimism,
            BirthSeason = source.BirthSeason,
            BirthDay = source.BirthDay,
            CanSocialize = source.CanSocialize,
            CanReceiveGifts = source.CanReceiveGifts,
            CanBeRomanced = source.CanBeRomanced
        };
    }

    private void NotifyReload()
    {
        try { _api.ReloadCustomPersonalities(); }
        catch (Exception ex) { _monitor.Log($"Reload notify failed: {ex.Message}", LogLevel.Warn); }
    }

    private void Close()
    {
        SetActiveField(ActiveField.None);
        _onClose();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        var values = TextBoxes().ToDictionary(pair => pair.Field, pair => pair.Box.Text);
        RecalculateLayout();
        InitializeTextBoxes();
        _appearanceBox.Text = values[ActiveField.Appearance];
        _personalityBox.Text = values[ActiveField.Personality];
        _loreBox.Text = values[ActiveField.Lore];
        _socialTagsBox.Text = values[ActiveField.SocialTags];
        SetActiveField(ActiveField.None);
    }

    protected override void cleanupBeforeExit()
    {
        SetActiveField(ActiveField.None);
        base.cleanupBeforeExit();
    }
}
