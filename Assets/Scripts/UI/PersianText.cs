using System.Collections.Generic;
using System.Text;

namespace Farhad
{
    // Unity's UI Text draws characters one by one, left to right, and doesn't join Arabic-script
    // letters. Shape swaps each Persian letter for its joined form (the Unicode presentation forms
    // the font carries), and Visual turns one finished line into left-to-right drawing order. Made
    // for Persian sentences; runs of Latin letters or digits inside them would come out reversed.
    public static class PersianText
    {
        const char Zwnj = '‌'; // the half-space: keeps a word's parts apart without a space

        // Each letter's forms: alone, last (joined to the letter before), first (joined to the
        // letter after), middle (joined on both sides). Letters with two only join to the one before.
        static readonly Dictionary<char, char[]> Forms = new()
        {
            ['ء'] = new[] { 'ﺀ' },                               // ء
            ['آ'] = new[] { 'ﺁ', 'ﺂ' },                     // آ
            ['أ'] = new[] { 'ﺃ', 'ﺄ' },                     // أ
            ['ؤ'] = new[] { 'ﺅ', 'ﺆ' },                     // ؤ
            ['إ'] = new[] { 'ﺇ', 'ﺈ' },                     // إ
            ['ئ'] = new[] { 'ﺉ', 'ﺊ', 'ﺋ', 'ﺌ' }, // ئ
            ['ا'] = new[] { 'ﺍ', 'ﺎ' },                     // ا
            ['ب'] = new[] { 'ﺏ', 'ﺐ', 'ﺑ', 'ﺒ' }, // ب
            ['ة'] = new[] { 'ﺓ', 'ﺔ' },                     // ة
            ['ت'] = new[] { 'ﺕ', 'ﺖ', 'ﺗ', 'ﺘ' }, // ت
            ['ث'] = new[] { 'ﺙ', 'ﺚ', 'ﺛ', 'ﺜ' }, // ث
            ['ج'] = new[] { 'ﺝ', 'ﺞ', 'ﺟ', 'ﺠ' }, // ج
            ['ح'] = new[] { 'ﺡ', 'ﺢ', 'ﺣ', 'ﺤ' }, // ح
            ['خ'] = new[] { 'ﺥ', 'ﺦ', 'ﺧ', 'ﺨ' }, // خ
            ['د'] = new[] { 'ﺩ', 'ﺪ' },                     // د
            ['ذ'] = new[] { 'ﺫ', 'ﺬ' },                     // ذ
            ['ر'] = new[] { 'ﺭ', 'ﺮ' },                     // ر
            ['ز'] = new[] { 'ﺯ', 'ﺰ' },                     // ز
            ['س'] = new[] { 'ﺱ', 'ﺲ', 'ﺳ', 'ﺴ' }, // س
            ['ش'] = new[] { 'ﺵ', 'ﺶ', 'ﺷ', 'ﺸ' }, // ش
            ['ص'] = new[] { 'ﺹ', 'ﺺ', 'ﺻ', 'ﺼ' }, // ص
            ['ض'] = new[] { 'ﺽ', 'ﺾ', 'ﺿ', 'ﻀ' }, // ض
            ['ط'] = new[] { 'ﻁ', 'ﻂ', 'ﻃ', 'ﻄ' }, // ط
            ['ظ'] = new[] { 'ﻅ', 'ﻆ', 'ﻇ', 'ﻈ' }, // ظ
            ['ع'] = new[] { 'ﻉ', 'ﻊ', 'ﻋ', 'ﻌ' }, // ع
            ['غ'] = new[] { 'ﻍ', 'ﻎ', 'ﻏ', 'ﻐ' }, // غ
            ['ـ'] = new[] { 'ـ', 'ـ', 'ـ', 'ـ' }, // ـ (stretch)
            ['ف'] = new[] { 'ﻑ', 'ﻒ', 'ﻓ', 'ﻔ' }, // ف
            ['ق'] = new[] { 'ﻕ', 'ﻖ', 'ﻗ', 'ﻘ' }, // ق
            ['ك'] = new[] { 'ﻙ', 'ﻚ', 'ﻛ', 'ﻜ' }, // ك
            ['ل'] = new[] { 'ﻝ', 'ﻞ', 'ﻟ', 'ﻠ' }, // ل
            ['م'] = new[] { 'ﻡ', 'ﻢ', 'ﻣ', 'ﻤ' }, // م
            ['ن'] = new[] { 'ﻥ', 'ﻦ', 'ﻧ', 'ﻨ' }, // ن
            ['ه'] = new[] { 'ﻩ', 'ﻪ', 'ﻫ', 'ﻬ' }, // ه
            ['و'] = new[] { 'ﻭ', 'ﻮ' },                     // و
            ['ى'] = new[] { 'ﻯ', 'ﻰ' },                     // ى
            ['ي'] = new[] { 'ﻱ', 'ﻲ', 'ﻳ', 'ﻴ' }, // ي
            ['پ'] = new[] { 'ﭖ', 'ﭗ', 'ﭘ', 'ﭙ' }, // پ
            ['چ'] = new[] { 'ﭺ', 'ﭻ', 'ﭼ', 'ﭽ' }, // چ
            ['ژ'] = new[] { 'ﮊ', 'ﮋ' },                     // ژ
            ['ک'] = new[] { 'ﮎ', 'ﮏ', 'ﮐ', 'ﮑ' }, // ک
            ['گ'] = new[] { 'ﮒ', 'ﮓ', 'ﮔ', 'ﮕ' }, // گ
            ['ۀ'] = new[] { 'ۀ', 'ﮥ' },                     // ۀ (Vazirmatn has no FBA4)
            ['ی'] = new[] { 'ﯼ', 'ﯽ', 'ﯾ', 'ﯿ' }, // ی
        };

        // Lam followed by an alef becomes one letter: alone, last.
        static readonly Dictionary<char, char[]> LamAlef = new()
        {
            ['آ'] = new[] { 'ﻵ', 'ﻶ' },
            ['أ'] = new[] { 'ﻷ', 'ﻸ' },
            ['إ'] = new[] { 'ﻹ', 'ﻺ' },
            ['ا'] = new[] { 'ﻻ', 'ﻼ' },
        };

        // Brackets and quotes point the other way in right-to-left text.
        static readonly Dictionary<char, char> Mirrors = new()
        {
            ['('] = ')', [')'] = '(', ['['] = ']', [']'] = '[', ['{'] = '}', ['}'] = '{',
            ['<'] = '>', ['>'] = '<', ['«'] = '»', ['»'] = '«',
        };

        // Vowel marks (like the ezafe kasra) sit on a letter without taking room or breaking joins.
        static bool IsMark(char c) => c >= 'ً' && c <= 'ٟ' || c == 'ٰ' || c >= 'ۖ' && c <= 'ۭ';

        // Joined letters, still in reading order (first letter first).
        public static string Shape(string text)
        {
            text = text.Replace("هٔ", "ۀ"); // ه with a hamza above is its own letter, ۀ
            var shaped = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (!Forms.TryGetValue(c, out var forms))
                {
                    if (c != Zwnj) shaped.Append(c);
                    continue;
                }

                int before = Neighbour(text, i, -1);
                bool joinsBefore = before >= 0 && Forms.TryGetValue(text[before], out var b) && b.Length == 4;
                int after = Neighbour(text, i, 1);

                if (c == 'ل' && after >= 0 && LamAlef.TryGetValue(text[after], out var ligature))
                {
                    shaped.Append(ligature[joinsBefore ? 1 : 0]);
                    for (int k = i + 1; k < after; k++) shaped.Append(text[k]); // its marks
                    i = after;
                    continue;
                }

                bool joinsAfter = forms.Length == 4 && after >= 0 && Forms.ContainsKey(text[after]);
                int form = joinsBefore ? (joinsAfter ? 3 : 1) : (joinsAfter ? 2 : 0);
                shaped.Append(forms[form]);
            }
            return shaped.ToString();
        }

        // The nearest character before (step -1) or after (step 1) that isn't a vowel mark.
        static int Neighbour(string text, int i, int step)
        {
            for (i += step; i >= 0 && i < text.Length; i += step)
                if (!IsMark(text[i])) return i;
            return -1;
        }

        // One shaped line in drawing order: the last letter first, each letter keeping its marks.
        // A mark goes just before its letter: the font draws marks to the right of where they
        // stand, which lands them on the letter after them.
        public static string Visual(string line)
        {
            var clusters = new List<string>(line.Length);
            foreach (char c in line)
            {
                if (IsMark(c) && clusters.Count > 0) clusters[^1] = c + clusters[^1];
                else clusters.Add(Mirrors.TryGetValue(c, out char m) ? m.ToString() : c.ToString());
            }
            clusters.Reverse();
            return string.Concat(clusters);
        }
    }
}
