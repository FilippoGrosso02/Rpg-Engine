using System;

namespace Game.Combat.Contracts
{
    /// <summary>
    /// Optional capability of an <see cref="IEncounterRunner"/>: run the fight on the level that is already loaded, around
    /// <see cref="InPlaceEncounterContext"/>, instead of in a separate arena. Callers fall back to
    /// <see cref="IEncounterRunner.StartEncounter"/> when the runner does not offer it.
    /// </summary>
    public interface IInPlaceEncounterRunner : IEncounterRunner
    {
        void StartEncounterInPlace(EncounterDefinition encounter, InPlaceEncounterContext context, Action<EncounterResult> onComplete);
    }
}
