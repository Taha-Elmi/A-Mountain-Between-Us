using System.Linq;
using UnityEditor;

namespace Farhad.EditorTools
{
    // Keeps the build's scene list in play order (the game starts with the first), with any
    // other scenes after them.
    public static class BuildSceneOrder
    {
        static readonly string[] PlayOrder =
        {
            "Assets/Scenes/Menu.unity",
            "Assets/Scenes/ComicIntro.unity",
            "Assets/Scenes/DigPrototype.unity",
            "Assets/Scenes/ComicOutro.unity",
        };

        public static void Apply()
        {
            var game = PlayOrder
                .Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                .Select(path => new EditorBuildSettingsScene(path, true));
            var others = EditorBuildSettings.scenes.Where(s => !PlayOrder.Contains(s.path));
            EditorBuildSettings.scenes = game.Concat(others).ToArray();
        }
    }
}
