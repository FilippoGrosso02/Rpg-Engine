using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Definitions
{
    // Not a GameDefinition: this is a single boot-config asset, not an ID-addressable
    // entity that other systems look up by string id.
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Database/Game Config")]
    public class GameConfigDefinition : ScriptableObject
    {
        public List<string> startingActorIds = new List<string>();
        public int startingGold;
        public string startingSceneName;
    }
}
