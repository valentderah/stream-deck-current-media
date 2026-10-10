using System.Globalization;
using System.Text;

namespace CurrentMedia;

internal static class TextFit
{
    private const string Gap = "   ";
    private const string Guard = "⁠";

    public static int Cells(string text)
    {
        var cells = 0;
        foreach (var start in StringInfo.ParseCombiningCharacters(text))
        {
            cells += Cells(Rune.GetRuneAt(text, start));
        }

        return cells;
    }

    public static string Window(string text, int offset, int maxCells) => Window(text, offset, maxCells, maxCells);

    public static string Window(string text, int offset, int fitCells, int windowCells)
    {
        if (string.IsNullOrEmpty(text) || Cells(text) <= fitCells)
        {
            return text;
        }

        var unit = text + Gap;
        var starts = StringInfo.ParseCombiningCharacters(unit);
        var period = starts.Length;

        var window = new StringBuilder();
        var cells = 0;
        for (var index = offset % period; ; index = (index + 1) % period)
        {
            var end = index + 1 < period ? starts[index + 1] : unit.Length;
            var next = cells + Cells(Rune.GetRuneAt(unit, starts[index]));
            if (next > windowCells)
            {
                break;
            }

            cells = next;
            window.Append(unit, starts[index], end - starts[index]);
        }

        return GuardEdges(window.ToString());
    }

    // Stream Deck trims whitespace at the edges of a title, which stalls a scrolling line.
    public static string GuardEdges(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var head = char.IsWhiteSpace(text[0]) ? Guard : "";
        var tail = char.IsWhiteSpace(text[^1]) ? Guard : "";
        return head + text + tail;
    }

    private static int Cells(Rune rune) => Rune.GetUnicodeCategory(rune) switch
    {
        UnicodeCategory.NonSpacingMark
            or UnicodeCategory.EnclosingMark
            or UnicodeCategory.Format
            or UnicodeCategory.Control => 0,
        _ => IsWide(rune.Value) ? 2 : 1
    };

    private static bool IsWide(int codePoint) => codePoint switch
    {
        >= 0x1100 and <= 0x115F => true,
        >= 0x2E80 and <= 0xA4CF => true,
        >= 0xAC00 and <= 0xD7A3 => true,
        >= 0xF900 and <= 0xFAFF => true,
        >= 0xFE30 and <= 0xFE4F => true,
        >= 0xFF00 and <= 0xFF60 => true,
        >= 0xFFE0 and <= 0xFFE6 => true,
        >= 0x1F300 and <= 0x1FAFF => true,
        >= 0x20000 and <= 0x3FFFD => true,
        _ => false
    };
}
