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

        public bool stackable;
        public int maxStack = 1;
    }
}
