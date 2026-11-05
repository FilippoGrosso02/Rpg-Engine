using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Overworld
{
    /// <summary>Press E next to it to load another overworld scene (a dungeon entrance or exit). The scene must be in Build Settings.</summary>
    public class DungeonPortal : Interactable
    {
        [Tooltip("Name of the scene to load, e.g. Dungeon_03.")]
        public string sceneName;

        public override void Interact()
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            SceneManager.LoadScene(sceneName);
        }
    }
}
