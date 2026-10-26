using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>
    /// A monster under the d10 OSR rules. Its stat block is flat: ATK, DEF and the save stat are numbers on the 2-12 scale
    /// (no STR/DEX split), morale is a 2d6 target number, and it has one damage roll.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemy", menuName = "Database/Enemy")]
    public class EnemyDefinition : GameDefinition
    {
        public Sprite sprite;
        public GameObject battleModelPrefab;
        public AnimationSetDefinition animationSet;

        public int hp = 5;
        [Tooltip("DEF on the 2-12 scale: grunts 2-4, soldiers and brutes 5-7, elites 8-10, bosses 11-12.")]
        public int armorClass = 5;

        /// <summary>ATK on the 2-12 scale; the attack hits when d10 + this - the target's DEF >= 6.</summary>
        public int attackBonus = 7;

        /// <summary>Damage on a hit (goblin d6, ogre 2d6, slime d6).</summary>
        public DiceRoll damage = new DiceRoll(1, 6, 0);

        [Header("Morale")]
        [Tooltip("2d6 target number. The monster flees when a 2d6 roll is higher than this. 0 = never checks (mindless).")]
        public int morale = 7;

        [Tooltip("DEX on the 2-12 scale: its minor modifier is added to the d10 initiative roll, and it breaks initiative ties.")]
        public int dexterity = 7;

        [Tooltip("Stat (2-12) rolled with d10 against a spell's DV when saving. Monsters use one flat number for VIT and MIND.")]
        public int saveBonus = 7;

        [Header("Special")]
        [Tooltip("Each hit corrodes the target's armor by 1 DEF until repaired (slime acid).")]
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
