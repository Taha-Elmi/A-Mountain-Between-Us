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
        Oval,    // an elliptical bubble, for voices on a black page
        Plain,   // the words alone, light on the dark: no box
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

    // A page as the script writes it: which picture it shows, and its words.
    public class ScriptPage
    {
        public const string Black = "black";

        public string image;      // a file name in the comic's folder, "black" for none, or empty for the next image
        public float fadeSeconds; // how long the page takes to fade in; 0 for the comic's usual page turn
        public List<ComicLine> lines = new();

        public bool IsBlack => string.Equals(image, Black, StringComparison.OrdinalIgnoreCase);
    }

    // Reads a comic's script file (Assets/Art/Comics/<name> Script.txt):
    //
    //   == Page 1 (panel1.jpg) fade 3 ==                    a new page showing that image (or
    //                                                       (black) for none), fading in over 3 s
    //   [Farhad] speech at 62 15 width 30 tail 55 40        who speaks, then optionally the box's
    //   fa: ...                                             style, centre, width and tail tip
    //   en: ...                                             (all in percent of the page)
    //
    // Lines starting with # are notes. A second fa: or en: row starts a new row of that text.
    // [Narration] lines are captions unless told otherwise, everyone else speaks in bubbles.
    public static class ComicScript
    {
        // The pages, in order: pages[0] is the first page.
        public static List<ScriptPage> Parse(string text, List<string> problems = null)
        {
            var pages = new List<ScriptPage>();
            if (string.IsNullOrEmpty(text)) return pages;

            ScriptPage page = null;
            ComicLine line = null;
            string[] rows = text.Replace("\r", "").Split('\n');
            for (int n = 0; n < rows.Length; n++)
            {
                string row = rows[n].Trim();
                if (row.Length == 0 || row.StartsWith("#")) continue;

                if (row.StartsWith("=="))
                {
                    string header = row.Trim('=', ' ');
                    string image = null;
                    int open = header.IndexOf('('), close = header.IndexOf(')');
                    if (open >= 0 && close > open)
                    {
                        image = header.Substring(open + 1, close - open - 1).Trim();
                        header = header.Remove(open, close - open + 1);
                    }
                    string[] words = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    int number = pages.Count + 1;
                    if (words.Length >= 2 && int.TryParse(words[1], out int given)) number = given;
                    if (number < 1)
                    {
                        problems?.Add($"Row {n + 1}: page {number} doesn't exist; pages start at 1.");
                        number = pages.Count + 1;
                    }
                    while (pages.Count < number) pages.Add(new ScriptPage());
                    page = pages[number - 1];
                    page.image = image;
                    for (int i = 2; i < words.Length; i++)
                    {
                        if (words[i].Equals("fade", StringComparison.OrdinalIgnoreCase) && Numbers(words, i, 1, out var fade))
                        {
                            page.fadeSeconds = Mathf.Max(0f, fade[0]);
                            i += 1;
                        }
                        else problems?.Add($"Row {n + 1}: didn't understand \"{words[i]}\" (expected (image), then optionally fade S).");
                    }
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
                        pages.Add(page = new ScriptPage());
                    }
                    line = new ComicLine { speaker = row.Substring(1, close - 1).Trim() };
                    line.style = line.speaker.Equals("Narration", StringComparison.OrdinalIgnoreCase) ? LineStyle.Caption : LineStyle.Speech;
                    ReadPlacement(row.Substring(close + 1), line, n + 1, problems);
                    page.lines.Add(line);
                }
                else if (row.StartsWith("fa:") || row.StartsWith("en:"))
                {
                    if (line == null)
                    {
                        problems?.Add($"Row {n + 1}: text before any [Speaker] line.");
                        continue;
                    }
                    string words = row.Substring(3).Trim();
                    static string Add(string text, string more) => string.IsNullOrEmpty(text) ? more : text + "\n" + more;
                    if (row.StartsWith("fa:")) line.persian = Add(line.persian, words);
                    else line.english = Add(line.english, words);
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
                    case "oval": line.style = LineStyle.Oval; break;
                    case "plain": line.style = LineStyle.Plain; break;
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
                        problems?.Add($"Row {row}: didn't understand \"{words[i]}\" (expected caption, speech, oval, plain, at X Y, width W or tail X Y).");
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
