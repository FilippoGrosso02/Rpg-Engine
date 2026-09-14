using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Definitions
{
    [CreateAssetMenu(fileName = "NewClass", menuName = "Database/Class")]
    public class ClassDefinition : GameDefinition
    {
        public int hpPerLevel;
        public int mpPerLevel;
        public int atkPerLevel;
        public int defPerLevel;
        public int speedPerLevel;

        public List<SkillUnlock> skillUnlocks = new List<SkillUnlock>();

        [Serializable]
        public class SkillUnlock
        {
            public int level;
            public string skillId;
        }
    }
}
