using System.Collections.Generic;
using UnityEngine;
using Game.Data.Enums;

namespace Game.Data.Definitions
{
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "Database/Weapon")]
    public class WeaponDefinition : EquipmentDefinition
    {
        /// <summary>Damage dice on a hit (shortsword d6, longbow d8, greataxe d12). The weapon die only: no ability bonus.</summary>
        public DiceRoll damage = new DiceRoll(1, 6, 0);

        /// <summary>Ranged weapons attack with DEX; melee weapons attack with STR.</summary>
        public bool isRanged;

        /// <summary>Added to the wielder's ATK: light weapons are positive, heavy weapons negative (dagger +2, greataxe -3).</summary>
        public int attackModifier;

        /// <summary>Metal weapons lose 1 damage (cumulative) each time they hit an acidic monster, until sharpened.</summary>
        public bool isMetal = true;

        public DamageType damageType;

        /// <summary>Basic Attack variants for this weapon (played in order as a combo). Empty = use the wielder's AnimationSet.</summary>
        public List<AnimationAction> basicAttackAnimations = new List<AnimationAction>();
    }
}
