using UnityEngine;
using Game.Data.Enums;

namespace Game.Data.Definitions
{
    [CreateAssetMenu(fileName = "NewSkill", menuName = "Database/Skill")]
    public class SkillDefinition : GameDefinition
    {
        [TextArea] public string description;
        public Sprite icon;

        public ActionCost actionCost;
        public TargetType targetType;
        public int range;

        public int resourceCost; // MP

        public int power; // the formula (power + casterAtk, etc.) lives in combat code later
        public DamageType damageType;

        public string statusEffectId; // nullable: empty = none
        [Range(0f, 1f)] public float statusEffectChance; // only read if statusEffectId is set
    }
}
