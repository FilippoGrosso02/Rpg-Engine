using System.Collections.Generic;
using Game.Audio;
using Game.Data.State;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>A chest: opens once, drops its loot into the <see cref="Inventory"/> and stays open (remembered in <see cref="WorldFlags"/>).</summary>
    public class LootContainer : Interactable
    {
        public List<LootItem> items = new List<LootItem>();
        public List<string> weaponIds = new List<string>();
        public List<string> armorIds = new List<string>();
        [Tooltip("Optional renderer to tint once opened.")]
        public Renderer openedRenderer;
        public Color openedColor = new Color(0.25f, 0.2f, 0.12f);

        string Flag => "chest:" + interactionId;

        public bool IsOpened => WorldFlags.Has(Flag);

        public override bool CanInteract => !IsOpened;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (IsOpened) ShowOpened();
        }

        public override void Interact()
        {
            if (IsOpened) return;

            WorldFlags.Set(Flag);
            GameAudio.Play(AudioCue.ChestOpen, transform.position);
            var lines = LootGrant.Apply(items, weaponIds, armorIds);
            InteractionSystem.ShowMessage(lines.Count > 0 ? "Found: " + string.Join(", ", lines) : "The chest is empty.");
            ShowOpened();
        }

        void ShowOpened()
        {
            if (openedRenderer == null) return;
            var block = new MaterialPropertyBlock();
            openedRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", openedColor);
            openedRenderer.SetPropertyBlock(block);
        }
    }
}
