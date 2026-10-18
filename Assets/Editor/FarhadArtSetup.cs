using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Farhad.EditorTools
{
    // Imports Farhad's character frames (made by Tools/process_farhad_frames.ps1) and builds his
    // animation clips and Animator Controller from them.
    //
    // Frame spec: every animation is cropped to the same 1600x1720 px box of the artist's canvas
    // (x 830, y 0), leaving room around him for his glow; Farhad is ~1475 px tall, imported at
    // 820 px per unit so he stands ~1.8 units (tiles) tall. The pivot sits between his feet on the
    // body's center line (canvas x 1458, y 1645), so he doesn't shift between animations and
    // flipping him keeps him in place.
    public static class FarhadArtSetup
    {
        public const string ArtDir = "Assets/Art/Characters/Farhad";
        const string AnimationDir = "Assets/Animation/Farhad";
        const string ControllerPath = AnimationDir + "/Farhad.controller";
        public const string WalkingParameter = "Walking";

        const float PixelsPerUnit = 820f;
        static readonly Vector2 FeetPivot = new((1458f - 830f) / 1600f, (1720f - 1645f) / 1720f);
        const float WalkFramesPerSecond = 12f;

        // A swing raises the pickaxe and brings it down only as far as the rock it is aimed at: the
        // drawn frames run up to that direction's impact frame, which is held a moment, then play
        // back (twice as fast) to where he started.
        const float StrikeFramesPerSecond = 12f;
        const int StrikeHoldFrames = 1;
        static readonly (StrikeDirection direction, int impactFrame)[] Strikes =
        {
            (StrikeDirection.Up, 5),   // pickaxe raised over his head
            (StrikeDirection.Side, 8), // swung out level in front of him
            (StrikeDirection.Down, 9), // bent over, swung down by his feet
        };

        // When the rock is hit, and when the next swing can begin (set on FarhadController).
        public static StrikeTiming TimingOf(StrikeDirection direction)
        {
            int impactFrame = Strikes.First(strike => strike.direction == direction).impactFrame;
            return new StrikeTiming
            {
                impactTime = (impactFrame - 1) / StrikeFramesPerSecond,
                duration = (impactFrame + StrikeHoldFrames) / StrikeFramesPerSecond
                           + (impactFrame - 1) / (StrikeFramesPerSecond * 2f),
            };
        }

        public static bool HasArt => AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/farhad_idle.png") != null;

        [MenuItem("Farhad/Rebuild Farhad Animations")]
        public static AnimatorController Build()
        {
            var idle = ImportSprite(ArtDir + "/farhad_idle.png");
            var walk = ImportFrames("Walk", "farhad_walk_");
            var strike = ImportFrames("Strike", "farhad_strike_");
            int framesNeeded = Strikes.Max(s => s.impactFrame);
            if (strike.Length < framesNeeded)
                throw new System.InvalidOperationException(
                    $"Farhad's strike needs {framesNeeded} frames; found {strike.Length} in {ArtDir}/Strike");

            EnsureFolder(AnimationDir);
            AssetDatabase.DeleteAsset(AnimationDir + "/Farhad_Strike.anim"); // the old one-way swing
            var idleClip = MakeClip("Farhad_Idle", new[] { idle }, 1f, loop: true);
            var walkClip = MakeClip("Farhad_Walk", walk, WalkFramesPerSecond, loop: true);

            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter(WalkingParameter, AnimatorControllerParameterType.Bool);
            var states = controller.layers[0].stateMachine;
            var idleState = states.AddState("Idle");
            idleState.motion = idleClip;
            var walkState = states.AddState("Walk");
            walkState.motion = walkClip;
            states.defaultState = idleState;
            AddInstantTransition(idleState, walkState, AnimatorConditionMode.If);
            AddInstantTransition(walkState, idleState, AnimatorConditionMode.IfNot);

            var summary = new System.Text.StringBuilder();
            foreach (var (direction, impactFrame) in Strikes)
            {
                // A swing can start from any state, and always plays to its end before he stands again.
                string name = FarhadController.StrikeParameter(direction);
                var clip = MakeClip("Farhad_" + name, StrikeFrames(strike, impactFrame), StrikeFramesPerSecond * 2f, loop: false);
                controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
                var strikeState = states.AddState(name);
                strikeState.motion = clip;
                var toStrike = states.AddAnyStateTransition(strikeState);
                toStrike.hasExitTime = false;
                toStrike.duration = 0f;
                toStrike.AddCondition(AnimatorConditionMode.If, 0f, name);
                var fromStrike = strikeState.AddTransition(idleState);
                fromStrike.hasExitTime = true;
                fromStrike.exitTime = 1f;
                fromStrike.duration = 0f;

                var timing = TimingOf(direction);
                summary.Append($", {direction.ToString().ToLower()} to frame {impactFrame} " +
                               $"(impact at {timing.impactTime:0.00}s of {timing.duration:0.00}s)");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Farhad animations built: idle, {walk.Length} walk frames, {strike.Length} strike frames{summary}");
            return controller;
        }

        // The swing in steps of half a drawn frame: each frame up to the impact shown for two steps,
        // the impact held, then the frames back down to the first shown for one step each.
        static Sprite[] StrikeFrames(Sprite[] strike, int impactFrame)
        {
            var forward = strike.Take(impactFrame).SelectMany(frame => new[] { frame, frame });
            var hold = Enumerable.Repeat(strike[impactFrame - 1], StrikeHoldFrames * 2);
            var back = strike.Take(impactFrame - 1).Reverse();
            return forward.Concat(hold).Concat(back).ToArray();
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
            settings.spriteMeshType = SpriteMeshType.FullRect; // a tight mesh would clip his glow
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
