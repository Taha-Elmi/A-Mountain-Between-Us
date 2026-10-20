using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Farhad
{
    // The menu's records: the player's best runs through the mountain (see Records) as a table of
    // rank, rockfalls and time, in the game's language, filled in each time it is shown. In Persian
    // the columns run from the right and the numbers are written in Persian digits. Before any run
    // has reached the far side, it says the game hasn't been played yet.
    public class RecordsBoard : MonoBehaviour
    {
        public Text title;
        [Tooltip("Three columns, one row per record under a heading, laid out left to right as in English.")]
        public Text rankColumn;
        public Text rockfallsColumn;
        public Text timeColumn;
        public Text empty;
        public Font englishFont;
        public Font persianFont;

        void OnEnable()
        {
            Show();
            Language.Changed += Show;
        }

        void OnDisable() => Language.Changed -= Show;

        public void Show()
        {
            bool rtl = Language.RightToLeft;
            var font = rtl ? persianFont : englishFont;
            foreach (var text in new[] { title, rankColumn, rockfallsColumn, timeColumn, empty }) text.font = font;
            // The Persian font's lines stand taller, so they're packed closer to fit a full table.
            foreach (var column in new[] { rankColumn, rockfallsColumn, timeColumn }) column.lineSpacing = rtl ? 0.85f : 1.15f;
            title.text = Language.Words("Records", "رکوردها");

            var records = Records.Best();
            empty.gameObject.SetActive(records.Count == 0);
            empty.text = Language.Words("You have not played the game yet.", "هنوز بازی نکرده‌اید.");

            var ranks = new List<string> { Language.Words("Rank", "رتبه") };
            var rockfalls = new List<string> { Language.Words("Rockfalls", "ریزش کوه") };
            var times = new List<string> { Language.Words("Time", "زمان") };
            for (int i = 0; i < records.Count; i++)
            {
                ranks.Add(Digits((i + 1).ToString()));
                rockfalls.Add(Digits(records[i].rockfalls.ToString()));
                times.Add(Digits(Records.Duration(records[i].seconds)));
            }
            bool any = records.Count > 0;
            Fill(rankColumn, ranks, any);
            Fill(rockfallsColumn, rockfalls, any);
            Fill(timeColumn, times, any);

            // Rank stays on the reading side: left in English, right in Persian.
            var rankRect = rankColumn.rectTransform;
            var timeRect = timeColumn.rectTransform;
            float rankMin = Mathf.Min(rankRect.anchorMin.x, timeRect.anchorMin.x);
            float timeMin = Mathf.Max(rankRect.anchorMin.x, timeRect.anchorMin.x);
            SetColumn(rankRect, rtl ? timeMin : rankMin);
            SetColumn(timeRect, rtl ? rankMin : timeMin);
        }

        static void Fill(Text column, List<string> lines, bool show)
        {
            column.gameObject.SetActive(show);
            column.text = string.Join("\n", lines);
        }

        // Moves a column sideways, keeping its width.
        static void SetColumn(RectTransform column, float minX)
        {
            float width = column.anchorMax.x - column.anchorMin.x;
            column.anchorMin = new Vector2(minX, column.anchorMin.y);
            column.anchorMax = new Vector2(minX + width, column.anchorMax.y);
        }

        // Persian digits in Persian; the digits as they are in English.
        static string Digits(string text)
        {
            if (!Language.RightToLeft) return text;
            var persian = new StringBuilder(text.Length);
            foreach (char c in text) persian.Append(c >= '0' && c <= '9' ? (char)('۰' + c - '0') : c);
            return persian.ToString();
        }
    }
}
