using System.Collections.Generic;

namespace Game.Combat.Contracts
{
    public class EncounterResult
    {
        public bool victory;
        public List<string> defeatedAllyIds = new List<string>();
        public int xpAwarded;
        public List<string> lootItemIds = new List<string>();
    }
}
