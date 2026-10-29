using System.Collections.Generic;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>
    /// Finds the nearest <see cref="Interactable"/> to the player, shows its "E - ..." prompt and triggers it on E. Also draws short
    /// on-screen messages ("Found: ..."). Sits next to the <see cref="OverworldDirector"/> (outside the root) and is added to it
    /// automatically, like the menu. The text is plain IMGUI for now.
    /// </summary>
    public class InteractionSystem : MonoBehaviour
    {
        public KeyCode interactKey = KeyCode.E;

        /// <summary>Set by the pause menu (a separate assembly that depends on this one) while it is open, which hides the prompt and ignores E.</summary>
        public static bool MenuOpen;

        const float ToastSeconds = 3.5f;

        struct Toast
        {
            public string text;
            public float until;
        }

        static readonly List<Toast> Toasts = new List<Toast>();

        OverworldDirector _director;
        Interactable _target;
        GUIStyle _promptStyle;
        GUIStyle _toastStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            MenuOpen = false;
            Toasts.Clear();
        }

        /// <summary>Shows a message for a few seconds (real time, so it also runs while the menu has paused the game).</summary>
        public static void ShowMessage(string text)
        {
            if (!string.IsNullOrEmpty(text)) Toasts.Add(new Toast { text = text, until = Time.unscaledTime + ToastSeconds });
        }

        public static int ActiveMessageCount
        {
            get
            {
                Toasts.RemoveAll(t => t.until <= Time.unscaledTime);
                return Toasts.Count;
            }
        }

        /// <summary>The nearest interactable that can currently be used and is within its own range of <paramref name="point"/>; null if none.</summary>
        public static Interactable FindNearest(Vector3 point)
        {
            Interactable best = null;
            float bestDistance = float.MaxValue;
            foreach (var candidate in Interactable.All)
            {
                if (candidate == null || !candidate.CanInteract) continue;
                float distance = candidate.DistanceTo(point);
                if (distance > candidate.range || distance >= bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }

            return best;
        }

        void Awake() => _director = GetComponent<OverworldDirector>();

        void Update()
        {
            _target = null;
            if (MenuOpen || DemoEndPanel.IsOpen || (_director != null && _director.InBattle)) return;
            if (_director == null || _director.player == null) return;

            _target = FindNearest(_director.player.transform.position);
            if (_target != null && Input.GetKeyDown(interactKey)) _target.Interact();
        }

        void OnGUI()
        {
            if (_director != null && _director.InBattle) return;

            _promptStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _toastStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true };

            if (_target != null && !MenuOpen)
                GUI.Box(new Rect(Screen.width / 2f - 150f, Screen.height - 110f, 300f, 34f), $"{interactKey} - {_target.prompt}", _promptStyle);

            Toasts.RemoveAll(t => t.until <= Time.unscaledTime);
            for (int i = 0; i < Toasts.Count; i++)
                GUI.Box(new Rect(Screen.width / 2f - 260f, 60f + i * 44f, 520f, 38f), Toasts[i].text, _toastStyle);
        }
    }
}
