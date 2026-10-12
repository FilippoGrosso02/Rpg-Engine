namespace Game.Combat.Contracts
{
    /// <summary>
    /// Write-only command surface a UI layer uses to tell any combat implementation what
    /// the player chose, without ever depending on the concrete runner type.
    /// </summary>
    public interface IPlayerCommandInput
    {
        void SubmitCommand(PlayerCommand command, string skillId = null);
    }
}
