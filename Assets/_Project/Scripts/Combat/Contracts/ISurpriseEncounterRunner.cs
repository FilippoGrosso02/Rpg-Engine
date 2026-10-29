namespace Game.Combat.Contracts
{
    /// <summary>
    /// Optional capability of an <see cref="IEncounterRunner"/>: start the fight with one side having caught the other off guard.
    /// Set before calling <see cref="IEncounterRunner.StartEncounter"/>. (In-place fights carry this in their context instead.)
    /// </summary>
    public interface ISurpriseEncounterRunner
    {
        EncounterAdvantage Advantage { get; set; }
    }
}
