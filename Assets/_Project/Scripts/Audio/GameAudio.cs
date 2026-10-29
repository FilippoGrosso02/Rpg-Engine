using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// The only door to sound for gameplay code: <c>GameAudio.Play(AudioCue.ChestOpen)</c>, <c>GameAudio.SetMusicState(...)</c>,
    /// <c>GameAudio.SetBusVolume(...)</c>. Which clip plays for a cue is decided in the <see cref="AudioLibrary"/> asset, so gameplay never names a file.
    /// Everything is a safe no-op for a cue with no clip, or with no library at all.
    /// </summary>
    public static class GameAudio
    {
        public const string LibraryResourcePath = "Audio/AudioLibrary";

        static AudioLibrary s_library;
        static bool s_libraryLoaded;
        static readonly Dictionary<AudioBus, float> BusVolumes = new Dictionary<AudioBus, float>();
        static readonly System.Random Rng = new System.Random();

        /// <summary>Raised for every <see cref="Play(AudioCue)"/> request, whether or not the cue has a clip (tests and debugging).</summary>
        public static event Action<AudioCue> CueRequested;

        /// <summary>Raised when a cue is actually served by a clip, with the clip chosen.</summary>
        public static event Action<AudioCue, AudioClip> ClipPlayed;

        /// <summary>Raised when a music state is served by a track, with the clip.</summary>
        public static event Action<AudioMusicState, AudioClip> MusicRequested;

        public static AudioMusicState MusicState { get; private set; }

        /// <summary>The library in use. Loaded from Resources on first use; tests can assign their own (or null to disable audio).</summary>
        public static AudioLibrary Library
        {
            get
            {
                if (!s_libraryLoaded)
                {
                    s_library = Resources.Load<AudioLibrary>(LibraryResourcePath);
                    s_libraryLoaded = true;
                }

                return s_library;
            }
            set
            {
                s_library = value;
                s_libraryLoaded = true;
            }
        }

        /// <summary>True when there is a library with at least one sound in it.</summary>
        public static bool Enabled => Library != null && Library.HasAnySound;

        /// <summary>
        /// Plays a cue. Sounds are 2D (a JRPG mix has no use for attenuation), so <paramref name="position"/> is accepted for call sites that have
        /// one but does not change how it sounds; it is the hook for positional sound if that is ever wanted.
        /// </summary>
        public static void Play(AudioCue cue, Vector3 position)
        {
            CueRequested?.Invoke(cue);

            var library = Library;
            if (library == null || !library.TryGetClip(cue, Rng, out var clip, out float volume, out float jitter)) return;

            ClipPlayed?.Invoke(cue, clip);
            if (Application.isPlaying) SfxPlayer.Play(clip, volume * GetBusVolume(AudioCues.BusFor(cue)), jitter);
        }

        public static void Play(AudioCue cue) => Play(cue, Vector3.zero);

        public static void SetMusicState(AudioMusicState state)
        {
            MusicState = state;
            var library = Library;
            if (library == null) return;

            if (library.TryGetMusic(state, out var track, out _)) MusicRequested?.Invoke(state, track);
            MusicPlayer.SetState(library, state);
        }

        /// <summary>Linear volume 0-1 for a mixer bus (Music scales the music tracks; UI and SFX scale their cues). Master is AudioListener.volume.</summary>
        public static void SetBusVolume(AudioBus bus, float volume) => BusVolumes[bus] = Mathf.Clamp01(volume);

        public static float GetBusVolume(AudioBus bus) => BusVolumes.TryGetValue(bus, out var v) ? v : 1f;

        /// <summary>Stops the music and forgets the state (a new game).</summary>
        public static void StopMusic()
        {
            MusicState = AudioMusicState.None;
            MusicPlayer.Stop();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            MusicState = AudioMusicState.None;
            BusVolumes.Clear();
            CueRequested = null;
            ClipPlayed = null;
            MusicRequested = null;
            MusicPlayer.ResetStatics();
            s_library = null;
            s_libraryLoaded = false;
        }
    }
}
