using System.Collections.Generic;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// The music: plain Unity audio, one looping source per music state. Every music state with a clip gets its own looping source;
    /// the active state's source fades in, the others fade out and PAUSE (not stop), so returning to a state resumes where it left off
    /// (back to the dungeon theme after a fight carries on mid-song). Fades run on unscaled time so the pause menu does not freeze them.
    /// </summary>
    public static class MusicPlayer
    {
        public const float FadeSeconds = 1.2f;

        /// <summary>One step of a linear fade of <paramref name="gain"/> towards 1 (active) or 0 (inactive) over <see cref="FadeSeconds"/>.</summary>
        public static float FadeStep(float gain, bool active, float deltaSeconds)
        {
            return Mathf.MoveTowards(gain, active ? 1f : 0f, deltaSeconds / FadeSeconds);
        }

        sealed class Track
        {
            public AudioSource source;
            public float volume;
            public float gain;
        }

        sealed class Driver : MonoBehaviour
        {
            void Update() => Tick(Time.unscaledDeltaTime);
        }

        static GameObject s_root;
        static readonly Dictionary<AudioMusicState, Track> Tracks = new Dictionary<AudioMusicState, Track>();
        static AudioMusicState s_active;

        /// <summary>Makes <paramref name="state"/> the audible track (AudioMusicState.None or a state with no clip fades everything out).</summary>
        internal static void SetState(AudioLibrary library, AudioMusicState state)
        {
            s_active = state;
            if (!Application.isPlaying || library == null) return;
            if (!library.TryGetMusic(state, out var clip, out float volume)) return;

            if (s_root == null) Build();
            if (!Tracks.TryGetValue(state, out var track) || track.source == null)
            {
                var source = s_root.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.volume = 0f;
                track = new Track { source = source };
                Tracks[state] = track;
            }

            if (track.source.clip != clip) track.source.clip = clip;
            track.volume = volume;
            if (!track.source.isPlaying)
            {
                // A paused source reports not-playing; Play() would restart it, so resume instead when it has progressed.
                if (track.source.time > 0f) track.source.UnPause();
                else track.source.Play();
            }
        }

        internal static void Stop()
        {
            s_active = AudioMusicState.None;
            foreach (var track in Tracks.Values)
            {
                if (track.source == null) continue;
                track.source.Stop();
                track.gain = 0f;
            }
        }

        static void Tick(float deltaSeconds)
        {
            float bus = GameAudio.GetBusVolume(AudioBus.Music);
            foreach (var pair in Tracks)
            {
                var track = pair.Value;
                if (track.source == null) continue;

                bool active = pair.Key == s_active;
                track.gain = FadeStep(track.gain, active, deltaSeconds);
                track.source.volume = track.gain * track.volume * bus;

                if (active && !track.source.isPlaying) track.source.UnPause();
                else if (!active && track.gain <= 0f && track.source.isPlaying) track.source.Pause();
            }
        }

        static void Build()
        {
            s_root = new GameObject("[GameAudio Music]");
            Object.DontDestroyOnLoad(s_root);
            s_root.AddComponent<Driver>();
            Tracks.Clear();
        }

        /// <summary>Drops everything when Play mode (re)starts: the scene objects are gone but statics may survive.</summary>
        internal static void ResetStatics()
        {
            Tracks.Clear();
            s_root = null;
            s_active = AudioMusicState.None;
        }
    }
}
