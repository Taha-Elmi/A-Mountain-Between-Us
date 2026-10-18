using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Farhad
{
    public enum LineStyle
    {
        Caption, // a square box: narration, or Farhad telling the story
        Speech,  // a rounded bubble, with a tail pointing at whoever speaks
    }

    // One line of a comic page in both languages, and where it sits on the page.
    [Serializable]
    public class ComicLine
    {
        public string speaker;
        public LineStyle style;
        public string persian;
        public string english;
        [Tooltip("Centre of the box, in percent of the page from its top-left corner.")]
        public Vector2 at = new(50f, 12f);
        [Tooltip("The widest the box may grow, in percent of the page's width.")]
        public float width = 40f;
        public bool hasTail;
        [Tooltip("Where the tail points, in percent of the page from its top-left corner.")]
        public Vector2 tail;

        public string Text(Lang lang)
        {
            string text = lang == Lang.Persian ? persian : english;
            return string.IsNullOrEmpty(text) ? (lang == Lang.Persian ? english : persian) : text;
        }
    }

    // Reads a comic's script file (Assets/Art/Comics/<name> Script.txt):
    //
    //   == Page 1 ==                                        a new page; lines follow in order
    //   [Farhad] speech at 62 15 width 30 tail 55 40        who speaks, then optionally the box's
    //   fa: ...                                             style, centre, width and tail tip
    //   en: ...                                             (all in percent of the page)
    //
    // Lines starting with # are notes. [Narration] lines are captions unless told otherwise,
    // everyone else speaks in bubbles.
    public static class ComicScript
    {
        // The lines of each page, in order: pages[0] is the first page.
        public static List<List<ComicLine>> Parse(string text, List<string> problems = null)
        {
            var pages = new List<List<ComicLine>>();
            if (string.IsNullOrEmpty(text)) return pages;

            List<ComicLine> page = null;
            ComicLine line = null;
            string[] rows = text.Replace("\r", "").Split('\n');
            for (int n = 0; n < rows.Length; n++)
            {
                string row = rows[n].Trim();
                if (row.Length == 0 || row.StartsWith("#")) continue;

                if (row.StartsWith("=="))
                {
                    string[] words = row.Trim('=', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    int number = pages.Count + 1;
                    if (words.Length >= 2 && int.TryParse(words[1], out int given)) number = given;
                    if (number < 1)
                    {
                        problems?.Add($"Row {n + 1}: page {number} doesn't exist; pages start at 1.");
                        number = pages.Count + 1;
                    }
                    while (pages.Count < number) pages.Add(new List<ComicLine>());
                    page = pages[number - 1];
                    line = null;
                }
                else if (row.StartsWith("["))
                {
                    int close = row.IndexOf(']');
                    if (close < 0)
                    {
                        problems?.Add($"Row {n + 1}: the speaker's name needs a closing ].");
                        continue;
                    }
                    if (page == null)
                    {
                        pages.Add(page = new List<ComicLine>());
                    }
                    line = new ComicLine { speaker = row.Substring(1, close - 1).Trim() };
                    line.style = line.speaker.Equals("Narration", StringComparison.OrdinalIgnoreCase) ? LineStyle.Caption : LineStyle.Speech;
                    ReadPlacement(row.Substring(close + 1), line, n + 1, problems);
                    page.Add(line);
                }
                else if (row.StartsWith("fa:") || row.StartsWith("en:"))
                {
                    if (line == null)
                    {
                        problems?.Add($"Row {n + 1}: text before any [Speaker] line.");
                        continue;
                    }
                    string words = row.Substring(3).Trim();
                    if (row.StartsWith("fa:")) line.persian = words;
                    else line.english = words;
                }
                else
                {
                    problems?.Add($"Row {n + 1}: not a page, speaker or fa:/en: line: \"{row}\"");
                }
            }
            return pages;
        }

        static void ReadPlacement(string rest, ComicLine line, int row, List<string> problems)
        {
            string[] words = rest.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                switch (words[i].ToLowerInvariant())
                {
                    case "caption": line.style = LineStyle.Caption; break;
                    case "speech": line.style = LineStyle.Speech; break;
                    case "at" when Numbers(words, i, 2, out var at):
                        line.at = new Vector2(at[0], at[1]);
                        i += 2;
                        break;
                    case "width" when Numbers(words, i, 1, out var width):
                        line.width = width[0];
                        i += 1;
                        break;
                    case "tail" when Numbers(words, i, 2, out var tail):
                        line.hasTail = true;
                        line.tail = new Vector2(tail[0], tail[1]);
                        i += 2;
                        break;
                    default:
                        problems?.Add($"Row {row}: didn't understand \"{words[i]}\" (expected caption, speech, at X Y, width W or tail X Y).");
                        break;
                }
            }
        }

        static bool Numbers(string[] words, int i, int count, out float[] values)
        {
            values = new float[count];
            if (i + count >= words.Length) return false;
            for (int k = 0; k < count; k++)
                if (!float.TryParse(words[i + 1 + k], NumberStyles.Float, CultureInfo.InvariantCulture, out values[k]))
                    return false;
            return true;
        }
    }
}
