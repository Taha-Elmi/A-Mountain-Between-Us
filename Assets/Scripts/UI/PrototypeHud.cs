using UnityEngine;

namespace Farhad
{
    // Throwaway IMGUI overlay for the dig prototype: how many times Farhad can still call Shirin,
    // the controls, and the end messages. Replace with a real UI once the HUD has art direction.
    public class PrototypeHud : MonoBehaviour
    {
        public FarhadController farhad;
        public ShirinCall call;
        public MountainGrid mountain;
        public GoalZone goal;

        static readonly Color Gold = new(1f, 0.82f, 0.5f);

        GUIStyle small, large, cry;

        void Awake()
        {
            if (call == null)
                Debug.LogError("The HUD has no ShirinCall. The scene is out of date: rebuild it with " +
                               "Farhad > Build Dig Prototype Scene.", this);
        }

        void OnGUI()
        {
            if (call == null) return;

            // Lay out in 1080p units and scale to the actual screen.
            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;

            small ??= new GUIStyle(GUI.skin.label) { fontSize = 22, normal = { textColor = Color.white } };
            large ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 44, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white },
            };
            cry ??= new GUIStyle(large) { fontSize = 56, fontStyle = FontStyle.Italic };

            // Dark panels keep the white text readable over the white rock.
            var panel = new Color(0f, 0f, 0f, 0.55f);
            DrawRect(new Rect(24f, 26f, 420f, 84f), panel);
            DrawRect(new Rect(24f, 1080f - 132f, width - 48f, 114f), panel);
            if (farhad.IsDead || goal.Reached) DrawRect(new Rect(0f, 210f, width, 220f), panel);

            // One gold light for every call he has left.
            GUI.Label(new Rect(40f, 34f, 400f, 30f), call.CallsLeft > 0
                ? $"Call Shirin  [{call.callKey}]"
                : "He has no voice left to call her", small);
            for (int i = 0; i < call.calls; i++)
            {
                var pip = new Rect(40f + i * 34f, 72f, 24f, 24f);
                DrawRect(pip, new Color(0f, 0f, 0f, 0.5f));
                if (i < call.CallsLeft) DrawRect(new Rect(pip.x + 3f, pip.y + 3f, 18f, 18f), Gold);
            }

            // His cry, fading as the rock falls silent again.
            float sinceCall = Time.time - call.LastCallTime;
            if (sinceCall < 2f && !farhad.IsDead)
            {
                var color = Gold;
                color.a = 1f - sinceCall / 2f;
                cry.normal.textColor = color;
                GUI.Label(new Rect(0f, 160f, width, 80f), "Shirin!", cry);
            }

            GUI.Label(new Rect(40f, 1080f - 124f, width - 80f, 30f),
                $"A / D  move     Space  jump     Left mouse  strike     {call.callKey}  call Shirin", small);
            GUI.Label(new Rect(40f, 1080f - 90f, width - 80f, 30f),
                "Hollow rock breaks. Strike solid rock and the mountain falls on you. Calling her shows the hollow rock around you for a moment.", small);
            GUI.Label(new Rect(40f, 1080f - 56f, width - 80f, 30f),
                mountain.DebugReveal
                    ? $"F1  hide hollow rock (debug: ON)     strain {farhad.Strain:0.0} / {farhad.strainLimit:0}"
                    : "F1  reveal hollow rock (debug)", small);

            if (farhad.IsDead)
            {
                GUI.Label(new Rect(0f, 300f, width, 80f), "The mountain fell on Farhad.", large);
                GUI.Label(new Rect(0f, 380f, width, 40f), "Press R to try again", Centered(small));
            }
            else if (goal.Reached)
                GUI.Label(new Rect(0f, 300f, width, 80f), "You reached the other side of the mountain.", large);

            if (goal.FadeOut > 0f) DrawRect(new Rect(0f, 0f, width, 1080f), new Color(0f, 0f, 0f, goal.FadeOut));
        }

        static GUIStyle Centered(GUIStyle style) => new(style) { alignment = TextAnchor.MiddleCenter };

        static void DrawRect(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
