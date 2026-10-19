using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>
    /// A reusable bundle of combat animations for one "style" of combatant (e.g. a swordsman,
    /// a gunslinger, a shambling horror). Actors and enemies point at one; weapons and skills
    /// can override individual attack clips. Any clip left empty simply doesn't play.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAnimationSet", menuName = "Database/Animation Set")]
    public class AnimationSetDefinition : ScriptableObject
    {
        [Header("Locomotion (use the InPlace clips)")]
        public AnimationClip idle;
        public AnimationClip run;

        [Header("Reactions")]
        public AnimationClip guard;
        [Tooltip("One is picked at random per hit.")]
        public List<AnimationClip> hits = new List<AnimationClip>();
        public AnimationClip defeated;

        [Header("Attacks")]
        [Tooltip("Basic Attack variants, used when the equipped weapon doesn't supply its own. Played in order as a combo.")]
        public List<AnimationAction> basicAttacks = new List<AnimationAction>();
        [Tooltip("Fallback for skills that don't author their own animation.")]
        public AnimationAction skillDefault = new AnimationAction();
    }
}
