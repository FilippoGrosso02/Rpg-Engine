using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Plays the one-shot clips from the <see cref="AudioLibrary"/> through plain Unity audio: a small round-robin pool of 2D sources,
    /// so several sounds (a swing, an impact, a footstep) can overlap without cutting each other off.
    /// </summary>
    static class SfxPlayer
    {
        const int PoolSize = 8;

        static GameObject s_root;
        static AudioSource[] s_sources;
        static int s_next;

        public static void Play(AudioClip clip, float volume, float pitchJitter)
        {
            if (clip == null || volume <= 0f) return;
            if (s_root == null) Build();

            var source = s_sources[s_next];
            s_next = (s_next + 1) % s_sources.Length;

            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.Play();
        }

        static void Build()
        {
            s_root = new GameObject("[GameAudio Sfx]");
            Object.DontDestroyOnLoad(s_root);
            s_sources = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var source = s_root.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                s_sources[i] = source;
            }

            s_next = 0;
        }
    }
}
