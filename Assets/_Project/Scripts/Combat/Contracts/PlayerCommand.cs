namespace Game.Combat.Contracts
{
    public enum PlayerCommand
    {
        Attack,
        Craft,
        Guard,

        /// <summary>
        /// Not a combat action — dismisses the post-encounter result panel so the runner
        /// knows it's safe to unload the arena. Added beyond the plan's original 3-case
        /// enum because the result panel needs to gate teardown (see Step 6): without it,
        /// there's no way for UI-only code (which only sees Contracts) to tell the runner
        /// "the player has seen the result, you can proceed."
        /// </summary>
        ContinueAfterResult,

        /// <summary>
        /// Clears the selected spell (back to the plain Basic Attack) without ending the turn.
        /// </summary>
        CancelCommand,
        /// <summary>
        /// The player confirmed fleeing (Tab, then Enter). Ends the encounter as an escape: no victory, no defeat.
        /// Whether fleeing should ever fail is game logic; today it always succeeds.
        /// </summary>
        Flee,
        /// <summary>
        /// A UI modal (the flee prompt) is open: the runner must ignore the commit click/Space and free movement until
        /// <see cref="ResumeActions"/>, otherwise the click that dismisses a prompt could also fire an attack.
        /// </summary>
        SuspendActions,
        ResumeActions
    }
}
