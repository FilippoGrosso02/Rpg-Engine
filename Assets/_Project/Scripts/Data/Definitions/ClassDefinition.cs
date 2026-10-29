using System;
using System.Collections.Generic;
using Game.Data.Enums;
using UnityEngine;

namespace Game.Data.Definitions
{
    [CreateAssetMenu(fileName = "NewClass", menuName = "Database/Class")]
    public class ClassDefinition : GameDefinition
    {
        /// <summary>Hit die size (fighter d8, cleric d6, thief d6, caster d4). Level-1 hit points are the die at its maximum plus the VIT minor modifier.</summary>
        public int hitDieSides = 8;

        /// <summary>Evade classes (thieves, rangers) add their DEX minor modifier to DEF; every other class gets no DEX bonus.</summary>
        public bool evadesWithDex;

        [Tooltip("How this class opens a fight from the overworld when its hero leads the party (thieves creep up unseen, rangers shoot).")]
        public OverworldAttackStyle overworldAttack = OverworldAttackStyle.Melee;

        public List<SkillUnlock> skillUnlocks = new List<SkillUnlock>();

        [Serializable]
        public class SkillUnlock
        {
            public int level;
            public string skillId;
        }
    }
}
