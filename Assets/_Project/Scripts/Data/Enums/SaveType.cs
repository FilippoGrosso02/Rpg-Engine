namespace Game.Data.Enums
{
    /// <summary>
    /// Which save resists an effect. Heroes save with their VIT (physical: webs, fire) or MIND (mental) score;
    /// monsters use a single flat saveBonus for either.
    /// </summary>
    public enum SaveType
    {
        None,
        Vitality,
        Mind
    }
}
