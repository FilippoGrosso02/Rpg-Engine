using System.Collections.Generic;

namespace Game.Combat.Contracts
{
    public class EncounterResult
    {
        public bool victory;

        /// <summary>The party escaped. Neither a victory nor a defeat: the overworld enemy stays and nobody is wiped.</summary>
        public bool fled;

        public List<string> defeatedAllyIds = new List<string>();
        public int xpAwarded;
        public List<string> lootItemIds = new List<string>();
    }
}
