using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Farhad
{
    // The controls and the mountain's two rules, written in the sky just over Farhad's head where
    // he starts, so the opening view shows them and they stay behind as he walks to the mountain.
    // Lives on a world-space canvas measured in 1080p pixels (128 to a tile), whose pivot is the
    // sign's bottom-left corner. In Persian the keys sit on the right and the words run from the right.
    [RequireComponent(typeof(Canvas))]
    public class TutorialSign : MonoBehaviour
    {
        public ShirinCall call;
        [Tooltip("Farhad at the start; the sign stands centred over his head.")]
        public Transform farhad;
        [Tooltip("World units from Farhad's centre up to the sign's bottom edge: clear of his head.")]
        public float headroom = 1.2f;
        [Tooltip("The sign's left edge never goes past this world x, the edge of the level.")]
        public float minX = 0.25f;
        public Font font;
        [Tooltip("A key cap, sliced so it can stretch into the space bar.")]
        public Sprite keySprite;
        public Sprite mouseSprite;
        [Tooltip("The mouse with its wheel lit, scrolling down.")]
        public Sprite scrollSprite;

        public float width = 900f;
        public float padding = 24f;
        public float rowHeight = 42f;
        public float iconColumn = 136f;
        public float columnGap = 32f;
        public int labelSize = 28;
        public int ruleSize = 24;
        public Color panelColor = new(0f, 0f, 0f, 0.6f);
        public Color textColor = Color.white;
        public Color keyTextColor = new(0.1f, 0.09f, 0.09f);

        void Awake()
        {
            // Placed once, where he starts; it does not follow him.
            if (farhad == null) return;
            var start = farhad.position;
            transform.position = new Vector3(Mathf.Max(minX, start.x - width * 0.5f * transform.lossyScale.x),
                start.y + headroom, transform.position.z);
        }

        void OnEnable()
        {
            Build();
            Language.Changed += Build;
        }

        void OnDisable() => Language.Changed -= Build;

        void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);

            bool rtl = Language.RightToLeft;
            var panel = Child<Image>("Panel", transform);
            panel.color = panelColor;
            panel.raycastTarget = false;

            string callKey = call != null ? call.callKey.ToString() : "Q";
            // A row shows either a key or a picture of the mouse.
            var rows = new (string key, Sprite mouse, string english, string persian)[]
            {
                ("D", null, "Move right", "حرکت به راست"),
                ("A", null, "Move left", "حرکت به چپ"),
                ("Space", null, "Jump", "پرش"),
                (null, mouseSprite, "Strike", "ضربه زدن"),
                (callKey, null, "Call Shirin", "فریاد نام شیرین"),
                (null, scrollSprite, "Zoom out", "کوچک‌نمایی"),
            };

            // Two columns of controls, the first on the left (in Persian, on the right).
            float y = -padding; // from the top, growing down
            int perColumn = (rows.Length + 1) / 2;
            float columnWidth = (width - 2f * padding - columnGap) * 0.5f;
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                int column = i / perColumn;
                float columnLeft = padding + (rtl ? 1 - column : column) * (columnWidth + columnGap);
                float middle = y - (i % perColumn + 0.5f) * rowHeight;
                float iconX = rtl ? columnLeft + columnWidth - iconColumn * 0.5f : columnLeft + iconColumn * 0.5f;
                if (row.key == null)
                {
                    var mouse = Child<Image>("Mouse", panel.transform);
                    mouse.sprite = row.mouse;
                    mouse.raycastTarget = false;
                    Place(mouse.rectTransform, new Vector2(iconX, middle), new Vector2(30f, 40f));
                }
                else
                {
                    var key = Child<Image>("Key " + row.key, panel.transform);
                    key.sprite = keySprite;
                    key.type = Image.Type.Sliced;
                    key.pixelsPerUnitMultiplier = 2f;
                    key.raycastTarget = false;
                    Place(key.rectTransform, new Vector2(iconX, middle), new Vector2(row.key.Length > 1 ? 120f : 42f, 42f));
                    var letter = MakeText(key.transform, row.key, row.key.Length > 1 ? 20 : 26, TextAnchor.MiddleCenter, keyTextColor);
                    Place(letter.rectTransform, new Vector2(key.rectTransform.sizeDelta.x * 0.5f, -19f), key.rectTransform.sizeDelta);
                }

                var label = MakeText(panel.transform, Language.Words(row.english, row.persian), labelSize,
                    rtl ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, textColor);
                float labelWidth = columnWidth - iconColumn - 16f;
                float labelX = rtl ? columnLeft + labelWidth * 0.5f : columnLeft + columnWidth - labelWidth * 0.5f;
                Place(label.rectTransform, new Vector2(labelX, middle), new Vector2(labelWidth, rowHeight));
            }
            y -= perColumn * rowHeight;

            y -= 14f;
            var rules = MakeText(panel.transform, "", ruleSize, rtl ? TextAnchor.UpperRight : TextAnchor.UpperLeft, textColor);
            var generator = new TextGenerator();
            var settings = rules.GetGenerationSettings(Vector2.zero);
            float Measure(string s) => generator.GetPreferredWidth(s, settings) / rules.pixelsPerUnit;
            string text = Language.Current == Lang.Persian
                ? "سنگ‌های توخالی قابل تخریب هستند، اما با ضربه زدن به سنگ‌های محکم، کوه ریزش می‌کند.\n" +
                  "فریاد نام شیرین برای ثانیه‌هایی بینش تشخیص سنگ‌های توخالی از سنگ‌های سفت را به فرهاد می‌دهد."
                : "Hollow rock can be broken, but strike solid rock and the mountain caves in.\n" +
                  "Calling Shirin's name gives Farhad, for a few seconds, the insight to tell hollow rock from solid.";
            float textWidth = width - 2f * padding;
            var lines = new List<string>();
            foreach (string rule in text.Split('\n'))
                lines.AddRange(ComicLines.Wrap(rule, rtl, textWidth, Measure, out _));
            rules.text = string.Join("\n", lines);
            float rulesHeight = generator.GetPreferredHeight(rules.text, settings) / rules.pixelsPerUnit;
            Place(rules.rectTransform, new Vector2(width * 0.5f, y - rulesHeight * 0.5f), new Vector2(textWidth, rulesHeight));
            y -= rulesHeight + padding;

            // Now the height is known; the sign stands on its bottom edge.
            float height = -y;
            var root = (RectTransform)transform;
            root.pivot = Vector2.zero;
            root.sizeDelta = new Vector2(width, height);
            Place(panel.rectTransform, new Vector2(width * 0.5f, -height * 0.5f), root.sizeDelta);
        }

        Text MakeText(Transform parent, string words, int size, TextAnchor alignment, Color color)
        {
            var text = Child<Text>("Text", parent);
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; // Persian lines are broken here, not by Text
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = words;
            return text;
        }

        static T Child<T>(string name, Transform parent) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            go.transform.SetParent(parent, false);
            return go.GetComponent<T>();
        }

        // Positions are of the centre, from the parent's top-left corner (x right, y up).
        static void Place(RectTransform rect, Vector2 centre, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = centre;
        }
    }
}
