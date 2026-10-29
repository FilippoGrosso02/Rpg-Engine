using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat.Contracts
{
    public class EncounterResult
    {
        public bool victory;

        /// <summary>The party escaped. Neither a victory nor a defeat: the overworld enemy stays and nobody is wiped.</summary>
        public bool fled;

        /// <summary>In-place fights only: where the party leader ended up, so the overworld player can continue from there.</summary>
        public Vector3? leaderEndPosition;

        public List<string> defeatedAllyIds = new List<string>();
        public int xpAwarded;
        public List<string> lootItemIds = new List<string>();
    }
}
