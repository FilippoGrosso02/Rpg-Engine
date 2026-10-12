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
        /// Backs out of targeting mode (after Attack/Craft was chosen but before a target
        /// was clicked) without ending the turn. Lets the UI give the player a way out of a
        /// state that previously had no visible exit besides guessing to click an enemy.
        /// </summary>
        CancelCommand
    }
}
