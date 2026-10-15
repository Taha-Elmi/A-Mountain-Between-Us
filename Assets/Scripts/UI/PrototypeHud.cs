using UnityEngine;

namespace Farhad
{
    // Throwaway IMGUI overlay for the dig prototype: stamina, controls, and the end message.
    // Replace with a real UI once the art direction for the HUD exists.
    public class PrototypeHud : MonoBehaviour
    {
        public FarhadController farhad;
        public MountainGrid mountain;
        public GoalZone goal;

        GUIStyle small, large;

        void OnGUI()
        {
            // Lay out in 1080p units and scale to the actual screen.
            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;

            small ??= new GUIStyle(GUI.skin.label) { fontSize = 22, normal = { textColor = Color.white } };
            large ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 44, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white },
            };

            var bar = new Rect(40f, 40f, 360f, 22f);
            DrawRect(bar, new Color(0f, 0f, 0f, 0.5f));
            DrawRect(new Rect(bar.x, bar.y, bar.width * farhad.Stamina / farhad.MaxStamina, bar.height),
                farhad.OutOfBreath ? new Color(0.85f, 0.3f, 0.25f) : new Color(0.95f, 0.78f, 0.45f));
            GUI.Label(new Rect(40f, 66f, 600f, 30f), farhad.OutOfBreath ? "Farhad must catch his breath..." : "Stamina", small);

            GUI.Label(new Rect(40f, 1080f - 90f, width - 80f, 30f),
                "A / D  move     Space  jump     Left mouse  strike     Right mouse  knock and listen for hollow rock", small);
            GUI.Label(new Rect(40f, 1080f - 56f, width - 80f, 30f),
                mountain.DebugReveal ? "F1  hide hollow rock (debug: ON)" : "F1  reveal hollow rock (debug)", small);

            if (goal.Reached)
                GUI.Label(new Rect(0f, 300f, width, 80f), "You reached the other side of the mountain.", large);
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
