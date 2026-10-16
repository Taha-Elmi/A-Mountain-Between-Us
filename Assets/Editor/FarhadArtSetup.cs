using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Farhad.EditorTools
{
    // Imports Farhad's character frames (made by Tools/process_farhad_frames.ps1) and builds his
    // animation clips and Animator Controller from them.
    //
    // Frame spec: every animation is cropped to the same 1200x1670 px box of the artist's canvas
    // (x 880, y 0); Farhad is ~1475 px tall, imported at 820 px per unit so he stands ~1.8 units
    // (tiles) tall. The pivot sits between his feet on the body's center line (canvas x 1458,
    // y 1645), so he doesn't shift between animations and flipping him keeps him in place.
    public static class FarhadArtSetup
    {
        public const string ArtDir = "Assets/Art/Characters/Farhad";
        const string AnimationDir = "Assets/Animation/Farhad";
        const string ControllerPath = AnimationDir + "/Farhad.controller";
        public const string WalkingParameter = "Walking";
        public const string StrikeParameter = "Strike";

        const float PixelsPerUnit = 820f;
        static readonly Vector2 FeetPivot = new((1458f - 880f) / 1200f, (1670f - 1645f) / 1670f);
        const float WalkFramesPerSecond = 12f;

        // The swing: the pickaxe meets the rock on its last drawn frame, which is then held a moment.
        const float StrikeFramesPerSecond = 12f;
        const int StrikeImpactFrame = 6;
        const int StrikeHoldFrames = 2;
        public const float StrikeImpactTime = (StrikeImpactFrame - 1) / StrikeFramesPerSecond;

        // Seconds from the start of a swing to when the next can begin (set on FarhadController).
        public static float StrikeDuration { get; private set; }

        public static bool HasArt => AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/farhad_idle.png") != null;

        [MenuItem("Farhad/Rebuild Farhad Animations")]
        public static AnimatorController Build()
        {
            var idle = ImportSprite(ArtDir + "/farhad_idle.png");
            var walk = ImportFrames("Walk", "farhad_walk_");
            var strike = ImportFrames("Strike", "farhad_strike_");

            EnsureFolder(AnimationDir);
            var idleClip = MakeClip("Farhad_Idle", new[] { idle }, 1f, loop: true);
            var walkClip = MakeClip("Farhad_Walk", walk, WalkFramesPerSecond, loop: true);
            var strikeFrames = strike.Concat(Enumerable.Repeat(strike[^1], StrikeHoldFrames)).ToArray();
            var strikeClip = MakeClip("Farhad_Strike", strikeFrames, StrikeFramesPerSecond, loop: false);
            StrikeDuration = strikeFrames.Length / StrikeFramesPerSecond;

            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter(WalkingParameter, AnimatorControllerParameterType.Bool);
            controller.AddParameter(StrikeParameter, AnimatorControllerParameterType.Trigger);
            var states = controller.layers[0].stateMachine;
            var idleState = states.AddState("Idle");
            idleState.motion = idleClip;
            var walkState = states.AddState("Walk");
            walkState.motion = walkClip;
            var strikeState = states.AddState("Strike");
            strikeState.motion = strikeClip;
            states.defaultState = idleState;
            AddInstantTransition(idleState, walkState, AnimatorConditionMode.If);
            AddInstantTransition(walkState, idleState, AnimatorConditionMode.IfNot);

            // A swing can start from any state, and always plays to its end before he stands again.
            var toStrike = states.AddAnyStateTransition(strikeState);
            toStrike.hasExitTime = false;
            toStrike.duration = 0f;
            toStrike.AddCondition(AnimatorConditionMode.If, 0f, StrikeParameter);
            var fromStrike = strikeState.AddTransition(idleState);
            fromStrike.hasExitTime = true;
            fromStrike.exitTime = 1f;
            fromStrike.duration = 0f;

            AssetDatabase.SaveAssets();
            Debug.Log($"Farhad animations built: idle, {walk.Length} walk frames, {strike.Length} strike frames " +
                      $"(impact at {StrikeImpactTime:0.00}s of {StrikeDuration:0.00}s)");
            return controller;
        }

        static Sprite[] ImportFrames(string folder, string prefix) =>
            AssetDatabase.FindAssets(prefix + " t:Texture2D", new[] { $"{ArtDir}/{folder}" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path)
                .Select(ImportSprite)
                .ToArray();

        public static Sprite IdleSprite => AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/farhad_idle.png");

        static void AddInstantTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.AddCondition(mode, 0f, WalkingParameter);
        }

        static Sprite ImportSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            // He is drawn ~6x larger than he appears on screen: mipmaps keep the fine ink and
            // halftone from shimmering when shrunk.
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = FeetPivot;
            settings.spriteMeshType = SpriteMeshType.Tight;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static AnimationClip MakeClip(string name, Sprite[] frames, float framesPerSecond, bool loop)
        {
            string path = $"{AnimationDir}/{name}.anim";
            AssetDatabase.DeleteAsset(path);

            var clip = new AnimationClip { frameRate = framesPerSecond };
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            // Unity gives the last key a full frame of time, so the loop is frames / fps long.
            var keys = frames
                .Select((sprite, i) => new ObjectReferenceKeyframe { time = i / framesPerSecond, value = sprite })
                .ToArray();
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
