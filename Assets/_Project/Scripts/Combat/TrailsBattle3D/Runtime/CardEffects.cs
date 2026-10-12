using Game.Combat.Contracts;
using Game.Data.Registry;
using UnityEngine;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// The handful of card behaviors too bespoke for SkillDefinition's generic fields
    /// (apCost/bonusPower/ignoresDef/fearDelta/isHeal/etc.) to express — keyed by skill id,
    /// applied by EncounterRunner.ExecuteSkill after the generic damage/status/heal/fear pass.
    /// Skill ids here must match the assets authored for the roster (DuelwrightRosterSetup).
    /// </summary>
    public static class CardEffects
    {
        public const string StickyFingers = "skill_nun_sticky_fingers";
        public const string SmokeBomb = "skill_nun_smoke_bomb";
        public const string NobleChallenge = "skill_count_noble_challenge";
        public const string LifeBond = "skill_countess_life_bond";
        public const string Bodyguard = "skill_guardsman_bodyguard";
        public const string Banish = "skill_exorcist_banish";
        public const string CleansingRite = "skill_exorcist_cleansing_rite";

        private const int StickyFingersStealAmount = 2;
        private const int LifeBondDurationTurns = 2;

        /// <summary>Applies the bespoke on-use effect for known skill ids. Returns false for
        /// any id it doesn't recognize (the generic SkillDefinition-field pass already covers
        /// everything else).</summary>
        public static bool TryApply(string skillId, CombatantRuntime actor, CombatantRuntime primaryTarget)
        {
            switch (skillId)
            {
                case StickyFingers:
                    if (primaryTarget != null)
                    {
                        int stolen = Mathf.Min(StickyFingersStealAmount, primaryTarget.currentAp);
                        primaryTarget.currentAp -= stolen;
                        actor.currentAp = Mathf.Min(actor.maxAp, actor.currentAp + stolen);
                    }
                    return true;

                case SmokeBomb:
                    actor.untargetable = true;
                    return true;

                case NobleChallenge:
                    if (primaryTarget != null)
                    {
                        primaryTarget.forcedAttackTarget = actor;
                    }
                    return true;

                case LifeBond:
                    if (primaryTarget != null)
                    {
                        primaryTarget.lifeBondProtector = actor;
                        primaryTarget.lifeBondTurnsRemaining = LifeBondDurationTurns;
                    }
                    return true;

                case Bodyguard:
                    if (primaryTarget != null)
                    {
                        actor.bodyguardTarget = primaryTarget;
                    }
                    return true;

                case CleansingRite:
                    // Removes any DoT-carrying status (poison, in practice — StatusEffectDefinition
                    // has no explicit "is poison" tag, so a DoT (tickDamage > 0) is the closest fit).
                    if (primaryTarget != null)
                    {
                        primaryTarget.statuses.RemoveAll(s =>
                        {
                            var definition = DatabaseRegistry.GetStatusEffect(s.statusEffectId);
                            return definition != null && definition.tickDamage > 0;
                        });
                    }
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>Banish's DEF handling depends on the target's Horror trait at the moment of
        /// the hit, not a static SkillDefinition field — decided here instead.</summary>
        public static bool BanishIgnoresDef(string skillId, CombatantRuntime target)
        {
            return skillId == Banish && target.isHorror;
        }

        public static bool BanishHalvesDef(string skillId, CombatantRuntime target)
        {
            return skillId == Banish && !target.isHorror;
        }
    }
}
