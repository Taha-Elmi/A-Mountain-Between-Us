using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Farhad.EditorTools
{
    // Imports Farhad's character frames (made by Tools/process_farhad_frames.ps1) and builds his
    // animation clips and Animator Controller from them.
    //
    // Frame spec: 845x1510 px crops of the artist's canvas; Farhad is ~1475 px tall, imported at
    // 820 px per unit so he stands ~1.8 units (tiles) tall. The pivot sits between his feet on the
    // body's center line, so flipping him to face left keeps him in place.
    public static class FarhadArtSetup
    {
        public const string ArtDir = "Assets/Art/Characters/Farhad";
        const string AnimationDir = "Assets/Animation/Farhad";
        const string ControllerPath = AnimationDir + "/Farhad.controller";
        public const string WalkingParameter = "Walking";

        const float PixelsPerUnit = 820f;
        static readonly Vector2 FeetPivot = new(0.412f, 0.013f);
        const float WalkFramesPerSecond = 12f;

        public static bool HasArt => AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/farhad_idle.png") != null;

        [MenuItem("Farhad/Rebuild Farhad Animations")]
        public static AnimatorController Build()
        {
            var idle = ImportSprite(ArtDir + "/farhad_idle.png");
            var walk = AssetDatabase.FindAssets("farhad_walk_ t:Texture2D", new[] { ArtDir + "/Walk" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path)
                .Select(ImportSprite)
                .ToArray();

            EnsureFolder(AnimationDir);
            var idleClip = MakeClip("Farhad_Idle", new[] { idle }, 1f);
            var walkClip = MakeClip("Farhad_Walk", walk, WalkFramesPerSecond);

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

            AssetDatabase.SaveAssets();
            Debug.Log($"Farhad animations built: idle + {walk.Length} walk frames at {WalkFramesPerSecond} fps");
            return controller;
        }

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

        static AnimationClip MakeClip(string name, Sprite[] frames, float framesPerSecond)
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
            clipSettings.loopTime = true;
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
