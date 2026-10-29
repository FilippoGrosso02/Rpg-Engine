using System.Collections.Generic;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>
    /// Something the player can use by standing close and pressing E (chest, door, shrine). <see cref="InteractionSystem"/> picks the
    /// nearest available one and calls <see cref="Interact"/>. Instances register themselves while enabled, so everything under the
    /// deactivated OverworldRoot drops out during a fight on its own.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        [Tooltip("Stable unique id within the dungeon; used to remember what the player already did here.")]
        public string interactionId;
        [Tooltip("Shown as \"E - <prompt>\" when the player is in range.")]
        public string prompt = "Open";
        public float range = 2.2f;

        /// <summary>False once it has been used up (opened chest, spent shrine): it then shows no prompt.</summary>
        public virtual bool CanInteract => true;

        public abstract void Interact();

        protected virtual void OnEnable() => All.Add(this);

        protected virtual void OnDisable() => All.Remove(this);

        /// <summary>Flat (XZ) distance from <paramref name="point"/> to this object.</summary>
        public float DistanceTo(Vector3 point)
        {
            Vector3 d = transform.position - point;
            d.y = 0f;
            return d.magnitude;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
