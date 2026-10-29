using UnityEngine;

namespace Game.Combat.Contracts
{
    /// <summary>
    /// Where an in-place (on-map) encounter happens. The runner builds both formations around these two points on the
    /// existing level instead of loading an arena scene.
    /// </summary>
    public class InPlaceEncounterContext
    {
        /// <summary>Where the party leader stands (the overworld player).</summary>
        public Vector3 partyPosition;

        /// <summary>Where the enemy group stands (the overworld enemy that made contact).</summary>
        public Vector3 enemyPosition;

        /// <summary>
        /// Where each party member already stands, in party order (leader first); null entries (or a null array) are placed by the
        /// runner's own formation. Likewise <see cref="enemyPositions"/>, in the encounter's enemy order.
        /// </summary>
        public Vector3?[] partyPositions;

        public Vector3?[] enemyPositions;

        /// <summary>Direction the party faces when both groups are on top of each other and no direction can be derived.</summary>
        public Vector3 fallbackForward = Vector3.forward;

        /// <summary>Who was caught off guard, if anyone (the party striking an enemy that had not noticed them, for instance).</summary>
        public EncounterAdvantage advantage = EncounterAdvantage.None;

        /// <summary>Pause after the encounter starts, so the camera can blend in before the first turn.</summary>
        public float introSeconds = 0.8f;
    }
}
