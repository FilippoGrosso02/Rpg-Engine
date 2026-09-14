using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Definitions
{
    [CreateAssetMenu(fileName = "NewEnemy", menuName = "Database/Enemy")]
    public class EnemyDefinition : GameDefinition
    {
        public Sprite sprite;

        public int hp;
        public int atk;
        public int def;
        public int speed;

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
