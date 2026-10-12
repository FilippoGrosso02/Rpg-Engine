using System;
using Game.Combat.Contracts;
using Game.Data.Definitions;
using Game.Data.Registry;
using UnityEngine;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Apply/tick/expire for statuses on a CombatantRuntime. No stacking rule is defined
    /// in StatusEffectDefinition (schema gap, see project memory) — reapplying an already
    /// active status just refreshes its remaining duration instead of stacking.
    /// </summary>
    public static class StatusEffectRuntime
    {
        public static void TryApply(CombatantRuntime target, string statusEffectId, float chance)
        {
            if (string.IsNullOrEmpty(statusEffectId))
            {
                return;
            }

            if (UnityEngine.Random.value > chance)
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

        /// <summary>Ticks DoT damage and counts down duration, removing expired statuses.</summary>
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

        public static int GetAtkModifier(CombatantRuntime combatant) => SumStatus(combatant, d => d.atkModifier);
        public static int GetDefModifier(CombatantRuntime combatant) => SumStatus(combatant, d => d.defModifier);
        public static int GetSpeedModifier(CombatantRuntime combatant) => SumStatus(combatant, d => d.speedModifier);

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
