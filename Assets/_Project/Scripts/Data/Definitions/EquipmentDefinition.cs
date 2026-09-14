using UnityEngine;
using Game.Data.Enums;

namespace Game.Data.Definitions
{
    public abstract class EquipmentDefinition : GameDefinition
    {
        [TextArea] public string description;
        public Sprite icon;
        public EquipmentSlot equipSlot;
    }
}
