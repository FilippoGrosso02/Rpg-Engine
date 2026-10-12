using System;

namespace Game.Combat.Contracts
{
    /// <summary>
    /// Entry point for starting a battle. Each concrete combat implementation (e.g.
    /// TrailsBattle3D, and later an in-place/on-map variant) provides one of these; callers
    /// only ever depend on this interface, never on a concrete runner type.
    /// </summary>
    public interface IEncounterRunner
    {
        /// <summary>
        /// When true, the runner holds off unloading the arena / invoking onComplete's
        /// side effects until something calls SubmitCommand(PlayerCommand.ContinueAfterResult)
        /// (see IPlayerCommandInput) — set by whoever wires up a HUD, so its result panel is
        /// actually readable before the arena disappears. Left false by default so bare/
        /// headless use (tests, no-UI callers) never waits on a "Continue" click that will
        /// never come.
        /// </summary>
        bool WaitForResultAcknowledgementBeforeUnload { get; set; }

        void StartEncounter(EncounterDefinition encounter, Action<EncounterResult> onComplete);
    }
}
