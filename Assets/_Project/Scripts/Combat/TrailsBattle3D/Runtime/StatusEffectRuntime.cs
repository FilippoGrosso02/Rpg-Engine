using System;
using Game.Combat.Contracts;
using Game.Data.Definitions;
using Game.Data.Registry;
using UnityEngine;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Apply/tick/expire for statuses on a CombatantRuntime. Reapplying an already active status just refreshes its
    /// remaining duration instead of stacking. Durations count the affected unit's own turns and are ticked at the END
    /// of each of its turns, so a 2-turn web really holds it for two whole turns.
    /// </summary>
    public static class StatusEffectRuntime
    {
        /// <param name="rng">Optional seeded generator so tests can assert exact outcomes.</param>
        public static void TryApply(CombatantRuntime target, string statusEffectId, float chance, System.Random rng = null)
        {
            if (string.IsNullOrEmpty(statusEffectId))
            {
                return;
            }

            double roll = rng != null ? rng.NextDouble() : UnityEngine.Random.value;
            if (roll > chance)
            {
                return;
            }

            var definition = DatabaseRegistry.GetStatusEffect(statusEffectId);
            if (definition == null)
            {
                return;
            }

            var existing = target.statuses.Find(s => s.statusEffectId == statusEffectId);
            if (existing != null)
            {
                existing.remainingDuration = definition.duration;
                return;
            }

            target.statuses.Add(new StatusEffectInstance
            {
                statusEffectId = statusEffectId,
                remainingDuration = definition.duration
            });
        }

        /// <summary>Ticks DoT damage and counts down duration, removing expired statuses. Called at the end of the unit's own turn.</summary>
        public static void TickAndExpire(CombatantRuntime combatant)
        {
            for (int i = combatant.statuses.Count - 1; i >= 0; i--)
            {
                var instance = combatant.statuses[i];
                var definition = DatabaseRegistry.GetStatusEffect(instance.statusEffectId);
                if (definition == null)
                {
                    combatant.statuses.RemoveAt(i);
                    continue;
                }

                if (definition.tickDamage > 0)
                {
                    combatant.currentHp = Mathf.Max(0, combatant.currentHp - definition.tickDamage);
                }

                instance.remainingDuration--;
                if (instance.remainingDuration <= 0)
                {
                    combatant.statuses.RemoveAt(i);
                }
            }
        }

        public static bool IsActionPrevented(CombatantRuntime combatant)
        {
            return AnyStatus(combatant, d => d.preventsAction);
        }

        public static bool IsMovementPrevented(CombatantRuntime combatant)
        {
            return AnyStatus(combatant, d => d.preventsMovement);
        }

        public static int GetAttackModifier(CombatantRuntime combatant) => SumStatus(combatant, d => d.attackModifier);

        public static int GetArmorClassModifier(CombatantRuntime combatant) => SumStatus(combatant, d => d.armorClassModifier);

        private static bool AnyStatus(CombatantRuntime combatant, Func<StatusEffectDefinition, bool> predicate)
        {
            foreach (var instance in combatant.statuses)
            {
                var definition = DatabaseRegistry.GetStatusEffect(instance.statusEffectId);
                if (definition != null && predicate(definition))
                {
                    return true;
                }
            }

            return false;
        }

        private static int SumStatus(CombatantRuntime combatant, Func<StatusEffectDefinition, int> selector)
        {
            int total = 0;
            foreach (var instance in combatant.statuses)
            {
                var definition = DatabaseRegistry.GetStatusEffect(instance.statusEffectId);
                if (definition != null)
                {
                    total += selector(definition);
                }
            }

            return total;
        }
    }
}
