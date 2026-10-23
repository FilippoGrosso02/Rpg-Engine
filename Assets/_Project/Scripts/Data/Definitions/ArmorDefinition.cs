using UnityEngine;

namespace Game.Data.Definitions
{
    [CreateAssetMenu(fileName = "NewArmor", menuName = "Database/Armor")]
    public class ArmorDefinition : EquipmentDefinition
    {
        /// <summary>Added to armor class (leather 4, chain mail 7, shield 1). Acid can corrode it by 1 per hit until repaired.</summary>
        public int acBonus;
    }
}
