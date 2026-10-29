namespace Game.Combat.Contracts
{
    /// <summary>Who, if anyone, was caught off guard when the fight began. The side with the advantage gets a free opening round.</summary>
    public enum EncounterAdvantage
    {
        /// <summary>Both sides knew: the normal initiative order from the start.</summary>
        None,

        /// <summary>The enemies were unaware (the party struck first): every hero acts before any enemy, once, then the normal order.</summary>
        Party,

        /// <summary>The party was surprised: every enemy acts before any hero, once, then the normal order.</summary>
        Enemies
    }
}
