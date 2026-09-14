using UnityEngine;

namespace Game.Data.Definitions
{
    [CreateAssetMenu(fileName = "NewArmor", menuName = "Database/Armor")]
    public class ArmorDefinition : EquipmentDefinition
    {
        public int defBonus;
    }
}
