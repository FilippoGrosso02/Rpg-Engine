using Game.Audio;
using Game.Data.State;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>A shrine that fully heals the party (and mends acid damage). Limited to <see cref="uses"/> uses per run.</summary>
    public class RestPoint : Interactable
    {
        [Min(1)] public int uses = 1;
        public Renderer glowRenderer;
        public Color spentColor = new Color(0.2f, 0.2f, 0.25f);

        string UseKey => "rest:" + interactionId;

        public int UsesLeft => Mathf.Max(0, uses - WorldFlags.GetCount(UseKey));

        public override bool CanInteract => UsesLeft > 0;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (UsesLeft == 0) ShowSpent();
        }

        public override void Interact()
        {
            if (!CanInteract) return;

            WorldFlags.Increment(UseKey);
            PartyState.HealAll();
            GameAudio.Play(AudioCue.ShrineUse, transform.position);
            InteractionSystem.ShowMessage("The shrine's light restores the party.");
            if (UsesLeft == 0) ShowSpent();
        }

        void ShowSpent()
        {
            if (glowRenderer == null) return;
            var block = new MaterialPropertyBlock();
            glowRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", spentColor);
            glowRenderer.SetPropertyBlock(block);
        }
    }
}
