using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Definitions
{
    [CreateAssetMenu(fileName = "NewActor", menuName = "Database/Actor")]
    public class ActorDefinition : GameDefinition
    {
        public Sprite portrait;

        public string classId;

        public int baseHp;
        public int baseMp;
        public int baseAtk;
        public int baseDef;
        public int baseSpeed;

        public List<string> startingSkillIds = new List<string>();
        public string startingWeaponId;
        public string startingArmorId;
    }
}
