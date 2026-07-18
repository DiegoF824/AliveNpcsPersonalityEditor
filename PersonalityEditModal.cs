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
    private readonly IMonitor _monitor;
    private readonly ITranslationHelper _i18n;
    private readonly Action _onClose;

    private MultilineTextBox _nameBox = null!;
    private MultilineTextBox _appearanceBox = null!;
    private MultilineTextBox _personalityBox = null!;
    private MultilineTextBox _loreBox = null!;
    private MultilineTextBox _socialTagsBox = null!;
    private MultilineTextBox _creditBox = null!;
    private ActiveField _activeField;
    private string _originalDisplayName = "";

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
    private Rectangle _resetButton;
    private Rectangle _galleryButton;
    private Rectangle _saveButton;
    private int _scrollY;
    private int _maxScroll;

    private const int PortraitSize = 194;
    private const int SelectorH = 44;
    private const int TextBoxH = 144;
    private const int ShortBoxH = 64;      // single-line-ish fields (Name, Submission Credit)
    private const int ContentHeight = 1690;
    private const int LabelColumnW = 236;

    private const int NameY = 8;
    private const int GenderY = 140;
    private const int MannerY = 216;
    private const int AnxietyY = 302;
    private const int OptimismY = 402;
    private const int SocializeY = 504;
    private const int RomanceY = 614;
    private const int AppearanceY = 716;
    private const int PersonalityY = 926;
    private const int LoreY = 1136;
    private const int SocialTagsY = 1346;
    private const int CreditY = 1560;

    private static readonly Color Paper = new(255, 248, 234);
    private static readonly Color Border = new(125, 60, 40);
    private static readonly Color Active = new(75, 135, 50);
    private static readonly Color Inactive = new(255, 248, 234);
    private static readonly Color SaveColor = new(125, 200, 105);
    private static readonly Color CancelColor = new(225, 125, 85);
    private static readonly Color ResetColor = new(205, 160, 95);

    private static readonly string[] Genders = { "field.gender.0", "field.gender.1" };
    private static readonly string[] Manners = { "field.manner.0", "field.manner.1", "field.manner.2" };
    private static readonly string[] Anxieties = { "field.social_anxiety.0", "field.social_anxiety.1", "field.social_anxiety.2" };
    private static readonly string[] Optimisms = { "field.optimism.0", "field.optimism.1", "field.optimism.2" };

    private enum ActiveField
    {
        None,
        Name,
        Appearance,
        Personality,
        Lore,
        SocialTags,
        SubmissionCredit
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
        _ = galleryService;
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
        _nameBox = CreateTextBox(textWidth, 80, ShortBoxH);
        _appearanceBox = CreateTextBox(textWidth, 500);
        _personalityBox = CreateTextBox(textWidth, 700);
        _loreBox = CreateTextBox(textWidth, 600);
        _socialTagsBox = CreateTextBox(textWidth, 300);
        _creditBox = CreateTextBox(textWidth, 120, ShortBoxH);
        LayoutTextBoxes();
    }

    private static MultilineTextBox CreateTextBox(int width, int limit, int height = TextBoxH)
    {
        return new MultilineTextBox(new Rectangle(0, 0, width, height), Game1.smallFont, new Color(80, 70, 75))
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
        Relocate(_nameBox, x, ContentY(NameY + 36), width, ShortBoxH);
        Relocate(_appearanceBox, x, ContentY(AppearanceY + 36), width);
        Relocate(_personalityBox, x, ContentY(PersonalityY + 36), width);
        Relocate(_loreBox, x, ContentY(LoreY + 36), width);
        Relocate(_socialTagsBox, x, ContentY(SocialTagsY + 36), width);
        Relocate(_creditBox, x, ContentY(CreditY + 36), width, ShortBoxH);
    }

    private static void Relocate(MultilineTextBox box, int x, int y, int width, int height = TextBoxH)
    {
        box.Bounds = new Rectangle(x, y, width, height);
    }

    private int ContentY(int relativeY) => _scrollArea.Y + relativeY - _scrollY;

    private void LoadState()
    {
        LoadBaseCharacterData();
        var entry = _store.Get(_npcName);
        _existingCharacterData = Clone(entry?.CharacterData);
        // Default name = your override, else a mod's detected change / the real original, else the shown name.
        _originalDisplayName = entry?.CharacterData?.DisplayName ?? TryGetBaseDisplayName() ?? _displayName ?? "";
        _nameBox.Text = _originalDisplayName;
        _appearanceBox.Text = entry?.Appearance ?? "";
        _personalityBox.Text = !string.IsNullOrWhiteSpace(entry?.CanonicalPersonality)
            ? entry!.CanonicalPersonality
            : _defaultPersonality;
        _loreBox.Text = entry?.Lore ?? "";
        _socialTagsBox.Text = entry?.SocialTags ?? "";
        _creditBox.Text = entry?.SubmissionCredit ?? "";

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

    private string? TryGetBaseDisplayName()
    {
        try
        {
            var name = _api.GetBaseDisplayName(_npcName);
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch { return null; }
    }

    private void LoadBaseCharacterData()
    {
        // Prefer AliveNpcs' pre-override snapshot: it's the true default even while an
        // override is currently baked into the live Data/Characters asset (so Reset works).
        try
        {
            var snap = _api.GetBaseCharacterData(_npcName);
            if (snap is { Length: >= 7 })
            {
                _gender = snap[0];
                _age = snap[1];
                _manner = snap[2];
                _socialAnxiety = snap[3];
                _optimism = snap[4];
                _canSocialize = snap[5] != 0;
                _canBeRomanced = snap[6] != 0;
                return;
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"GetBaseCharacterData failed for {_npcName}: {ex.Message}", LogLevel.Trace);
        }

        // Fallback: read the live asset (fine when no override has ever been applied).
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
        var title = _i18n.Get("editor.editing", new { name = _displayName }).ToString();
        var size = Game1.dialogueFont.MeasureString(title);
        Utility.drawTextWithShadow(b, title, Game1.dialogueFont,
            new Vector2(xPositionOnScreen + (width - size.X) / 2f, yPositionOnScreen + 28), Color.Black);
    }

    private void DrawPortraitCard(SpriteBatch b)
    {
        EditorTheme.DrawFrame(b, _portraitCard, Paper);
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

        DrawTextField(b, NameY, "field.display_name", _nameBox);
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
        DrawTextField(b, CreditY, "field.submission_credit", _creditBox);

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
        EditorTheme.DrawFrame(b, rect, background);
        if (selected && !disabled)
            EditorTheme.DrawHighlight(b, rect);
        var size = Game1.smallFont.MeasureString(label);
        Utility.drawTextWithShadow(b, label, Game1.smallFont,
            new Vector2(rect.X + (rect.Width - size.X) / 2f, rect.Y + (rect.Height - size.Y) / 2f),
            disabled ? Color.DarkGray : selected ? Color.White : Color.Black);
    }

    private void DrawFooter(SpriteBatch b)
    {
        var y = yPositionOnScreen + height - 70;
        var sideWidth = Math.Min(210, width / 6);
        _cancelButton = new Rectangle(xPositionOnScreen + 28, y, sideWidth, 58);
        _saveButton = new Rectangle(xPositionOnScreen + width - 28 - sideWidth, y, sideWidth, 58);

        // Centre pair: Reset (restore defaults) sits directly beside Save to Gallery.
        var galleryWidth = Math.Min(320, width / 4);
        var resetWidth = Math.Min(210, width / 6);
        const int pairGap = 12;
        var pairTotal = resetWidth + pairGap + galleryWidth;
        var pairX = xPositionOnScreen + (width - pairTotal) / 2;
        _resetButton = new Rectangle(pairX, y, resetWidth, 58);
        _galleryButton = new Rectangle(pairX + resetWidth + pairGap, y, galleryWidth, 58);

        DrawFooterButton(b, _cancelButton, _i18n.Get("button.cancel"), CancelColor);
        DrawFooterButton(b, _resetButton, _i18n.Get("button.reset"), ResetColor);
        DrawFooterButton(b, _galleryButton, _i18n.Get("button.save_gallery"), Paper);
        DrawFooterButton(b, _saveButton, _i18n.Get("button.save"), SaveColor);
    }

    private static void DrawFooterButton(SpriteBatch b, Rectangle rect, string label, Color color)
        => EditorTheme.DrawButton(b, rect, label, color, Color.Black);

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        LayoutTextBoxes();
        if (_cancelButton.Contains(x, y))
        {
            Game1.playSound("bigDeSelect");
            Close();
            return;
        }
        if (_resetButton.Contains(x, y))
        {
            // Restore defaults: drop this NPC's override so the game reverts, then
            // reload the fields to show the default values.
            _store.Set(_npcName, null);
            _store.Save(_npcName);
            NotifyReload();
            LoadState();
            Game1.playSound("trashcan");
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
        yield return (ActiveField.Name, _nameBox);
        yield return (ActiveField.Appearance, _appearanceBox);
        yield return (ActiveField.Personality, _personalityBox);
        yield return (ActiveField.Lore, _loreBox);
        yield return (ActiveField.SocialTags, _socialTagsBox);
        yield return (ActiveField.SubmissionCredit, _creditBox);
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
            SubmissionCredit = _creditBox.Text.Trim(),
            CharacterData = BuildCharacterDataOverride()
        };
        return entry;
    }

    private CharacterDataOverride? BuildCharacterDataOverride()
    {
        var existing = _existingCharacterData;
        var result = new CharacterDataOverride
        {
            DisplayName = ResolveDisplayName(existing),
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

    /// <summary>
    /// Return the DisplayName to persist: the edited name when the player changed it
    /// from the value shown on open, otherwise whatever override was already stored
    /// (so an unedited field never clobbers or creates an override).
    /// </summary>
    private string? ResolveDisplayName(CharacterDataOverride? existing)
    {
        var name = _nameBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(name)
            && !string.Equals(name, _originalDisplayName.Trim(), StringComparison.Ordinal))
            return name;
        return existing?.DisplayName;
    }

    // Saves to the LOCAL preset gallery only. Sharing to the community server is an
    // explicit, opt-in action from the Gallery tab's Local view (privacy by default).
    private void SaveToGallery()
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
            Game1.playSound("coin");
        }
        catch (Exception ex)
        {
            _monitor.Log($"Save to gallery failed: {ex.Message}", LogLevel.Warn);
        }
    }

    private void ApplyPreset(NpcOverrideEntry entry)
    {
        _appearanceBox.Text = entry.Appearance ?? "";
        _personalityBox.Text = entry.CanonicalPersonality ?? "";
        _loreBox.Text = entry.Lore ?? "";
        _socialTagsBox.Text = entry.SocialTags ?? "";
        _creditBox.Text = entry.SubmissionCredit ?? "";
        if (!string.IsNullOrWhiteSpace(entry.CharacterData?.DisplayName))
            _nameBox.Text = entry.CharacterData!.DisplayName!;
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
        _nameBox.Text = values[ActiveField.Name];
        _appearanceBox.Text = values[ActiveField.Appearance];
        _personalityBox.Text = values[ActiveField.Personality];
        _loreBox.Text = values[ActiveField.Lore];
        _socialTagsBox.Text = values[ActiveField.SocialTags];
        _creditBox.Text = values[ActiveField.SubmissionCredit];
        SetActiveField(ActiveField.None);
    }

    protected override void cleanupBeforeExit()
    {
        SetActiveField(ActiveField.None);
        base.cleanupBeforeExit();
    }
}
