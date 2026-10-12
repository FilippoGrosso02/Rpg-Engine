using Game.Combat.Contracts;
using UnityEngine;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// The Fear meter: 0-10 per combatant, gained on being hit, decays at turn start if unhit
    /// since the combatant's last turn, and imposes ATK/DEF penalties at thresholds. Mirrors
    /// StatusEffectRuntime's shape (static, operates directly on CombatantRuntime fields) but
    /// Fear is a standalone accumulating meter, not a StatusEffectDefinition-backed status.
    /// </summary>
    public static class FearRuntime
    {
        private const int MaxFear = 10;
        private const int ThresholdLow = 4;
        private const int ThresholdMid = 7;
        private const int ThresholdFreeze = 10;

        private const int NormalHitGain = 2;
        private const int CritHitGain = 3;
        private const int HorrorBonusGain = 1;

        /// <summary>Same penalty applies to both ATK and DEF per the rules doc.</summary>
        public static int GetStatPenalty(CombatantRuntime combatant)
        {
            if (combatant.fear >= ThresholdFreeze) return 3;
            if (combatant.fear >= ThresholdMid) return 2;
            if (combatant.fear >= ThresholdLow) return 1;
            return 0;
        }

        public static bool ShouldFreeze(CombatantRuntime combatant) => combatant.fear >= ThresholdFreeze;

        public static void ApplyFearFromHit(CombatantRuntime target, bool wasCrit, bool attackerIsHorror)
        {
            int gain = wasCrit ? CritHitGain : NormalHitGain;
            if (attackerIsHorror)
            {
                gain += HorrorBonusGain;
            }

            target.fear = Mathf.Clamp(target.fear + gain, 0, MaxFear);
            target.wasHitSinceLastTurn = true;
        }

        /// <summary>Called once at a combatant's own turn start: decays -1 if it wasn't hit
        /// since its last turn, then resets the hit-tracking flag for the turn ahead.</summary>
        public static void DecayAtTurnStart(CombatantRuntime combatant)
        {
            if (!combatant.wasHitSinceLastTurn)
            {
                combatant.fear = Mathf.Max(0, combatant.fear - 1);
            }

            combatant.wasHitSinceLastTurn = false;
        }
    }
}
