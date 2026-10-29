namespace Game.Data.Enums
{
    /// <summary>How a class opens a fight from the overworld when its hero leads the party.</summary>
    public enum OverworldAttackStyle
    {
        /// <summary>Walks into reach and strikes. Never gets the drop on anyone.</summary>
        Melee,

        /// <summary>Unseen, points at a far-off enemy and walks up to ambush it. Only works while the enemy has not noticed the party.</summary>
        Stealth,

        /// <summary>Shoots an enemy from afar. An enemy that has not noticed the party is ambushed.</summary>
        Ranged
    }
}
