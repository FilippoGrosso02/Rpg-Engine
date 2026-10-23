using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>
    /// A monster under the d12 OSR rules. Its stat block is flat: attack is a bonus added to the d12 roll (no stat split),
    /// morale is a 2d6 target number, and it has one damage roll.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemy", menuName = "Database/Enemy")]
    public class EnemyDefinition : GameDefinition
    {
        public Sprite sprite;
        public GameObject battleModelPrefab;
        public AnimationSetDefinition animationSet;

        public int hp = 5;
        public int armorClass = 9;

        /// <summary>Added to the d12 attack roll; the attack hits when d12 + this >= the target's AC.</summary>
        public int attackBonus = 5;

        /// <summary>Damage on a hit (goblin d6, ogre 2d6, slime d6).</summary>
        public DiceRoll damage = new DiceRoll(1, 6, 0);

        [Header("Morale")]
        [Tooltip("2d6 target number. The monster flees when a 2d6 roll is higher than this. 0 = never checks (mindless).")]
        public int morale = 7;

        [Tooltip("Added to the d12 initiative roll each round.")]
        public int initiative = 6;

        [Tooltip("Added to the d12 when saving against spells (webs, fire...). Monsters use one flat number.")]
        public int saveBonus = 5;

        [Header("Special")]
        [Tooltip("Each hit corrodes the target's armor by 1 AC until repaired (slime acid).")]
        public bool corrodesArmor;

        [Tooltip("Metal weapons that hit this monster lose 1 damage (cumulative) until sharpened (slime).")]
        public bool corrodesWeapons;

        public bool immuneToCharm;
        public bool immuneToFear;
        public bool immuneToSleep;

        /// <summary>How far (in world units) this monster can move on its own turn.</summary>
        public int moveRange = 6;

        /// <summary>Attack range expressed as a multiplier of ActionResolver.RangeUnit — 1 is the melee baseline.</summary>
        public int attackRangeMultiplier = 1;

        /// <summary>Short flavor label for the HUD (e.g. "Grunt") — purely cosmetic.</summary>
        public string roleLabel;

        /// <summary>Basic Attack first; monsters attack with their own stat block, so this is normally one entry.</summary>
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
