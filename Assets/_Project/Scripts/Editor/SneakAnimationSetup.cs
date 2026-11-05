using System.Linq;
using Game.Data.Definitions;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Fills every Animation Set's sneak clips (crouch idle / crouch walk) from the DoubleL pack. Safe to re-run.</summary>
    public static class SneakAnimationSetup
    {
        const string IdlePath = "Assets/DoubleL/FBX Unity/One Hand Up/Movement/Crouch/Idle/Idle/1Hand_Up_Crouch_Idle_1.fbx";
        const string WalkPath = "Assets/DoubleL/FBX Unity/One Hand Up/Movement/Crouch/Base/InPlace/1Hand_Up_Crouch_F_InPlace.fbx";

        [MenuItem("Tools/Overworld/Assign Sneak Animations")]
        public static void Run()
        {
            var idle = LoadClip(IdlePath);
            var walk = LoadClip(WalkPath);
            if (idle == null || walk == null)
            {
                Debug.LogError("Sneak clips not found; check the paths in SneakAnimationSetup.");
                return;
            }

            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationSetDefinition"))
            {
                var set = AssetDatabase.LoadAssetAtPath<AnimationSetDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (set == null) continue;
                set.sneakIdle = idle;
                set.sneak = walk;
                EditorUtility.SetDirty(set);
                count++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Assigned sneak animations to {count} animation sets.");
        }

        static AnimationClip LoadClip(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }
}
