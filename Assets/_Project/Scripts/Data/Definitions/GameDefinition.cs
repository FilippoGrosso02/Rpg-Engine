using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>
    /// Base type for every ID-addressable data asset in the database.
    /// <see cref="id"/> is a stable, human-readable string (e.g. "skill_fireball") that
    /// every other system (combat, events, dialogue) uses to reference this asset —
    /// never the Unity asset GUID, which isn't stable across reimports/moves and isn't
    /// legible in authored content.
    /// </summary>
    public abstract class GameDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
    }
}
