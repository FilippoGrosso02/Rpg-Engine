using Game.Data.Definitions;
using Game.Data.Enums;
using Game.Data.Registry;

namespace Game.Combat.UI.UGUI
{
    /// <summary>One-line, upper-case descriptions of a spell for the tooltip and damage box, built from its definition so they can't drift from the rules.</summary>
    public static class SpellText
    {
        /// <summary>"MAGIC MISSILE - AP 1".</summary>
        public static string Title(SkillDefinition skill)
        {
            return $"{skill.displayName.ToUpperInvariant()} - AP {skill.apCost}";
        }

        /// <summary>"NEVER MISSES D6", "VIT SAVE OR STUCK 2T", "AREA 2D6 VIT SAVE HALF".</summary>
        public static string Effect(SkillDefinition skill)
        {
            string dice = skill.damage.HasDice ? skill.damage.ToString().ToUpperInvariant() : null;
            string save = skill.saveType == SaveType.Vitality ? "VIT" : skill.saveType == SaveType.Mind ? "MIND" : null;
            string area = skill.targetType == TargetType.AreaCircle || skill.targetType == TargetType.AreaCone || skill.targetType == TargetType.AreaLine
                ? "AREA "
                : "";

            if (skill.autoHit)
            {
                return "NEVER MISSES " + dice;
            }

            if (save != null && skill.halfDamageOnSave)
            {
                return $"{area}{dice} {save} SAVE HALF";
            }

            if (save != null)
            {
                return $"{save} SAVE OR {StatusPhrase(skill)}";
            }

            return $"{area}{dice}".Trim();
        }

        private static string StatusPhrase(SkillDefinition skill)
        {
            if (string.IsNullOrEmpty(skill.statusEffectId))
            {
                return "FAIL";
            }

            var status = DatabaseRegistry.GetStatusEffect(skill.statusEffectId);
            if (status == null)
            {
                return "AFFECTED";
            }

            string name = status.displayName.ToUpperInvariant();
            return status.duration > 0 ? $"{name} {status.duration}T" : name;
        }
    }
}
