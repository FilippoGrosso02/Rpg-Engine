using System.Collections.Generic;
using UnityEngine;
using Game.Data.Enums;

namespace Game.Data.Definitions
{
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "Database/Weapon")]
    public class WeaponDefinition : EquipmentDefinition
    {
        /// <summary>Damage dice on a hit (sword d8, shortbow d6, dagger d4). The STR modifier is added on top when <see cref="addsStrengthModifier"/> is set.</summary>
        public DiceRoll damage = new DiceRoll(1, 6, 0);

        /// <summary>Ranged weapons attack with DEX; melee weapons attack with STR.</summary>
        public bool isRanged;

        /// <summary>Melee weapons and thrown daggers add the STR modifier to damage; bows do not.</summary>
        public bool addsStrengthModifier = true;

        /// <summary>Metal weapons lose 1 damage (cumulative) each time they hit an acidic monster, until sharpened.</summary>
        public bool isMetal = true;

        public DamageType damageType;

        /// <summary>Basic Attack variants for this weapon (played in order as a combo). Empty = use the wielder's AnimationSet.</summary>
        public List<AnimationAction> basicAttackAnimations = new List<AnimationAction>();
    }
}
