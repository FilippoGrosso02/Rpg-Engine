using System.Collections.Generic;
using System.Linq;
using Game.Combat.Contracts;
using Game.Data.Definitions;
using Game.Data.Enums;
using UnityEngine;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Resolves targeting, damage, and Guard per the Duelwright rules: damage = 1d6 + ATK -
    /// DEF (min 1), natural 6 crits and ignores DEF entirely (damage = ATK + 6). Fear
    /// (FearRuntime) and status (StatusEffectRuntime) modifiers both apply to effective
    /// ATK/DEF; effective DEF is floored at 0. A handful of cards override parts of this via
    /// SkillDefinition's Duelwright fields (bonusPower/ignoresDef/skipDiceRoll/extraDiceCount/
    /// critThreshold) or CardEffects (Banish's conditional DEF handling).
    /// </summary>
    public static class ActionResolver
    {
        /// <summary>World units per point of attackRangeMultiplier — a combatant's actual
        /// attack range is attackRangeMultiplier * RangeUnit. Melee's multiplier of 1 gives a
        /// RangeUnit-distance melee reach; ranged characters use a higher multiplier instead
        /// of the old unlimited-range Ranged trait.</summary>
        public const float RangeUnit = 2f;

        private const float AoeRadius = 3.5f;
        private const int GuardDefBonus = 2;
        private const int DefaultCritThreshold = 6;

        private static readonly System.Random SharedRandom = new System.Random();

        public static List<CombatantRuntime> ResolveTargets(
            SkillDefinition skill,
            CombatantRuntime user,
            CombatantRuntime primaryTarget,
            IEnumerable<CombatantRuntime> allCombatants)
        {
            switch (skill.targetType)
            {
                case TargetType.Self:
                    return new List<CombatantRuntime> { user };

                case TargetType.SingleAlly:
                case TargetType.SingleEnemy:
                    return primaryTarget != null
                        ? new List<CombatantRuntime> { primaryTarget }
                        : new List<CombatantRuntime>();

                case TargetType.AllEnemies:
                    return allCombatants
                        .Where(c => !c.IsDefeated && c.isPlayerSide != user.isPlayerSide)
                        .ToList();

                case TargetType.AllAllies:
                    return allCombatants
                        .Where(c => !c.IsDefeated && c.isPlayerSide == user.isPlayerSide)
                        .ToList();

                case TargetType.AreaCircle:
                case TargetType.AreaCone:
                case TargetType.AreaLine:
                default:
                {
                    // Only AreaCircle is implemented for the one sample AOE craft in this
                    // phase; Cone/Line fall back to a circle centered on the primary target.
                    var center = primaryTarget != null ? primaryTarget.position : user.position;
                    return allCombatants
                        .Where(c => !c.IsDefeated
                                    && c.isPlayerSide != user.isPlayerSide
                                    && Vector3.Distance(c.position, center) <= AoeRadius)
                        .ToList();
                }
            }
        }

        /// <summary>
        /// Duelwright Basic Attack / damaging-card formula. <paramref name="rng"/> defaults to
        /// a shared instance but accepts a seeded System.Random so tests can assert exact
        /// outcomes instead of asserting on live randomness. Returns the full breakdown (not
        /// just the final number) so UI code can show "ATK + roll - DEF = damage".
        /// </summary>
        public static DamageRollInfo ResolveDamage(
            CombatantRuntime attacker,
            CombatantRuntime target,
            SkillDefinition skill,
            System.Random rng = null)
        {
            rng ??= SharedRandom;

            int fearAtkPenalty = FearRuntime.GetStatPenalty(attacker);
            int fearDefPenalty = FearRuntime.GetStatPenalty(target);

            int effectiveAtk = attacker.atk + StatusEffectRuntime.GetAtkModifier(attacker) - fearAtkPenalty;
            int effectiveDef = target.def + StatusEffectRuntime.GetDefModifier(target) - fearDefPenalty;
            if (target.guarding)
            {
                effectiveDef += GuardDefBonus;
            }
            effectiveDef = Mathf.Max(0, effectiveDef);

            bool ignoresDef = skill.ignoresDef || CardEffects.BanishIgnoresDef(skill.id, target);
            if (!ignoresDef && CardEffects.BanishHalvesDef(skill.id, target))
            {
                effectiveDef /= 2;
            }

            var info = new DamageRollInfo
            {
                attacker = attacker,
                target = target,
                effectiveAtk = effectiveAtk,
                effectiveDef = effectiveDef,
                defIgnored = ignoresDef
            };

            if (skill.skipDiceRoll)
            {
                info.diceRoll = 0;
                info.damage = Mathf.Max(1, effectiveAtk + skill.bonusPower);
                return info;
            }

            int diceCount = Mathf.Max(1, 1 + skill.extraDiceCount);
            int diceTotal = 0;
            for (int i = 0; i < diceCount; i++)
            {
                diceTotal += rng.Next(1, 7);
            }
            info.diceRoll = diceTotal;

            // Crit rule ("natural 6") only applies to a single-die roll — multi-dice cards
            // (e.g. Point-Blank Blast) roll flat, no crit check.
            int critThreshold = skill.critThreshold > 0 ? skill.critThreshold : DefaultCritThreshold;
            if (diceCount == 1 && diceTotal >= critThreshold)
            {
                info.wasCrit = true;
                info.defIgnored = true;
                info.damage = effectiveAtk + 6 + skill.bonusPower;
                return info;
            }

            int raw = diceTotal + effectiveAtk + skill.bonusPower - (ignoresDef ? 0 : effectiveDef);
            info.damage = Mathf.Max(1, raw);
            return info;
        }

        /// <summary>
        /// Brings a defeated combatant back at <see cref="SkillDefinition.reviveHp"/> HP (at least 1, at most max HP),
        /// clearing fear, statuses and one-turn flags. Returns false (and changes nothing) if the target is still standing.
        /// </summary>
        public static bool ApplyRevive(CombatantRuntime target, SkillDefinition skill)
        {
            if (target == null || !target.IsDefeated)
            {
                return false;
            }

            target.currentHp = Mathf.Clamp(skill.reviveHp, 1, Mathf.Max(1, target.maxHp));
            target.fear = 0;
            target.wasHitSinceLastTurn = false;
            target.guarding = false;
            target.untargetable = false;
            target.statuses.Clear();
            return true;
        }

        public static void ApplyGuard(CombatantRuntime combatant)
        {
            combatant.guarding = true;
            combatant.pendingGuardApBonus = true;
            combatant.fear = Mathf.Max(0, combatant.fear - 2);
        }
    }
}
