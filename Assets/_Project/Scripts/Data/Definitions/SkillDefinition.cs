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

        public int resourceCost; // MP — legacy, unread by Duelwright combat (AP replaces it as the combat resource)

        public int power; // legacy, unread by Duelwright combat — see the fields below instead
        public DamageType damageType;

        public string statusEffectId; // nullable: empty = none
        [Range(0f, 1f)] public float statusEffectChance; // only read if statusEffectId is set

        // --- Duelwright fields ---

        /// <summary>AP cost to use this as a Craft. Basic Attack (skillIds[0]) costs 0 AP always.</summary>
        public int apCost;

        /// <summary>True for pure utility/debuff/forced-target cards (e.g. Curse of Dread, Hex
        /// of Weakness, Noble Challenge) — skips ResolveDamage and the automatic on-hit fear
        /// gain entirely; only fearDelta/statusEffectId/bespoke effects apply.</summary>
        public bool skipDamage;

        /// <summary>Flat damage added on top of the die roll (or the whole damage, when
        /// skipDiceRoll is set) — e.g. Blood for Power's "+4".</summary>
        public int bonusPower;

        /// <summary>Damage ignores DEF entirely (e.g. Blood for Power).</summary>
        public bool ignoresDef;

        /// <summary>Damage = effective ATK + bonusPower, no die roll at all (e.g. Blood for Power).</summary>
        public bool skipDiceRoll;

        /// <summary>Extra d6 added to the roll on top of the usual one (e.g. Point-Blank Blast = 1).</summary>
        public int extraDiceCount;

        /// <summary>Natural roll needed to crit; 0 means the default of 6 (e.g. Quick Draw sets 5).</summary>
        public int critThreshold;

        /// <summary>Damage the user takes for using this card (e.g. Blood for Power's self-3).</summary>
        public int selfDamage;

        /// <summary>Extra fear applied to the target beyond the automatic on-hit gain (or, when
        /// skipDamage is set, the card's entire fear effect — negative reduces, e.g. Calming Presence).</summary>
        public int fearDelta;

        /// <summary>True for heal cards (e.g. Mend) — rolls 1d6+healBonus and adds to the
        /// target's HP instead of dealing damage.</summary>
        public bool isHeal;
        public int healBonus;

        /// <summary>True for revive cards: targets a defeated ally (SingleAlly) and brings them back
        /// to <see cref="reviveHp"/> HP with fear and statuses cleared. They act again from the next round.</summary>
        public bool isRevive;
        public int reviveHp = 1;
    }
}
