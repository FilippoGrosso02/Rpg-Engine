using System.Collections.Generic;
using Game.Data.Registry;
using Game.Data.State;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>
    /// Player input for the party rules: <see cref="modeKey"/> toggles Attached / Detached, <see cref="hideKey"/> makes the leader's DEX
    /// check to hide (Detached only). Also draws a small status line. Lives next to the <see cref="OverworldDirector"/>; added by it
    /// when the party is on the map.
    /// </summary>
    public class PartyStealthController : MonoBehaviour
    {
        public KeyCode modeKey = KeyCode.R;
        [Tooltip("Press to start sneaking while the party is detached; press again to walk normally.")]
        public KeyCode sneakKey = KeyCode.LeftShift;

        [Tooltip("After a failed hide check the leader has to wait this long before trying again.")]
        public float retrySeconds = 4f;

        [Tooltip("Off: pressing Shift to sneak always works. On: the leader must pass the DEX check (rules section 2.2); a failure means waiting retrySeconds.")]
        public bool requireDexCheck;

        OverworldDirector _director;
        float _nextAttempt;
        GUIStyle _style;

        void Awake() => _director = GetComponent<OverworldDirector>();

        void Update()
        {
            if (_director == null || _director.InBattle || _director.player == null) return;
            if (Cursor.lockState != CursorLockMode.Locked || Time.timeScale <= 0f || _director.player.movementLocked) return;

            if (Input.GetKeyDown(modeKey)) ToggleMode();

            // Detached starts visible. Shift toggles sneaking on and off; being spotted also drops it.
            if (!PartyRules.CanStealth) return;
            if (!Input.GetKeyDown(sneakKey) && !Input.GetKeyDown(KeyCode.RightShift)) return;

            if (PartyRules.Hidden)
            {
                PartyRules.EndHidden();
                InteractionSystem.ShowMessage("Walking normally.");
            }
            else
            {
                _nextAttempt = 0f;
                TryHide();
            }
        }

        public static void ToggleMode()
        {
            var party = FindAnyObjectByType<PartyFollowers>();
            if (party != null && party.SwitchInProgress) return;

            bool attach = PartyRules.Mode == PartyMode.Detached;
            PartyRules.SetMode(attach ? PartyMode.Attached : PartyMode.Detached);
            InteractionSystem.ShowMessage(attach
                ? "Party attached. The party sticks together."
                : "Party detached. Followers hold their ground. Press Shift to sneak.");
        }

        void TryHide()
        {
            if (PartyRules.Hidden || Time.time < _nextAttempt) return;

            // Nobody can slip out of sight while an enemy is hunting them; try again shortly.
            foreach (var enemy in FindObjectsByType<OverworldEnemy>())
            {
                if (enemy.CurrentState != OverworldEnemy.State.Chase) continue;
                _nextAttempt = Time.time + 1f;
                return;
            }

            var leader = PartyRoster.Count > 0 ? DatabaseRegistry.GetActor(PartyRoster.Members[0]) : null;
            int dexterity = leader != null ? leader.dexterity : 7;
            int roll = Random.Range(1, 11);
            if (!requireDexCheck || PartyRules.Check(roll, dexterity, PartyRules.StealthDifficulty))
            {
                PartyRules.BecomeHidden();
                InteractionSystem.ShowMessage("HIDDEN. Strike first, or stay out of sight.");
            }
            else
            {
                _nextAttempt = Time.time + retrySeconds;
                InteractionSystem.ShowMessage("Could not hide. Try again in a moment.");
            }
        }

        /// <summary>
        /// The party member with the highest MIND makes a MIND check against <paramref name="difficulty"/> (any of those tied for highest
        /// may roll). Returns true when it passes; <paramref name="who"/> is the roller's name.
        /// </summary>
        public static bool RollMindCheck(int difficulty, out string who)
        {
            var names = new List<string>();
            var scores = new List<int>();
            foreach (var id in PartyRoster.Members)
            {
                var actor = DatabaseRegistry.GetActor(id);
                if (actor == null) continue;
                names.Add(string.IsNullOrEmpty(actor.displayName) ? actor.id : actor.displayName);
                scores.Add(actor.mind);
            }

            who = null;
            if (scores.Count == 0) return false;

            var tied = PartyRules.HighestScoreIndices(scores);
            int roller = tied[Random.Range(0, tied.Count)];
            who = names[roller];
            return PartyRules.Check(Random.Range(1, 11), scores[roller], difficulty);
        }

        void OnGUI()
        {
            if (_director == null || !_director.UsesInPlaceCombat || _director.InBattle || DemoEndPanel.IsOpen) return;

            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerLeft };
            string text = PartyRules.Mode == PartyMode.Attached ? "ATTACHED  [R] detach" : PartyRules.Hidden ? "DETACHED - SNEAKING  [Shift] walk normally  [R] attach" : "DETACHED  [Shift] sneak  [R] attach";
            var rect = new Rect(16f, Screen.height - 36f, 420f, 24f);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, _style);
            GUI.color = PartyRules.Hidden ? new Color(0.45f, 1f, 0.5f) : Color.white;
            GUI.Label(rect, text, _style);
            GUI.color = Color.white;
        }
    }
}
