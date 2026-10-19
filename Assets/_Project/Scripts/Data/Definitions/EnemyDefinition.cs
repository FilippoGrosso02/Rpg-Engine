using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Definitions
{
    [CreateAssetMenu(fileName = "NewEnemy", menuName = "Database/Enemy")]
    public class EnemyDefinition : GameDefinition
    {
        public Sprite sprite;
        public GameObject battleModelPrefab;
        public AnimationSetDefinition animationSet;

        public int hp;
        public int atk;
        public int def;
        public int speed;

        /// <summary>How far (in world units) this monster can move on its own turn.</summary>
        public int moveRange = 6;

        /// <summary>Attack range expressed as a multiplier of ActionResolver.RangeUnit — 1 is
        /// the melee baseline (must close to melee distance to attack); higher values give a
        /// longer ranged attack distance instead of the old unlimited-range Ranged trait.</summary>
        public int attackRangeMultiplier = 1;

        /// <summary>Horror-type monsters inflict +1 additional fear per hit, on top of the normal gain.</summary>
        public bool isHorror;

        /// <summary>Short flavor label for the HUD (e.g. "Melee hard-hitter") — purely cosmetic.</summary>
        public string roleLabel;

        public List<string> skillIds = new List<string>();

        public string aiTag; // placeholder, no AI logic yet

        public List<LootEntry> lootTable = new List<LootEntry>();

        [Serializable]
        public class LootEntry
        {
            public string itemId;
            [Range(0f, 1f)] public float dropChance;
        }
    }
}
