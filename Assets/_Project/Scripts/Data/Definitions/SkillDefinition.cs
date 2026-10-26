using UnityEngine;
using Game.Data.Enums;

namespace Game.Data.Definitions
{
    /// <summary>
    /// A Basic Attack or a spell. The Basic Attack (first skill on every combatant) uses the attacker's own weapon or
    /// stat block; a spell carries its own damage dice, an AP cost equal to its level, and optionally a save.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSkill", menuName = "Database/Skill")]
    public class SkillDefinition : GameDefinition
    {
        [TextArea] public string description;
        public Sprite icon;
        public ActionCost actionCost;
        public TargetType targetType;
        public int range;

        // --- Area of effect (used when targetType is AreaCircle / AreaCone / AreaLine; 0 = use the default) ---
        /// <summary>Circle: radius (default 3.5). Cone / Line: length from the caster (defaults 6 / 8).</summary>
        public float aoeSize;

        /// <summary>Cone only: full opening angle in degrees (default 90).</summary>
        public float aoeAngle;

        /// <summary>Line only: full width (default 2).</summary>
        public float aoeWidth;

        public DamageType damageType;

        // --- Rules ---
        /// <summary>AP spent to cast this (a spell's level). The Basic Attack costs 0.</summary>
        public int apCost;

        /// <summary>Spell level, shown in the spell list. Informational: AP cost is <see cref="apCost"/>.</summary>
        public int spellLevel;

        /// <summary>True for the Basic Attack: damage comes from the attacker's weapon or stat block, not from <see cref="damage"/>.</summary>
        public bool usesWeaponDamage;

        /// <summary>Spell damage dice. Count 0 = no damage (a spell that only applies a status).</summary>
        public DiceRoll damage;

        /// <summary>Magic Missile: skips the attack roll entirely and always hits.</summary>
        public bool autoHit;

        /// <summary>The target may roll this save (d10 + VIT or MIND against <see cref="saveDifficulty"/>) to resist.</summary>
        public SaveType saveType;

        /// <summary>The save's difficulty value (DV): the target's stat is the chance, 50% when it equals the DV.</summary>
        public int saveDifficulty = 7;

        /// <summary>On a passed save the target takes half damage (Fireball). If false, a passed save negates the spell (Web).</summary>
        public bool halfDamageOnSave;

        public string statusEffectId; // nullable: empty = none
        [Range(0f, 1f)] public float statusEffectChance = 1f; // only read if statusEffectId is set

        // --- Animation ---
        /// <summary>Custom animation for this skill. Empty clip = fall back to the user's
        /// AnimationSet (basic attacks use the weapon's / set's basic-attack list instead).</summary>
        public AnimationAction animation = new AnimationAction();
    }
}
