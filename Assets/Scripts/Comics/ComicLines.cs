using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Farhad
{
    // The words on a comic page: one box per line, drawn in ink on paper like the panels, sized
    // to its text and placed where the script says. Captions are square boxes; speech bubbles
    // are rounded, with a tail; oval bubbles are ellipses. Plain lines are the words alone, in
    // the paper's colour, for black pages and dark pictures. Persian is joined and laid out right
    // to left here, since Unity's Text can't do either. The boxes start hidden; the player fades
    // them in one by one.
    [RequireComponent(typeof(RectTransform))]
    public class ComicLines : MonoBehaviour
    {
        public Font persianFont;
        public Font englishFont;
        [Tooltip("Text size on a page shown 1080 tall; scales with the page.")]
        public int persianSize = 30;
        public int englishSize = 28;
        public float lineSpacing = 1f;
        public Color ink = Color.black;
        public Color paper = Color.white;
        [Tooltip("Thickness of the boxes' outline, on a page shown 1080 tall.")]
        public float outline = 3f;

        // Everything below is measured on a page 1080 tall.
        static readonly Vector2 CaptionPadding = new(24f, 14f);
        static readonly Vector2 SpeechPadding = new(36f, 22f);
        static readonly Vector2 OvalPadding = new(24f, 36f); // beyond the ellipse through the text's corners
        static readonly Vector2 PlainShadow = new(2f, -2f);
        const float SpeechCorner = 44f;
        const float CaptionCorner = 2f;
        const float TailWidth = 20f; // half the tail's width where it leaves the bubble

        readonly List<CanvasGroup> boxes = new();
        readonly List<UnityEngine.Object> made = new(); // textures and sprites to free

        public int Count => boxes.Count;

        RectTransform Rect => (RectTransform)transform;

        // Covers the part of the parent where a page of this shape shows (as an Image with
        // preserveAspect draws it) and lays the lines out on it, all hidden.
        public void Show(IReadOnlyList<ComicLine> lines, Lang lang, float pageAspect)
        {
            Clear();
            Vector2 room = ((RectTransform)Rect.parent).rect.size;
            Vector2 page = room.x / room.y > pageAspect
                ? new Vector2(room.y * pageAspect, room.y)
                : new Vector2(room.x, room.x / pageAspect);
            Rect.anchorMin = Rect.anchorMax = Rect.pivot = new Vector2(0.5f, 0.5f);
            Rect.anchoredPosition = Vector2.zero;
            Rect.sizeDelta = page;

            if (lines == null) return;
            foreach (var line in lines)
                boxes.Add(Build(line, lang, page));
        }

        public void SetAlpha(int index, float alpha) => boxes[index].alpha = alpha;

        public void Clear()
        {
            foreach (var box in boxes)
                if (box != null) Free(box.gameObject);
            foreach (var thing in made) Free(thing);
            boxes.Clear();
            made.Clear();
        }

        void OnDestroy() => Clear();

        static void Free(UnityEngine.Object thing)
        {
            if (Application.isPlaying) Destroy(thing);
            else DestroyImmediate(thing);
        }

        CanvasGroup Build(ComicLine line, Lang lang, Vector2 page)
        {
            float k = page.y / 1080f;
            bool rtl = lang == Lang.Persian;
            bool speech = line.style == LineStyle.Speech;
            bool oval = line.style == LineStyle.Oval;
            bool plain = line.style == LineStyle.Plain;

            var box = new GameObject(line.speaker, typeof(RectTransform), typeof(CanvasGroup));
            box.transform.SetParent(transform, false);
            var group = box.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = false;

            Image shape = null;
            if (!plain)
            {
                shape = new GameObject("Shape", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                shape.transform.SetParent(box.transform, false);
                shape.raycastTarget = false;
            }

            var text = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(box.transform, false);
            text.font = rtl ? persianFont : englishFont;
            text.fontSize = Mathf.Max(1, Mathf.RoundToInt((rtl ? persianSize : englishSize) * k));
            text.lineSpacing = lineSpacing;
            text.color = plain ? paper : ink;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; // lines are broken here, not by Text
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.alignment = line.style == LineStyle.Caption ? rtl ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            if (plain)
            {
                // A soft shadow keeps the words clear where a picture runs light behind them.
                var shadow = text.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(ink.r, ink.g, ink.b, 0.8f);
                shadow.effectDistance = PlainShadow * k;
            }

            Vector2 padding = (plain ? Vector2.zero : speech ? SpeechPadding : oval ? OvalPadding : CaptionPadding) * k;
            var generator = new TextGenerator();
            var settings = text.GetGenerationSettings(Vector2.zero);
            float unitsPerPixel = 1f / text.pixelsPerUnit;
            float Measure(string s) => generator.GetPreferredWidth(s, settings) * unitsPerPixel;

            float maxWidth = line.width / 100f * page.x - 2f * padding.x;
            if (oval) maxWidth /= Mathf.Sqrt(2f);
            maxWidth = Mathf.Max(maxWidth, 60f * k);
            var rows = Wrap(line.Text(lang) ?? "", rtl, maxWidth, Measure, out float textWidth);
            text.text = string.Join("\n", rows);
            var inner = new Vector2(textWidth, generator.GetPreferredHeight(text.text, settings) * unitsPerPixel);
            text.rectTransform.sizeDelta = inner;
            // An oval runs through the corners of a box its own shape around the text.
            Vector2 size = (oval ? inner * Mathf.Sqrt(2f) : inner) + 2f * padding;

            // The script places boxes in percent of the page from its top-left; here the page's
            // centre is the origin. Boxes stay on the page.
            Vector2 FromScript(Vector2 percent) => new((percent.x / 100f - 0.5f) * page.x, (0.5f - percent.y / 100f) * page.y);
            Vector2 centre = FromScript(line.at);
            Vector2 room = Vector2.Max((page - size) * 0.5f, Vector2.zero);
            centre = new Vector2(Mathf.Clamp(centre.x, -room.x, room.x), Mathf.Clamp(centre.y, -room.y, room.y));
            var boxRect = (RectTransform)box.transform;
            boxRect.sizeDelta = size;
            boxRect.anchoredPosition = centre;

            if (plain) return group;
            float scale = Mathf.Max(1f, GetComponentInParent<Canvas>().rootCanvas.scaleFactor);
            Vector2? tip = line.hasTail ? FromScript(line.tail) - centre : null;
            shape.sprite = DrawShape(size, oval, speech ? SpeechCorner * k : CaptionCorner * k, tip, TailWidth * k,
                outline * k, scale, out var bounds);
            shape.rectTransform.anchoredPosition = bounds.center;
            shape.rectTransform.sizeDelta = bounds.size;
            return group;
        }

        // Breaks the text into rows no wider than maxWidth, as evenly as it can (the narrowest
        // width that needs no more rows), and returns them in drawing order. A row break in the
        // text always starts a new row.
        static List<string> Wrap(string text, bool rtl, float maxWidth, Func<string, float> measure, out float width)
        {
            var paragraphs = new List<string[]>();
            foreach (string paragraph in text.Split('\n'))
                paragraphs.Add((rtl ? PersianText.Shape(paragraph) : paragraph).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            string Drawn(string row) => rtl ? PersianText.Visual(row) : row;

            List<string> Fill(float limit)
            {
                var rows = new List<string>();
                foreach (string[] words in paragraphs)
                {
                    string row = "";
                    foreach (string word in words)
                    {
                        string longer = row.Length == 0 ? word : row + " " + word;
                        if (row.Length > 0 && measure(Drawn(longer)) > limit)
                        {
                            rows.Add(row);
                            row = word;
                        }
                        else row = longer;
                    }
                    if (row.Length > 0) rows.Add(row);
                }
                return rows;
            }

            var best = Fill(maxWidth);
            float low = 0f, high = maxWidth;
            for (int i = 0; i < 12 && best.Count > paragraphs.Count; i++)
            {
                float mid = (low + high) * 0.5f;
                var rows = Fill(mid);
                if (rows.Count <= best.Count)
                {
                    best = rows;
                    high = mid;
                }
                else low = mid;
            }

            width = 0f;
            for (int i = 0; i < best.Count; i++)
            {
                best[i] = Drawn(best[i]);
                width = Mathf.Max(width, measure(best[i]));
            }
            return best;
        }

        // Paints the box (centred on the origin; an ellipse filling it if oval), joined to a tail
        // running to tip if it's outside the box: white inside, an ink outline, soft edges. Drawn
        // at screen resolution.
        Sprite DrawShape(Vector2 size, bool oval, float corner, Vector2? tip, float tailWidth, float border, float scale, out Rect bounds)
        {
            Vector2 half = size * 0.5f;
            corner = Mathf.Min(corner, half.x, half.y);
            float Body(Vector2 p) => oval ? Ellipse(p, half) : RoundedBox(p, half, corner);

            bool tail = tip.HasValue && Body(tip.Value) > 0f;
            Vector2 tailTip = tip ?? Vector2.zero, sideA = default, sideB = default;
            if (tail)
            {
                // The tail starts well inside the box, at the point nearest the tip (an oval's:
                // toward it), so the box's outline is hidden where they meet.
                tailWidth = Mathf.Min(tailWidth, half.x * 0.4f, half.y * 0.6f);
                Vector2 root;
                if (oval)
                {
                    Vector2 toward = new Vector2(tailTip.x / half.x, tailTip.y / half.y).normalized * 0.7f;
                    root = Vector2.Scale(toward, half);
                }
                else
                {
                    Vector2 inset = Vector2.Max(half - Vector2.one * (corner + tailWidth), Vector2.zero);
                    root = new Vector2(Mathf.Clamp(tailTip.x, -inset.x, inset.x), Mathf.Clamp(tailTip.y, -inset.y, inset.y));
                }
                Vector2 along = (tailTip - root).normalized;
                Vector2 across = new Vector2(-along.y, along.x) * tailWidth;
                sideA = root + across;
                sideB = root - across;
            }

            Vector2 margin = Vector2.one * (border + 2f / scale);
            Vector2 min = (tail ? Vector2.Min(-half, tailTip) : -half) - margin;
            Vector2 max = (tail ? Vector2.Max(half, tailTip) : half) + margin;
            bounds = new Rect(min, max - min);

            int w = Mathf.CeilToInt(bounds.width * scale), h = Mathf.CeilToInt(bounds.height * scale);
            var pixels = new Color32[w * h];
            float borderPixels = border * scale;
            Color32 inkColour = ink, paperColour = paper;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = min + new Vector2(x + 0.5f, y + 0.5f) / scale;
                float d = Body(p);
                if (tail) d = Mathf.Min(d, Triangle(p, sideA, sideB, tailTip));
                d *= scale;
                float alpha = Mathf.Clamp01(0.5f - d);
                float inkAmount = Mathf.Clamp01(0.5f - (Mathf.Abs(d + borderPixels * 0.5f) - borderPixels * 0.5f));
                var c = Color32.Lerp(paperColour, inkColour, inkAmount);
                c.a = (byte)(alpha * Mathf.Lerp(paperColour.a, inkColour.a, inkAmount));
                pixels[y * w + x] = c;
            }

            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "Comic Line Shape",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave;
            made.Add(texture);
            made.Add(sprite);
            return sprite;
        }

        // Signed distances: negative inside, positive outside.
        static float RoundedBox(Vector2 p, Vector2 half, float corner)
        {
            var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + Vector2.one * corner;
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - corner;
        }

        // Close to the true distance near the outline, which is all the drawing needs.
        static float Ellipse(Vector2 p, Vector2 radii)
        {
            float k0 = new Vector2(p.x / radii.x, p.y / radii.y).magnitude;
            float k1 = new Vector2(p.x / (radii.x * radii.x), p.y / (radii.y * radii.y)).magnitude;
            return k1 > 0f ? k0 * (k0 - 1f) / k1 : -Mathf.Min(radii.x, radii.y);
        }

        static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 e0 = b - a, e1 = c - b, e2 = a - c;
            Vector2 v0 = p - a, v1 = p - b, v2 = p - c;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            float d0 = pq0.sqrMagnitude, d1 = pq1.sqrMagnitude, d2 = pq2.sqrMagnitude;
            float s0 = s * (v0.x * e0.y - v0.y * e0.x), s1 = s * (v1.x * e1.y - v1.y * e1.x), s2 = s * (v2.x * e2.y - v2.y * e2.x);
            float distance = Mathf.Min(d0, Mathf.Min(d1, d2));
            float side = Mathf.Min(s0, Mathf.Min(s1, s2));
            return -Mathf.Sqrt(distance) * Mathf.Sign(side);
        }
    }
}
