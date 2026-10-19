using System;
using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>
    /// One playable combat animation: the clip plus the moment inside it where the effect lands.
    /// A null <see cref="clip"/> means "not authored" — callers fall back to the next source
    /// (skill -> weapon -> actor/enemy set).
    /// </summary>
    [Serializable]
    public class AnimationAction
    {
        public AnimationClip clip;

        /// <summary>Normalized point (0..1) of the clip at which damage/heal/status is applied.</summary>
        [Range(0f, 1f)] public float impact = 0.5f;

        /// <summary>Playback speed multiplier (1 = authored speed).</summary>
        public float speed = 1f;

        public bool HasClip => clip != null;

        /// <summary>Real-time length in seconds after the speed multiplier.</summary>
        public float Duration => clip == null ? 0f : clip.length / Mathf.Max(0.01f, speed);

        /// <summary>Seconds from the start of playback to the impact moment.</summary>
        public float ImpactTime => Duration * impact;
    }
}
