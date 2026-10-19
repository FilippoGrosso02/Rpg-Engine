using System.Collections.Generic;
using UnityEngine;
using Game.Data.Enums;

namespace Game.Data.Definitions
{
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "Database/Weapon")]
    public class WeaponDefinition : EquipmentDefinition
    {
        public int atkBonus;
        public DamageType damageType;

        /// <summary>Basic Attack variants for this weapon (played in order as a combo). Empty = use the wielder's AnimationSet.</summary>
        public List<AnimationAction> basicAttackAnimations = new List<AnimationAction>();
    }
}
