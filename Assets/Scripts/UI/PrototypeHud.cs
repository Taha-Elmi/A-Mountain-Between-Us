using UnityEngine;

namespace Farhad
{
    // Throwaway IMGUI overlay for the dig prototype: stamina, madness, the name being typed,
    // the dream screen, and the end messages. Replace with a real UI once the HUD has art direction.
    public class PrototypeHud : MonoBehaviour
    {
        public FarhadController farhad;
        public Longing longing;
        public MountainGrid mountain;
        public GoalZone goal;

        static readonly Color MadnessTint = new(0.75f, 0.25f, 0.45f);

        GUIStyle small, large, nameStyle;

        void OnGUI()
        {
            // Lay out in 1080p units and scale to the actual screen.
            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            var screen = new Rect(0f, 0f, width, 1080f);

            small ??= new GUIStyle(GUI.skin.label) { fontSize = 22, normal = { textColor = Color.white } };
            large ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 44, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white },
            };
            nameStyle ??= new GUIStyle(large) { fontSize = 56, normal = { textColor = new Color(1f, 0.85f, 0.6f) } };

            if (longing.IsDreaming)
            {
                DrawRect(screen, new Color(0.08f, 0.04f, 0.1f, 0.95f));
                GUI.Label(new Rect(0f, 440f, width, 80f), "Farhad is dreaming of Shirin...", large);
                GUI.Label(new Rect(0f, 520f, width, 40f), "(the dream is not designed yet)", Centered(small));
                return;
            }

            // Madness seeps into the edges of the world.
            float tint = Mathf.InverseLerp(0.3f, 1f, longing.Madness01) * 0.3f;
            if (tint > 0f) DrawRect(screen, new Color(MadnessTint.r, MadnessTint.g, MadnessTint.b, tint));

            // Dark panels keep the white text readable over the white rock.
            var panel = new Color(0f, 0f, 0f, 0.55f);
            DrawRect(new Rect(24f, 26f, farhad.OutOfBreath ? 900f : 392f, 130f), panel);
            DrawRect(new Rect(24f, 1080f - 132f, width - 48f, 114f), panel);
            if (longing.TypedLetters > 0 || farhad.IsDead || goal.Reached) DrawRect(new Rect(0f, 210f, width, 220f), panel);

            var bar = new Rect(40f, 40f, 360f, 22f);
            DrawBar(bar, farhad.Stamina / farhad.MaxStamina,
                farhad.OutOfBreath ? new Color(0.85f, 0.3f, 0.25f) : new Color(0.95f, 0.78f, 0.45f));
            GUI.Label(new Rect(40f, 66f, 900f, 30f), farhad.OutOfBreath
                ? "Farhad has no strength left. Stand still and type SHIRIN to think of her."
                : "Stamina", small);

            var madnessBar = new Rect(40f, 104f, 360f, 14f);
            DrawBar(madnessBar, longing.Madness01, MadnessTint);
            if (longing.Floor > 0f) // the madness he can never come back below
                DrawRect(new Rect(madnessBar.x, madnessBar.y, madnessBar.width * longing.Floor / longing.madnessLimit, madnessBar.height),
                    new Color(0.3f, 0.08f, 0.18f));
            GUI.Label(new Rect(40f, 120f, 600f, 30f), "Sowda (madness)", small);

            if (longing.TypedLetters > 0)
                GUI.Label(new Rect(0f, 220f, width, 80f), TypedName(), nameStyle);

            GUI.Label(new Rect(40f, 1080f - 124f, width - 80f, 30f),
                "A / D  move     Space  jump     Left mouse  strike     Right mouse  knock and listen for hollow rock", small);
            GUI.Label(new Rect(40f, 1080f - 90f, width - 80f, 30f),
                "Stand still and type  S H I R I N  to think of her: it gives strength, and feeds madness", small);
            GUI.Label(new Rect(40f, 1080f - 56f, width - 80f, 30f),
                mountain.DebugReveal
                    ? $"F1  hide hollow rock (debug: ON)     strain {farhad.Strain:0.0} / {farhad.strainLimit:0}" +
                      $"     madness {longing.Madness:0} (floor {longing.Floor:0})     false echoes {mountain.FalseEchoChance:P0}"
                    : "F1  reveal hollow rock (debug)", small);

            if (farhad.IsDead)
            {
                GUI.Label(new Rect(0f, 300f, width, 80f), "The mountain fell on Farhad.", large);
                GUI.Label(new Rect(0f, 380f, width, 40f), "Press R to try again", Centered(small));
            }
            else if (goal.Reached)
                GUI.Label(new Rect(0f, 300f, width, 80f), "You reached the other side of the mountain.", large);
        }

        // "S H I _ _ _"
        string TypedName()
        {
            var name = longing.belovedName;
            var chars = new string[name.Length];
            for (int i = 0; i < name.Length; i++) chars[i] = i < longing.TypedLetters ? name[i].ToString() : "_";
            return string.Join(" ", chars);
        }

        static GUIStyle Centered(GUIStyle style) => new(style) { alignment = TextAnchor.MiddleCenter };

        static void DrawBar(Rect rect, float fill, Color color)
        {
            DrawRect(rect, new Color(0f, 0f, 0f, 0.5f));
            DrawRect(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), color);
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
