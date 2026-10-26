namespace Game.Data.Enums
{
    /// <summary>
    /// Which save resists an effect. Heroes roll d10 + their VIT (physical: webs, fire) or MIND (mental) score
    /// against the effect's DV; monsters use a single flat saveBonus stat for either.
    /// </summary>
    public enum SaveType
    {
        None,
        Vitality,
        Mind
    }
}
