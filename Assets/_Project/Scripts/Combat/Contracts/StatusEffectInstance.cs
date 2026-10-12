namespace Game.Combat.Contracts
{
    /// <summary>
    /// One active status on a CombatantRuntime for the duration of a single encounter.
    /// Points at a StatusEffectDefinition by id rather than holding a direct reference,
    /// matching how every other runtime/definition link in the database works.
    /// </summary>
    public class StatusEffectInstance
    {
        public string statusEffectId;
        public int remainingDuration;
    }
}
