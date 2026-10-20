using UnityEngine;

namespace Farhad
{
    // Throwaway IMGUI overlay for the dig prototype: how many times Farhad can still call Shirin
    // (one of her at the top of the screen for each call), and the messages, in the game's language
    // (in Persian the calls are spent from the left and the words run from the right). The controls
    // are on the TutorialSign in the sky. Replace with a real UI once the HUD has art direction.
    public class PrototypeHud : MonoBehaviour
    {
        public FarhadController farhad;
        public ShirinCall call;
        public MountainGrid mountain;
        public GoalZone goal;
        [Tooltip("For the Persian words; the English ones use IMGUI's own font.")]
        public Font persianFont;
        [Tooltip("Shirin's shape, white on clear: drawn as a black silhouette once for every call he has left.")]
        public Texture2D shirinIcon;

        GUIStyle small, large, cry;
        Lang? styledFor;

        void Awake()
        {
            if (call == null)
                Debug.LogError("The HUD has no ShirinCall. The scene is out of date: rebuild it with " +
                               "Farhad > Build Dig Prototype Scene.", this);
        }

        void OnGUI()
        {
            // The pause menu covers the level, so the HUD steps aside for it.
            if (call == null || PauseMenu.Paused) return;

            // Lay out in 1080p units and scale to the actual screen.
            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;

            bool rtl = Language.RightToLeft;
            if (styledFor != Language.Current)
            {
                styledFor = Language.Current;
                // Persian lines come already in drawing order, so IMGUI mustn't break them; and
                // the Persian font sits lower than IMGUI's, so its letters may hang below the rect.
                var font = rtl ? persianFont : null;
                var clipping = rtl ? TextClipping.Overflow : TextClipping.Clip;
                small = new GUIStyle(GUI.skin.label)
                {
                    font = font, fontSize = 22, wordWrap = !rtl, clipping = clipping, normal = { textColor = Color.white },
                    alignment = rtl ? TextAnchor.UpperRight : TextAnchor.UpperLeft,
                };
                large = new GUIStyle(GUI.skin.label)
                {
                    font = font, fontSize = 44, wordWrap = !rtl, clipping = clipping, alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white },
                };
                cry = new GUIStyle(large) { fontSize = 56, fontStyle = rtl ? FontStyle.Bold : FontStyle.Italic };
            }
            string Words(string english, string persian) => Language.Words(english, persian);

            // Dark panels keep the white text readable over the white rock.
            var panel = new Color(0f, 0f, 0f, 0.55f);
            const float iconHeight = 64f, iconGap = 14f;
            float iconWidth = shirinIcon != null ? iconHeight * shirinIcon.width / shirinIcon.height : iconHeight;
            // Top centre, clear of the sign in the sky's top-left corner.
            float iconsWidth = call.calls * iconWidth + (call.calls - 1) * iconGap;
            float iconsLeft = (width - iconsWidth) * 0.5f;
            // Her black silhouettes stand on a white panel, so they show over the dark sky and rock.
            DrawRect(new Rect(iconsLeft - 16f, 24f, iconsWidth + 32f, iconHeight + 28f), new Color(1f, 1f, 1f, 0.9f));
            bool spentMessage = Time.time - call.LastSpentCallTime < 2.5f && !farhad.IsDead;
            if (farhad.IsDead || spentMessage) DrawRect(new Rect(0f, 250f, width, 140f), panel);

            // One Shirin for every call he has left; the spent ones a faint shadow of her.
            for (int i = 0; i < call.calls; i++)
            {
                int slot = rtl ? call.calls - 1 - i : i;
                var icon = new Rect(iconsLeft + slot * (iconWidth + iconGap), 38f, iconWidth, iconHeight);
                var old = GUI.color;
                GUI.color = i < call.CallsLeft ? Color.black : new Color(0f, 0f, 0f, 0.15f);
                if (shirinIcon != null) GUI.DrawTexture(icon, shirinIcon, ScaleMode.ScaleToFit);
                else DrawRect(icon, GUI.color);
                GUI.color = old;
            }

            // His cry, white on a black panel, fading as the rock falls silent again.
            float sinceCall = Time.time - call.LastCallTime;
            if (sinceCall < 2f && !farhad.IsDead)
            {
                float alpha = 1f - sinceCall / 2f;
                string words = Words("Shirin!", "شیرین!");
                float wordsWidth = cry.CalcSize(new GUIContent(words)).x;
                DrawRect(new Rect((width - wordsWidth) * 0.5f - 40f, 160f, wordsWidth + 80f, 80f), new Color(0f, 0f, 0f, alpha));
                cry.normal.textColor = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(0f, 160f, width, 80f), words, cry);
            }

            // F1 (the debug view of the hollow rock) still works; it says so only while it's on.
            if (mountain.DebugReveal)
            {
                DrawRect(new Rect(24f, 1080f - 70f, width - 48f, 50f), panel);
                GUI.Label(new Rect(40f, 1080f - 60f, width - 80f, 30f), Words(
                    $"F1  hide hollow rock (debug: ON)     strain {farhad.Strain:0.0} / {farhad.strainLimit:0}",
                    $"F1  پنهان کردن سنگ توخالی (دیباگ: روشن)     فشار {farhad.Strain:0.0} / {farhad.strainLimit:0}"), small);
            }

            if (farhad.IsDead)
                GUI.Label(new Rect(0f, 280f, width, 80f), Words(
                    "The mountain caved in... Press R to try this path again", "کوه ریزش کرد... برای امتحان دوباره این مسیر R را بزنید"), large);
            else if (spentMessage)
                GUI.Label(new Rect(0f, 280f, width, 80f), Words(
                    "Farhad has no strength left to cry out.", "فرهاد دیگر توان فریاد زدن ندارد."), large);

            if (goal.FadeOut > 0f) DrawRect(new Rect(0f, 0f, width, 1080f), new Color(0f, 0f, 0f, goal.FadeOut));
        }

        static void DrawRect(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
