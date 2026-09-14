using UnityEngine;
using Game.Data.Enums;

namespace Game.Data.Definitions
{
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "Database/Weapon")]
    public class WeaponDefinition : EquipmentDefinition
    {
        public int atkBonus;
        public DamageType damageType;
    }
}
