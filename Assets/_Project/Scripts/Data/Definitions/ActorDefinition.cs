using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Definitions
{
    [CreateAssetMenu(fileName = "NewActor", menuName = "Database/Actor")]
    public class ActorDefinition : GameDefinition
    {
        public Sprite portrait;
        public GameObject battleModelPrefab;

        public string classId;

        public int baseHp;
        public int baseMp;
        public int baseAtk;
        public int baseDef;
        public int baseSpeed;

        /// <summary>How far (in world units) this character can move on its own turn.</summary>
        public int moveRange = 6;

        /// <summary>Attack range expressed as a multiplier of ActionResolver.RangeUnit — 1 is
        /// the melee baseline (must close to melee distance to attack); higher values give a
        /// longer ranged attack distance instead of the old unlimited-range Ranged trait.</summary>
        public int attackRangeMultiplier = 1;

        /// <summary>Short flavor label for the HUD (e.g. "Thief w/ Gun") — purely cosmetic.</summary>
        public string roleLabel;

        public List<string> startingSkillIds = new List<string>();
        public string startingWeaponId;
        public string startingArmorId;
    }
}
