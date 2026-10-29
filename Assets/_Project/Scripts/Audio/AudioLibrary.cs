using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Audio
{
    /// <summary>
    /// The one asset that maps game cues to sound (Resources/Audio/AudioLibrary). It keeps a row for every <see cref="AudioCue"/> and every
    /// <see cref="AudioMusicState"/>, so changing a sound is: open the asset, drag a clip onto the row. A row with no clip is simply silent.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "Config/Audio Library")]
    public class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public AudioCue cue;

            [FormerlySerializedAs("placeholders")]
            [Tooltip("The sounds for this cue. One is picked at random each time it plays, so a few variations keep repeated hits from sounding identical.")]
            public AudioClip[] clips = new AudioClip[0];

            [FormerlySerializedAs("placeholderVolume")]
            [Range(0f, 1f)] public float volume = 1f;

            [FormerlySerializedAs("placeholderPitchJitter")]
            [Tooltip("Random pitch variation (+/-) so repeated sounds are not identical.")]
            [Range(0f, 0.3f)] public float pitchJitter = 0.05f;
        }

        [Serializable]
        public class MusicEntry
        {
            public AudioMusicState state;

            [Tooltip("A looping track. It fades in when this state becomes active and fades out (paused, not stopped) when another takes over.")]
            public AudioClip clip;

            [Range(0f, 1f)] public float volume = 0.6f;
        }

        [Header("Music (one looping track per game state; a state with no clip fades the music out)")]
        [FormerlySerializedAs("musicPlaceholders")]
        public List<MusicEntry> music = new List<MusicEntry>();

        [Header("One-shots")]
        public List<Entry> cues = new List<Entry>();

        /// <summary>True once any cue or music state has a clip.</summary>
        public bool HasAnySound
        {
            get
            {
                foreach (var entry in cues) if (entry != null && HasClip(entry)) return true;
                foreach (var entry in music) if (entry != null && entry.clip != null) return true;
                return false;
            }
        }

        static bool HasClip(Entry entry)
        {
            if (entry.clips == null) return false;
            foreach (var clip in entry.clips) if (clip != null) return true;
            return false;
        }

        /// <summary>Picks one of the cue's clips (random among the assigned ones). False when it has none.</summary>
        public bool TryGetClip(AudioCue cue, System.Random rng, out AudioClip clip, out float volume, out float pitchJitter)
        {
            clip = null;
            volume = 1f;
            pitchJitter = 0f;
            foreach (var entry in cues)
            {
                if (entry == null || entry.cue != cue || !HasClip(entry)) continue;

                var usable = new List<AudioClip>();
                foreach (var candidate in entry.clips) if (candidate != null) usable.Add(candidate);
                clip = usable[usable.Count == 1 ? 0 : rng.Next(usable.Count)];
                volume = entry.volume;
                pitchJitter = entry.pitchJitter;
                return true;
            }

            return false;
        }

        /// <summary>The track for a music state, if one is assigned.</summary>
        public bool TryGetMusic(AudioMusicState state, out AudioClip clip, out float volume)
        {
            clip = null;
            volume = 1f;
            foreach (var entry in music)
            {
                if (entry == null || entry.state != state || entry.clip == null) continue;
                clip = entry.clip;
                volume = entry.volume;
                return true;
            }

            return false;
        }

        /// <summary>Adds a row for every cue and music state that has none (existing rows and their order are kept). Returns true if anything was added.</summary>
        public bool EnsureAllRows()
        {
            bool changed = false;

            var present = new HashSet<AudioCue>();
            foreach (var entry in cues) if (entry != null) present.Add(entry.cue);
            foreach (AudioCue cue in Enum.GetValues(typeof(AudioCue)))
            {
                if (cue == AudioCue.None || present.Contains(cue)) continue;
                cues.Add(new Entry { cue = cue });
                changed = true;
            }

            var presentStates = new HashSet<AudioMusicState>();
            foreach (var entry in music) if (entry != null) presentStates.Add(entry.state);
            foreach (AudioMusicState state in Enum.GetValues(typeof(AudioMusicState)))
            {
                if (state == AudioMusicState.None || presentStates.Contains(state)) continue;
                music.Add(new MusicEntry { state = state });
                changed = true;
            }

            return changed;
        }

        void OnValidate() => EnsureAllRows();
    }
}
