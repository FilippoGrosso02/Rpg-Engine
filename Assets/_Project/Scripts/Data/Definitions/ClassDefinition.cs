using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Definitions
{
    [CreateAssetMenu(fileName = "NewClass", menuName = "Database/Class")]
    public class ClassDefinition : GameDefinition
    {
        /// <summary>Hit die size (Warrior d8, Ranger d8, Wizard d4). Level-1 hit points are the die at its maximum plus the VIT modifier.</summary>
        public int hitDieSides = 8;

        public List<SkillUnlock> skillUnlocks = new List<SkillUnlock>();

        [Serializable]
        public class SkillUnlock
        {
            public int level;
            public string skillId;
        }
    }
}
