using System.Collections.Generic;
using Game.Audio;
using Game.Data.Registry;
using Game.Data.State;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>
    /// A door that opens when the party carries any one of <see cref="acceptedItemIds"/> (several keys can fit, which is what lets
    /// both branches of the dungeon lead here). The key is used up. Once open the whole object is hidden and stays that way.
    /// </summary>
    public class LockedDoor : Interactable
    {
        public List<string> acceptedItemIds = new List<string>();
        public bool consumeKey = true;

        string Flag => "door:" + interactionId;

        public bool IsUnlocked => WorldFlags.Has(Flag);

        public override bool CanInteract => !IsUnlocked;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (IsUnlocked) gameObject.SetActive(false);
        }

        /// <summary>The first accepted key the party is carrying, or null.</summary>
        public static string FindKey(IEnumerable<string> acceptedItemIds)
        {
            if (acceptedItemIds == null) return null;
            foreach (var id in acceptedItemIds)
                if (Inventory.GetItemCount(id) > 0) return id;
            return null;
        }

        public override void Interact()
        {
            if (IsUnlocked) return;

            string key = FindKey(acceptedItemIds);
            if (key == null)
            {
                GameAudio.Play(AudioCue.DoorLocked, transform.position);
                InteractionSystem.ShowMessage("Locked. You need a key.");
                return;
            }

            if (consumeKey) Inventory.RemoveItem(key);
            WorldFlags.Set(Flag);
            GameAudio.Play(AudioCue.DoorUnlock, transform.position);
            var def = DatabaseRegistry.GetItem(key);
            InteractionSystem.ShowMessage("You unlock the door with the " + (def != null ? def.displayName : key) + ".");
            gameObject.SetActive(false);
        }
    }
}
