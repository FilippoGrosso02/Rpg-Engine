using Game.Audio;
using Game.Data.State;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Overworld
{
    /// <summary>
    /// "Demo complete" screen shown after the final boss falls. Freezes the game and offers a fresh run or the main menu. Session state
    /// (defeated enemies, chests, HP, items) is reset before either, since all of it is static. Plain IMGUI for now.
    /// </summary>
    public class DemoEndPanel : MonoBehaviour
    {
        public const string MainMenuScene = "MainMenu";

        public static bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => IsOpen = false;

        public static void Show()
        {
            if (IsOpen) return;
            IsOpen = true;
            Time.timeScale = 0f;
            GameAudio.SetMusicState(AudioMusicState.Victory);
        }

        /// <summary>Forgets everything the run changed so the next one starts clean.</summary>
        public static void ResetRun()
        {
            DefeatedEnemies.Clear();
            WorldFlags.Clear();
            PartyState.HealAll();
            Inventory.Reset();
        }

        void OnDestroy()
        {
            if (IsOpen) Close();
        }

        static void Close()
        {
            IsOpen = false;
            Time.timeScale = 1f;
        }

        void OnGUI()
        {
            if (!IsOpen) return;

            var box = new GUIStyle(GUI.skin.box) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            var button = new GUIStyle(GUI.skin.button) { fontSize = 20 };

            float w = 420f, h = 230f;
            var rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(rect, "DEMO COMPLETE\n\nThe warlord is defeated.", box);

            if (GUI.Button(new Rect(rect.x + 40f, rect.y + 140f, w - 80f, 36f), "Play again", button))
            {
                ResetRun();
                Close();
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }

            if (GUI.Button(new Rect(rect.x + 40f, rect.y + 182f, w - 80f, 36f), "Main menu", button))
            {
                ResetRun();
                Close();
                SceneManager.LoadScene(MainMenuScene);
            }
        }
    }
}
