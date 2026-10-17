using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Editor helpers for the overworld scenes (greybox NavMesh bake).</summary>
public static class OverworldSceneSetup
{
    const string DungeonScenePath = "Assets/_Project/Scenes/Overworld/Dungeon_01.unity";

    [MenuItem("Tools/Overworld/Bake Dungeon_01 NavMesh")]
    public static void BakeDungeonNavMesh()
    {
        var scene = EditorSceneManager.OpenScene(DungeonScenePath, OpenSceneMode.Single);
        var dungeon = GameObject.Find("OverworldRoot/Dungeon");
        if (dungeon == null) { Debug.LogError("OverworldRoot/Dungeon not found"); return; }

        var surface = dungeon.GetComponent<NavMeshSurface>();
        if (surface == null) surface = dungeon.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Dungeon_01 NavMesh baked and saved.");
    }
}
