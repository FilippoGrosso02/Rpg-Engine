using Game.Combat.Contracts;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Turns a fight's <see cref="ICombatEvents"/> into sound cues, the same way the HUD turns them into widgets: combat never knows audio
    /// exists. Attach once per encounter (done by CombatSystemConfig); it detaches itself when the encounter ends.
    /// </summary>
    public sealed class CombatAudioBridge
    {
        readonly ICombatEvents _events;

        CombatAudioBridge(ICombatEvents events) => _events = events;

        public static void Attach(ICombatEvents events)
        {
            if (events == null) return;

            var bridge = new CombatAudioBridge(events);
            events.OnDamageRolled += bridge.HandleDamage;
            events.OnCombatantDefeated += bridge.HandleDefeated;
            events.OnEncounterEnded += bridge.HandleEnded;
            GameAudio.Play(AudioCue.BattleStart);
        }

        /// <summary>The swing / cast sound: a spell is a cast, a weapon attack is a hit or a miss.</summary>
        public static AudioCue CueFor(DamageRollInfo info)
        {
            if (info != null && !string.IsNullOrEmpty(info.spellName)) return AudioCue.SpellCast;
            return info != null && info.hit ? AudioCue.AttackHit : AudioCue.AttackMiss;
        }

        public static AudioCue CueFor(EncounterResult result) => result != null && result.victory ? AudioCue.BattleWin : AudioCue.BattleLose;

        void HandleDamage(DamageRollInfo info)
        {
            if (info == null) return;
            var target = info.target;
            Vector3 where = target == null ? Vector3.zero : target.viewTransform != null ? target.viewTransform.position : target.position;
            GameAudio.Play(CueFor(info), where);
            if (info.hit) GameAudio.Play(AudioCue.DamageTaken, where); // the impact on whoever was hit, layered on the swing
        }

        void HandleDefeated(CombatantRuntime _) => GameAudio.Play(AudioCue.CombatantDefeated);

        void HandleEnded(EncounterResult result)
        {
            _events.OnDamageRolled -= HandleDamage;
            _events.OnCombatantDefeated -= HandleDefeated;
            _events.OnEncounterEnded -= HandleEnded;

            // A fled fight is neither a win nor a wipe: it gets its own sound instead of a stinger.
            if (result != null && result.fled)
            {
                GameAudio.Play(AudioCue.Flee);
                return;
            }

            GameAudio.Play(CueFor(result));
        }
    }
}
