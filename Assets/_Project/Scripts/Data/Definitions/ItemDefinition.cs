using UnityEngine;
using Game.Data.Enums;

namespace Game.Data.Definitions
{
    [CreateAssetMenu(fileName = "NewItem", menuName = "Database/Item")]
    public class ItemDefinition : GameDefinition
    {
        [TextArea] public string description;
        public Sprite icon;

        public ItemType itemType;

        public string effectSkillId; // nullable: e.g. a potion points at a Skill that heals

        [Tooltip("> 0: using this item in the menu heals the most wounded hero by this many HP.")]
        public int healAmount;

        public bool stackable;
        public int maxStack = 1;
    }
}
