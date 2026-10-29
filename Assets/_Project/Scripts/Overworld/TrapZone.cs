using Game.Audio;
using Game.Data.State;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>
    /// A trigger volume that hurts the whole party once when the player walks in (never below 1 HP). Needs a Collider set to
    /// "Is Trigger"; the player's CharacterController triggers it.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TrapZone : MonoBehaviour
    {
        public string trapId;
        public int damage = 2;
        public string message = "A trap springs! The party takes damage.";

        string Flag => "trap:" + trapId;

        void OnTriggerEnter(Collider other)
        {
            if (WorldFlags.Has(Flag) || !other.CompareTag("Player")) return;

            WorldFlags.Set(Flag);
            PartyCare.DamageParty(damage);
            GameAudio.Play(AudioCue.TrapTrigger, transform.position);
            InteractionSystem.ShowMessage(message + " (-" + damage + " HP each)");
        }
    }
}
