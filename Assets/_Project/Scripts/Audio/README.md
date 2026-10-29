# Audio

Gameplay never names a sound file. It asks `GameAudio` (assembly `Game.Audio`) for a **cue**, and `Resources/Audio/AudioLibrary.asset` decides which clip(s) play.
A cue with no clip is silent, so a missing sound never breaks anything.

```csharp
GameAudio.Play(AudioCue.ChestOpen);                    // one-shot (2D)
GameAudio.SetMusicState(AudioMusicState.Battle);       // crossfades to that state's track
GameAudio.SetBusVolume(AudioBus.Music, 0.5f);          // 0-1 per group (Music / Sfx / Ui); Master is AudioListener.volume
```

## Changing a sound
Open `Resources/Audio/AudioLibrary.asset`:
- **Cues:** every `AudioCue` has a row with a list of clips (one is picked at random each time, so a few variations keep repeats from sounding identical),
  a volume, and a pitch jitter.
- **Music:** one row per `AudioMusicState` (Menu, Overworld, Battle, Victory) with a looping clip and volume. A state with no clip fades the music out.
  Switching states crossfades (1.2 s, on real time so the pause menu does not freeze it). A track that fades out is paused, not stopped, so returning
  from a fight resumes the dungeon theme mid-song.

Long music should stream: set the clip's import to *Streaming* + *Vorbis* (the setup tool below does this for the two default tracks).

## Defaults
`Tools > Audio > Fill Default Sounds` fills any EMPTY row from the packs in the project (table in `Scripts/Editor/AudioDefaultsSetup.cs`):
Leohpaz "RPG Essentials Free" for the cues, PSX Horror Music Pack "Below Ground" (Overworld) and "Empty Halls" (Battle) for music.
Rows you filled by hand are left alone. `Reset Sounds To Pack Defaults` overwrites everything with the table.

## Adding a cue
Add it at the END of `AudioCue` (so saved libraries keep their order), open the library asset (the row is added automatically), assign a clip, call
`GameAudio.Play(...)`. Which volume group it uses is decided in `AudioCues.BusFor` (menu sounds = Ui, everything else = Sfx).

## What triggers what
| Cue | Triggered by |
|---|---|
| BattleStart / BattleWin / BattleLose / Flee | encounter starts / ends (via `CombatAudioBridge` on `ICombatEvents`, attached by `CombatSystemConfig`) |
| AttackHit / AttackMiss / SpellCast / DamageTaken / CombatantDefeated | combat roll results |
| ChestOpen, DoorLocked, DoorUnlock, ShrineUse, TrapTrigger | dungeon interactables |
| PotionUse, EquipItem, UnequipItem, UiDenied | items and equipment menus |
| MenuOpen / MenuClose, UiMove, UiConfirm, UiBack | pause menu |
| Footstep | walking in the overworld |

Music: `OverworldDirector` (Overworld <-> Battle), `DemoEndPanel` (Victory), `MainMenuController` (Menu).

## If you hear nothing
- `Project Settings > Audio > Disable Unity Audio` must be OFF (everything here is plain Unity audio).
- The Game view's "Mute Audio" button must be off, and the scene needs an `AudioListener` (the cameras have one).
- Check the Music/SFX/UI volume and that the clip's row in the library is filled.

## History
This project briefly integrated FMOD 2.02. It was removed because there were no banks or events to play. The wrapper API above is unchanged, so
FMOD (or another middleware) could be brought back behind `GameAudio` later without touching gameplay code.
