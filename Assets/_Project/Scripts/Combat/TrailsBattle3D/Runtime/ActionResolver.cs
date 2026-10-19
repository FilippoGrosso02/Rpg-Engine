using System.Collections.Generic;
using System.Linq;
using Game.Combat.Contracts;
using Game.Common.Area;
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

        public const float DefaultAoeRadius = 3.5f;
        public const float DefaultConeLength = 6f;
        public const float DefaultConeAngle = 90f;
        public const float DefaultLineLength = 8f;
        public const float DefaultLineWidth = 2f;
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
                    // Circle: centred on the primary target. Cone / Line: start at the caster and point at the primary target.
                    // The same AreaShape drives the on-screen preview, so what is shown is what is hit.
                    if (!TryGetAreaShape(skill, out var shape))
                    {
                        shape = AreaShape.Circle(DefaultAoeRadius);
                    }

                    var aim = primaryTarget != null ? primaryTarget.position : user.position;
                    GetAreaFrame(skill, user.position, aim, out var origin, out var forward);

                    return allCombatants
                        .Where(c => !c.IsDefeated
                                    && c.isPlayerSide != user.isPlayerSide
                                    && shape.Contains(origin, forward, c.position))
                        .ToList();
                }
            }
        }

        /// <summary>The ground-plane shape of an area skill (size/angle/width from the skill, falling back to defaults). False for non-area skills.</summary>
        public static bool TryGetAreaShape(SkillDefinition skill, out AreaShape shape)
        {
            switch (skill.targetType)
            {
                case TargetType.AreaCircle:
                    shape = AreaShape.Circle(skill.aoeSize > 0f ? skill.aoeSize : DefaultAoeRadius);
                    return true;

                case TargetType.AreaCone:
                    shape = AreaShape.Cone(skill.aoeSize > 0f ? skill.aoeSize : DefaultConeLength,
                        skill.aoeAngle > 0f ? skill.aoeAngle : DefaultConeAngle);
                    return true;

                case TargetType.AreaLine:
                    shape = AreaShape.Line(skill.aoeSize > 0f ? skill.aoeSize : DefaultLineLength,
                        skill.aoeWidth > 0f ? skill.aoeWidth : DefaultLineWidth);
                    return true;

                default:
                    shape = default;
                    return false;
            }
        }

        /// <summary>Where an area skill is anchored and which way it faces, given the caster and the point being aimed at.</summary>
        public static void GetAreaFrame(SkillDefinition skill, Vector3 userPosition, Vector3 aimPosition, out Vector3 origin, out Vector3 forward)
        {
            forward = aimPosition - userPosition;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            origin = skill.targetType == TargetType.AreaCircle ? aimPosition : userPosition;
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
