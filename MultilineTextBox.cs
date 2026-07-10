using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace AliveNpcsPersonalityEditor;

/// <summary>A small multiline editor for menus. The game's built-in TextBox only renders one line.</summary>
internal sealed class MultilineTextBox : IKeyboardSubscriber
{
    private readonly SpriteFont _font;
    private readonly Color _textColor;
    private string _text = "";
    private int _cursor;

    public Rectangle Bounds { get; }
    public string Text
    {
        get => _text;
        set
        {
            _text = (value ?? "")
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            _cursor = _text.Length;
        }
    }
    public int TextLimit { get; set; } = 100000;
    public bool Selected { get; set; }

    public MultilineTextBox(Rectangle bounds, SpriteFont font, Color textColor)
    {
        Bounds = bounds;
        _font = font;
        _textColor = textColor;
    }

    public void Draw(SpriteBatch b)
    {
        var lines = GetWrappedLines();
        var lineHeight = (int)_font.MeasureString("A").Y;
        var visibleLines = Math.Max(1, (Bounds.Height - 8) / lineHeight);
        var firstLine = Math.Max(0, lines.Count - visibleLines);

        b.Draw(Game1.staminaRect, Bounds, Color.Black * 0.18f);
        b.Draw(Game1.staminaRect, new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, 1), Color.Wheat * 0.75f);
        b.Draw(Game1.staminaRect, new Rectangle(Bounds.X, Bounds.Bottom - 1, Bounds.Width, 1), Color.Wheat * 0.75f);

        for (var i = firstLine; i < lines.Count; i++)
        {
            var line = lines[i];
            var display = Text.Substring(line.Start, line.Length).TrimEnd();
            var y = Bounds.Y + 4 + (i - firstLine) * lineHeight;
            b.DrawString(_font, display, new Vector2(Bounds.X + 4, y), _textColor);
        }

        if (!Selected || DateTime.UtcNow.Millisecond >= 500)
            return;

        var cursorLine = FindCursorLine(lines);
        if (cursorLine < firstLine || cursorLine >= firstLine + visibleLines)
            cursorLine = lines.Count - 1;
        var current = lines[cursorLine];
        var charactersBeforeCursor = Math.Clamp(_cursor - current.Start, 0, current.Length);
        var before = Text.Substring(current.Start, charactersBeforeCursor).TrimEnd();
        var caretX = Bounds.X + 4 + (int)_font.MeasureString(before).X;
        var caretY = Bounds.Y + 4 + (cursorLine - firstLine) * lineHeight;
        b.Draw(Game1.staminaRect, new Rectangle(caretX, caretY, 2, lineHeight), _textColor);
    }

    public void SetCursorFromClick(int x, int y)
    {
        var lines = GetWrappedLines();
        var lineHeight = (int)_font.MeasureString("A").Y;
        var visibleLines = Math.Max(1, (Bounds.Height - 8) / lineHeight);
        var firstLine = Math.Max(0, lines.Count - visibleLines);
        var lineIndex = Math.Clamp(firstLine + (y - Bounds.Y - 4) / lineHeight, firstLine, lines.Count - 1);
        var line = lines[lineIndex];
        var targetX = Math.Max(0, x - Bounds.X - 4);
        var position = line.Start;

        while (position < line.Start + line.Length
            && _font.MeasureString(Text.Substring(line.Start, position - line.Start + 1)).X <= targetX)
        {
            position++;
        }

        _cursor = position;
    }

    public void RecieveTextInput(char inputChar)
    {
        if (!char.IsControl(inputChar))
            Insert(inputChar.ToString());
    }

    public void RecieveTextInput(string text)
    {
        if (!string.IsNullOrEmpty(text))
            Insert(text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'));
    }

    public void RecieveCommandInput(char command)
    {
        if (command == '\b')
            Backspace();
    }

    public void RecieveSpecialInput(Keys key)
    {
        switch (key)
        {
            case Keys.Back:
                Backspace();
                break;
            case Keys.Delete:
                if (_cursor < Text.Length)
                    _text = Text.Remove(_cursor, 1);
                break;
            case Keys.Left:
                _cursor = Math.Max(0, _cursor - 1);
                break;
            case Keys.Right:
                _cursor = Math.Min(Text.Length, _cursor + 1);
                break;
            case Keys.Home:
                _cursor = 0;
                break;
            case Keys.End:
                _cursor = Text.Length;
                break;
            case Keys.Enter:
                Insert("\n");
                break;
        }
    }

    private void Insert(string value)
    {
        if (Text.Length >= TextLimit)
            return;

        value = value[..Math.Min(value.Length, TextLimit - Text.Length)];
        _text = Text.Insert(_cursor, value);
        _cursor += value.Length;
    }

    private void Backspace()
    {
        if (_cursor == 0)
            return;

        _text = Text.Remove(_cursor - 1, 1);
        _cursor--;
    }

    private int FindCursorLine(IReadOnlyList<WrappedLine> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (_cursor <= line.Start + line.Length || i == lines.Count - 1)
                return i;
        }

        return lines.Count - 1;
    }

    private List<WrappedLine> GetWrappedLines()
    {
        var lines = new List<WrappedLine>();
        var maxWidth = Bounds.Width - 8;
        var start = 0;

        while (start < Text.Length)
        {
            var newline = Text.IndexOf('\n', start);
            var paragraphEnd = newline >= 0 ? newline : Text.Length;
            var position = start;

            if (position == paragraphEnd)
                lines.Add(new WrappedLine(start, 0));

            while (position < paragraphEnd)
            {
                var end = position;
                var lastWhitespace = -1;
                while (end < paragraphEnd && _font.MeasureString(Text.Substring(position, end - position + 1)).X <= maxWidth)
                {
                    if (char.IsWhiteSpace(Text[end]))
                        lastWhitespace = end;
                    end++;
                }

                if (end == position)
                    end++;

                if (end < paragraphEnd && lastWhitespace >= position)
                {
                    lines.Add(new WrappedLine(position, lastWhitespace - position));
                    position = lastWhitespace + 1;
                }
                else
                {
                    lines.Add(new WrappedLine(position, end - position));
                    position = end;
                }
            }

            start = newline >= 0 ? newline + 1 : Text.Length;
        }

        if (lines.Count == 0)
            lines.Add(new WrappedLine(0, 0));
        else if (Text.EndsWith('\n'))
            lines.Add(new WrappedLine(Text.Length, 0));

        return lines;
    }

    private readonly record struct WrappedLine(int Start, int Length);
}
