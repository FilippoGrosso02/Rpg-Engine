using System;
using System.Collections.Generic;
using Game.Combat.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace Game.Audio.Tests.EditMode
{
    public class AudioTests
    {
        AudioLibrary _library;

        [SetUp]
        public void SetUp()
        {
            _library = ScriptableObject.CreateInstance<AudioLibrary>();
            _library.EnsureAllRows();
            GameAudio.Library = _library;
        }

        [TearDown]
        public void TearDown()
        {
            GameAudio.Library = null;
            GameAudio.StopMusic();
            UnityEngine.Object.DestroyImmediate(_library);
        }

        [Test]
        public void Library_HasARowForEveryCue_AndDoesNotDuplicateOnRepeat()
        {
            int expected = System.Enum.GetValues(typeof(AudioCue)).Length - 1; // minus None

            Assert.AreEqual(expected, _library.cues.Count);
            Assert.IsFalse(_library.EnsureAllRows(), "a second call has nothing to add");
            Assert.AreEqual(expected, _library.cues.Count);
        }

        [Test]
        public void EmptyLibrary_IsDisabled_AndResolvesNothing()
        {
            Assert.IsFalse(_library.HasAnySound);
            Assert.IsFalse(GameAudio.Enabled);
            Assert.IsFalse(_library.TryGetClip(AudioCue.ChestOpen, new System.Random(1), out _, out _, out _));
        }

        [Test]
        public void Play_WithNoClips_IsASilentNoOp_ButStillReportsTheRequest()
        {
            var seen = new List<AudioCue>();
            GameAudio.CueRequested += seen.Add;
            try
            {
                Assert.DoesNotThrow(() => GameAudio.Play(AudioCue.ChestOpen, Vector3.one));
                Assert.DoesNotThrow(() => GameAudio.Play(AudioCue.UiConfirm));
            }
            finally
            {
                GameAudio.CueRequested -= seen.Add;
            }

            CollectionAssert.AreEqual(new[] { AudioCue.ChestOpen, AudioCue.UiConfirm }, seen);
        }

        [Test]
        public void Play_WithNoLibraryAtAll_DoesNotThrow()
        {
            GameAudio.Library = null;
            Assert.DoesNotThrow(() => GameAudio.Play(AudioCue.AttackHit));
            Assert.IsFalse(GameAudio.Enabled);
        }

        [Test]
        public void MusicState_IsRemembered_EvenWhileDisabled()
        {
            GameAudio.SetMusicState(AudioMusicState.Battle);
            Assert.AreEqual(AudioMusicState.Battle, GameAudio.MusicState);

            GameAudio.StopMusic();
            Assert.AreEqual(AudioMusicState.None, GameAudio.MusicState);
        }

        [Test]
        public void BusVolume_IsClampedAndRemembered_EvenWhileDisabled()
        {
            GameAudio.SetBusVolume(AudioBus.Music, 1.7f);
            GameAudio.SetBusVolume(AudioBus.Sfx, -3f);
            GameAudio.SetBusVolume(AudioBus.Ui, 0.25f);

            Assert.AreEqual(1f, GameAudio.GetBusVolume(AudioBus.Music));
            Assert.AreEqual(0f, GameAudio.GetBusVolume(AudioBus.Sfx));
            Assert.AreEqual(0.25f, GameAudio.GetBusVolume(AudioBus.Ui));
            Assert.AreEqual(1f, GameAudio.GetBusVolume(AudioBus.Master), "untouched buses default to full volume");
        }

        [Test]
        public void CombatBridge_MapsRollsAndResultsToCues()
        {
            Assert.AreEqual(AudioCue.AttackHit, CombatAudioBridge.CueFor(new DamageRollInfo { hit = true }));
            Assert.AreEqual(AudioCue.AttackMiss, CombatAudioBridge.CueFor(new DamageRollInfo { hit = false }));
            Assert.AreEqual(AudioCue.BattleWin, CombatAudioBridge.CueFor(new EncounterResult { victory = true }));
            Assert.AreEqual(AudioCue.BattleLose, CombatAudioBridge.CueFor(new EncounterResult { victory = false }));
            Assert.AreEqual(AudioCue.SpellCast, CombatAudioBridge.CueFor(new DamageRollInfo { hit = true, spellName = "Fireball" }));
            Assert.AreEqual(AudioCue.SpellCast, CombatAudioBridge.CueFor(new DamageRollInfo { hit = false, spellName = "Web" }));
        }

        [Test]
        public void Cue_PlaysItsClip_AndSkipsCuesWithNone()
        {
            var clip = AudioClip.Create("test", 100, 1, 44100, false);
            var entry = _library.cues.Find(e => e.cue == AudioCue.ChestOpen);
            entry.clips = new[] { clip };

            var played = new List<AudioClip>();
            Action<AudioCue, AudioClip> handler = (cue, c) => played.Add(c);
            GameAudio.ClipPlayed += handler;
            try
            {
                GameAudio.Play(AudioCue.ChestOpen);
                GameAudio.Play(AudioCue.UiConfirm); // no placeholder: nothing
            }
            finally
            {
                GameAudio.ClipPlayed -= handler;
                UnityEngine.Object.DestroyImmediate(clip);
            }

            Assert.AreEqual(1, played.Count);
        }

        [Test]
        public void Cue_PicksAmongSeveralClips_AndIgnoresEmptySlots()
        {
            var a = AudioClip.Create("a", 100, 1, 44100, false);
            var b = AudioClip.Create("b", 100, 1, 44100, false);
            var entry = _library.cues.Find(e => e.cue == AudioCue.AttackHit);
            entry.clips = new[] { a, null, b };
            entry.volume = 0.4f;

            var seen = new HashSet<AudioClip>();
            var rng = new System.Random(5);
            for (int i = 0; i < 60; i++)
            {
                Assert.IsTrue(_library.TryGetClip(AudioCue.AttackHit, rng, out var clip, out float volume, out _));
                Assert.AreEqual(0.4f, volume);
                Assert.IsNotNull(clip);
                seen.Add(clip);
            }

            CollectionAssert.AreEquivalent(new[] { a, b }, seen);
            Assert.IsTrue(_library.HasAnySound);
            UnityEngine.Object.DestroyImmediate(a);
            UnityEngine.Object.DestroyImmediate(b);
        }

        [Test]
        public void Library_HasAMusicRowForEveryState()
        {
            foreach (AudioMusicState state in Enum.GetValues(typeof(AudioMusicState)))
            {
                if (state == AudioMusicState.None) continue;
                Assert.IsNotNull(_library.music.Find(e => e.state == state), state.ToString());
            }
        }

        [Test]
        public void Music_IsServedPerState_AndSilentStatesRequestNothing()
        {
            var clip = AudioClip.Create("loop", 100, 1, 44100, false);
            var row = _library.music.Find(e => e.state == AudioMusicState.Overworld);
            row.clip = clip;
            row.volume = 0.5f;

            Assert.IsTrue(_library.TryGetMusic(AudioMusicState.Overworld, out _, out float volume));
            Assert.AreEqual(0.5f, volume);
            Assert.IsFalse(_library.TryGetMusic(AudioMusicState.Battle, out _, out _));

            var requested = new List<string>();
            Action<AudioMusicState, AudioClip> handler = (state, c) => requested.Add(state + ":" + c.name);
            GameAudio.MusicRequested += handler;
            try
            {
                GameAudio.SetMusicState(AudioMusicState.Overworld);
                GameAudio.SetMusicState(AudioMusicState.Battle); // no clip assigned: nothing requested, remembered as the state
                GameAudio.SetMusicState(AudioMusicState.None);
            }
            finally
            {
                GameAudio.MusicRequested -= handler;
                UnityEngine.Object.DestroyImmediate(clip);
            }

            CollectionAssert.AreEqual(new[] { "Overworld:loop" }, requested);
            Assert.AreEqual(AudioMusicState.None, GameAudio.MusicState);
        }

        [Test]
        public void MusicFade_MovesLinearlyTowardsTheTarget_AndClamps()
        {
            Assert.AreEqual(0.5f, MusicPlayer.FadeStep(0f, true, MusicPlayer.FadeSeconds / 2f), 1e-4f);
            Assert.AreEqual(1f, MusicPlayer.FadeStep(0.9f, true, 10f), 1e-4f);
            Assert.AreEqual(0.25f, MusicPlayer.FadeStep(0.75f, false, MusicPlayer.FadeSeconds / 2f), 1e-4f);
            Assert.AreEqual(0f, MusicPlayer.FadeStep(0.1f, false, 10f), 1e-4f);
        }

        [Test]
        public void MenuCuesUseTheUiBus_EverythingElseSfx()
        {
            Assert.AreEqual(AudioBus.Ui, AudioCues.BusFor(AudioCue.UiConfirm));
            Assert.AreEqual(AudioBus.Ui, AudioCues.BusFor(AudioCue.EquipItem));
            Assert.AreEqual(AudioBus.Sfx, AudioCues.BusFor(AudioCue.AttackHit));
            Assert.AreEqual(AudioBus.Sfx, AudioCues.BusFor(AudioCue.Footstep));
        }

        [Test]
        public void CombatBridge_TurnsEncounterEventsIntoCues_AndDetachesWhenItEnds()
        {
            var events = new FakeCombatEvents();
            var seen = new List<AudioCue>();
            GameAudio.CueRequested += seen.Add;
            try
            {
                CombatAudioBridge.Attach(events);
                events.RaiseDamage(new DamageRollInfo { hit = true });
                events.RaiseDamage(new DamageRollInfo { hit = false });
                events.RaiseDefeated();
                events.RaiseEnded(new EncounterResult { victory = true });

                // After the end the bridge has detached: further events make no sound.
                events.RaiseDamage(new DamageRollInfo { hit = true });
            }
            finally
            {
                GameAudio.CueRequested -= seen.Add;
            }

            CollectionAssert.AreEqual(new[]
            {
                AudioCue.BattleStart, AudioCue.AttackHit, AudioCue.DamageTaken, AudioCue.AttackMiss, AudioCue.CombatantDefeated, AudioCue.BattleWin,
            }, seen);
        }

        [Test]
        public void CombatBridge_GivesAFledFightItsOwnSoundInsteadOfAStinger()
        {
            var events = new FakeCombatEvents();
            var seen = new List<AudioCue>();
            GameAudio.CueRequested += seen.Add;
            try
            {
                CombatAudioBridge.Attach(events);
                events.RaiseEnded(new EncounterResult { fled = true });
            }
            finally
            {
                GameAudio.CueRequested -= seen.Add;
            }

            CollectionAssert.AreEqual(new[] { AudioCue.BattleStart, AudioCue.Flee }, seen);
        }

#pragma warning disable CS0067 // unused events: the interface requires them
        sealed class FakeCombatEvents : ICombatEvents
        {
            public event Action<CombatantRuntime> OnTurnStarted;
            public event Action<CombatantRuntime> OnCombatantStateChanged;
            public event Action<CombatantRuntime> OnCombatantDefeated;
            public event Action<IReadOnlyList<CombatantRuntime>> OnTurnOrderChanged;
            public event Action<EncounterResult> OnEncounterEnded;
            public event Action<CombatantRuntime> OnHoverChanged;
            public event Action<DamageRollInfo> OnDamageRolled;
            public event Action<AimReadout> OnAimChanged;
            public event Action OnPlayerActionCommitted;

            public void RaiseDamage(DamageRollInfo info) => OnDamageRolled?.Invoke(info);
            public void RaiseDefeated() => OnCombatantDefeated?.Invoke(new CombatantRuntime());
            public void RaiseEnded(EncounterResult result) => OnEncounterEnded?.Invoke(result);
        }
#pragma warning restore CS0067
    }
}
