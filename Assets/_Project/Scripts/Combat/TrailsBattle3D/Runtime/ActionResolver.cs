using System.Collections.Generic;
using System.Linq;
using Game.Combat.Contracts;
using Game.Common.Area;
using Game.Data.Definitions;
using Game.Data.Enums;
using Game.Data.Rules;
using UnityEngine;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Resolves targeting and the d10 OSR combat rules.
    ///
    /// Attack: d10 + ATK - DEF >= 6 (a natural 1 always misses, a natural 10 always hits), then the weapon's or stat block's
    /// damage dice (no ability bonus, at least 1). Spells: Magic Missile auto-hits; others may allow a save (d10 + VIT/MIND
    /// against the spell's DV, same rule) that negates the effect (Web) or halves the damage (Fireball). Slime acid corrodes
    /// armor and metal weapons.
    /// Each method takes an optional seeded <see cref="System.Random"/> so tests can assert exact outcomes.
    /// </summary>
    public static class ActionResolver
    {
        /// <summary>World units per point of attackRangeMultiplier — a combatant's actual
        /// attack range is attackRangeMultiplier * RangeUnit. Melee's multiplier of 1 gives a
        /// RangeUnit-distance melee reach.</summary>
        public const float RangeUnit = 2f;

        public const float DefaultAoeRadius = 3.5f;
        public const float DefaultConeLength = 6f;
        public const float DefaultConeAngle = 90f;
        public const float DefaultLineLength = 8f;
        public const float DefaultLineWidth = 2f;

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

        // ------------------------------------------------------------------ rules

        /// <summary>DEF including status modifiers (never below the minimum of 2).</summary>
        public static int GetArmorClass(CombatantRuntime target)
        {
            return Mathf.Max(OsrRules.MinDefense, target.ArmorClass + StatusEffectRuntime.GetArmorClassModifier(target));
        }

        /// <summary>ATK including status modifiers.</summary>
        public static int GetAttackBonus(CombatantRuntime attacker)
        {
            return attacker.attackBonus + StatusEffectRuntime.GetAttackModifier(attacker);
        }

        public static int GetSaveBonus(CombatantRuntime target, SaveType saveType)
        {
            switch (saveType)
            {
                case SaveType.Vitality: return target.saveVitality;
                case SaveType.Mind: return target.saveMind;
                default: return 0;
            }
        }

        /// <summary>
        /// One attack or spell against one target: the attack roll, the damage, and any save. Does not change HP, apply
        /// statuses or corrode anything — the caller does that from the returned breakdown, so this stays a pure function of its inputs.
        /// </summary>
        public static DamageRollInfo ResolveAttack(
            CombatantRuntime attacker,
            CombatantRuntime target,
            SkillDefinition skill,
            System.Random rng = null)
        {
            rng ??= SharedRandom;

            bool isSpell = !skill.usesWeaponDamage;
            var info = new DamageRollInfo
            {
                attacker = attacker,
                target = target,
                spellName = isSpell ? skill.displayName : null,
                targetArmorClass = GetArmorClass(target)
            };

            // 1. Does it land?
            if (isSpell && skill.autoHit)
            {
                info.autoHit = true;
                info.hit = true;
            }
            else if (isSpell && skill.saveType != SaveType.None)
            {
                // Save-based spells (Web, Fireball) have no attack roll; the target's save decides what happens.
                info.hit = true;
            }
            else
            {
                info.attackBonus = GetAttackBonus(attacker);
                info.attackRoll = rng.Next(1, OsrRules.DieSides + 1);
                info.naturalOne = info.attackRoll == 1;
                info.naturalTen = info.attackRoll == OsrRules.DieSides;
                info.hit = OsrRules.AttackHits(info.attackRoll, info.attackBonus, info.targetArmorClass);
            }

            if (!info.hit)
            {
                return info;
            }

            // 2. How hard?
            var dice = isSpell ? skill.damage : attacker.attackDamage;
            info.damageText = dice.ToString();
            info.damageBonus = dice.bonus;
            info.diceCount = dice.HasDice ? dice.count : 0;
            info.diceSides = dice.HasDice ? dice.sides : 0;
            int damage = 0;
            if (dice.HasDice || dice.bonus != 0)
            {
                info.diceTotal = dice.HasDice ? dice.RollDice(rng) : 0;
                if (!isSpell)
                {
                    info.corrosionPenalty = attacker.weaponCorrosion;
                }

                // A hit always does at least 1, however badly corroded the weapon is.
                damage = Mathf.Max(1, info.diceTotal + dice.bonus - info.corrosionPenalty);
            }

            // 3. Does the target resist?
            if (isSpell && skill.saveType != SaveType.None)
            {
                info.hasSave = true;
                info.saveBonus = GetSaveBonus(target, skill.saveType);
                info.saveDifficulty = skill.saveDifficulty;
                info.saveRoll = rng.Next(1, OsrRules.DieSides + 1);
                info.savePassed = OsrRules.TestPasses(info.saveRoll, info.saveBonus, info.saveDifficulty);

                if (info.savePassed)
                {
                    if (skill.halfDamageOnSave)
                    {
                        info.halved = true;
                        damage = damage > 0 ? Mathf.Max(1, damage / 2) : 0;
                    }
                    else
                    {
                        info.effectNegated = true;
                        damage = 0;
                    }
                }
            }

            info.damage = damage;
            return info;
        }

        /// <summary>
        /// The chance (0-100) that <paramref name="skill"/> works against <paramref name="target"/>, for the hit-chance badge. It uses
        /// exactly the dice rules ResolveAttack rolls: a weapon attack is the share of d10 rolls that hit (clamped to 10-90%), an
        /// auto-hit is 100, and a save-based spell is the share of d10 rolls the target FAILS its save on.
        /// </summary>
        public static int GetHitChancePercent(CombatantRuntime attacker, CombatantRuntime target, SkillDefinition skill)
        {
            bool isSpell = !skill.usesWeaponDamage;
            int successes = 0;

            for (int roll = 1; roll <= OsrRules.DieSides; roll++)
            {
                bool works;
                if (isSpell && skill.autoHit)
                {
                    works = true;
                }
                else if (isSpell && skill.saveType != SaveType.None)
                {
                    works = !OsrRules.TestPasses(roll, GetSaveBonus(target, skill.saveType), skill.saveDifficulty);
                }
                else
                {
                    works = OsrRules.AttackHits(roll, GetAttackBonus(attacker), GetArmorClass(target));
                }

                if (works)
                {
                    successes++;
                }
            }

            return Mathf.RoundToInt(successes * 100f / OsrRules.DieSides);
        }

        /// <summary>
        /// Slime acid, after a Basic Attack that hit: the attacker's armor corrodes by 1 AC (only while it still has armor to lose),
        /// and a metal weapon that hit a corrosive monster loses 1 damage, cumulatively, until sharpened.
        /// </summary>
        public static void ApplyAcidEffects(DamageRollInfo info)
        {
            if (info == null || !info.hit || info.spellName != null)
            {
                return;
            }

            if (info.attacker.corrodesArmor && info.target.armorBonus > info.target.armorCorrosion)
            {
                info.target.armorCorrosion++;
            }

            if (info.target.corrodesWeapons && info.attacker.weaponIsMetal)
            {
                info.attacker.weaponCorrosion++;
            }
        }

        /// <summary>Guard: +2 DEF until the next turn, and a caster earns 1 extra AP at that next turn.</summary>
        public static void ApplyGuard(CombatantRuntime combatant)
        {
            combatant.guarding = true;
            combatant.pendingGuardApBonus = combatant.usesAp;
        }

        /// <summary>
        /// Start-of-turn bookkeeping: Guard ends, and a caster gains AP — 1 at the start of the fight, +1 every turn after its first,
        /// and +1 more if it guarded last turn.
        /// </summary>
        public static void BeginTurn(CombatantRuntime combatant)
        {
            combatant.guarding = false;

            if (combatant.usesAp && combatant.turnsTaken > 0)
            {
                int gain = 1 + (combatant.pendingGuardApBonus ? 1 : 0);
                combatant.currentAp = Mathf.Min(combatant.maxAp, combatant.currentAp + gain);
            }

            combatant.pendingGuardApBonus = false;
            combatant.turnsTaken++;
        }

        /// <summary>d10 + the DEX minor modifier, rolled once per combatant at the start of the fight.</summary>
        public static int RollInitiative(CombatantRuntime combatant, System.Random rng)
        {
            return rng.Next(1, OsrRules.DieSides + 1) + combatant.initiative;
        }
    }
}
