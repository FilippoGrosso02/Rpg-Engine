using System.Collections.Generic;
using Game.Audio;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Fills the AudioLibrary's empty cue rows from the Leohpaz "RPG Essentials Free" pack. Re-runnable: "Fill" only touches cues that have no
    /// clip yet, so clips you swapped in by hand survive. "Reset" puts every cue back to the pack defaults below.
    /// To use different sounds, edit the table or just drag clips onto the rows of Resources/Audio/AudioLibrary.asset.
    /// Run via: Tools/Audio/Fill Default Sounds (or -executeMethod Game.EditorTools.AudioDefaultsSetup.Fill)
    /// </summary>
    public static class AudioDefaultsSetup
    {
        const string LibraryPath = "Assets/_Project/Resources/Audio/AudioLibrary.asset";
        const string Pack = "Assets/Leohpaz/RPG_Essentials_Free/";
        const string MusicPack = "Assets/PSX Horror Music Pack Vol 1 - Exploration/";

        const string Battle = Pack + "10_Battle_SFX/";
        const string Ui = Pack + "10_UI_Menu_SFX/";
        const string Move = Pack + "12_Player_Movement_SFX/";
        const string Magic = Pack + "8_Atk_Magic_SFX/";
        const string Buffs = Pack + "8_Buffs_Heals_SFX/";

        struct Row
        {
            public AudioCue cue;
            public float volume;
            public string[] files;

            public Row(AudioCue cue, float volume, params string[] files)
            {
                this.cue = cue;
                this.volume = volume;
                this.files = files;
            }
        }

        static readonly Row[] Table =
        {
            // Menus
            new Row(AudioCue.UiMove, 0.6f, Ui + "001_Hover_01.wav"),
            new Row(AudioCue.UiConfirm, 0.8f, Ui + "013_Confirm_03.wav"),
            new Row(AudioCue.UiBack, 0.8f, Ui + "029_Decline_09.wav"),
            new Row(AudioCue.UiDenied, 0.8f, Ui + "033_Denied_03.wav"),
            new Row(AudioCue.MenuOpen, 0.8f, Ui + "092_Pause_04.wav"),
            new Row(AudioCue.MenuClose, 0.8f, Ui + "098_Unpause_04.wav"),
            new Row(AudioCue.EquipItem, 0.9f, Ui + "070_Equip_10.wav"),
            new Row(AudioCue.UnequipItem, 0.9f, Ui + "071_Unequip_01.wav"),

            // Dungeon
            new Row(AudioCue.ChestOpen, 1f, Ui + "079_Buy_sell_01.wav"),
            new Row(AudioCue.DoorLocked, 0.9f, Ui + "033_Denied_03.wav"),
            new Row(AudioCue.DoorUnlock, 1f, Move + "42_Cling_climb_03.wav"),
            new Row(AudioCue.ShrineUse, 1f, Buffs + "02_Heal_02.wav"),
            new Row(AudioCue.TrapTrigger, 1f, Move + "61_Hit_03.wav"),
            new Row(AudioCue.PotionUse, 1f, Ui + "051_use_item_01.wav"),
            new Row(AudioCue.Footstep, 0.45f, Move + "08_Step_rock_02.wav"),

            // Combat
            new Row(AudioCue.BattleStart, 1f, Battle + "55_Encounter_02.wav"),
            new Row(AudioCue.BattleWin, 1f, Buffs + "16_Atk_buff_04.wav"),
            new Row(AudioCue.BattleLose, 1f, Buffs + "21_Debuff_01.wav"),
            new Row(AudioCue.AttackHit, 1f, Battle + "22_Slash_04.wav", Battle + "03_Claw_03.wav", Battle + "08_Bite_04.wav"),
            new Row(AudioCue.AttackMiss, 0.9f, Battle + "35_Miss_Evade_02.wav"),
            new Row(AudioCue.DamageTaken, 0.9f, Battle + "15_Impact_flesh_02.wav", Battle + "77_flesh_02.wav"),
            new Row(AudioCue.CombatantDefeated, 1f, Battle + "69_Enemy_death_01.wav"),
            new Row(AudioCue.SpellCast, 1f, Magic + "04_Fire_explosion_04_medium.wav", Magic + "18_Thunder_02.wav", Magic + "46_Poison_01.wav"),
            new Row(AudioCue.Flee, 1f, Battle + "51_Flee_02.wav"),
        };

        struct MusicRow
        {
            public AudioMusicState state;
            public float volume;
            public string file;

            public MusicRow(AudioMusicState state, float volume, string file)
            {
                this.state = state;
                this.volume = volume;
                this.file = file;
            }
        }

        // Menu and Victory are left silent on purpose: a state with no track fades the music out.
        static readonly MusicRow[] MusicTable =
        {
            new MusicRow(AudioMusicState.Overworld, 0.5f, MusicPack + "PSX Below Ground.wav"),
            new MusicRow(AudioMusicState.Battle, 0.55f, MusicPack + "PSX Empty Halls.wav"),
        };

        [MenuItem("Tools/Audio/Fill Default Sounds")]
        public static void Fill() => Apply(overwrite: false);

        [MenuItem("Tools/Audio/Reset Sounds To Pack Defaults")]
        public static void Reset() => Apply(overwrite: true);

        static void Apply(bool overwrite)
        {
            var library = EnsureLibrary();
            library.EnsureAllRows();

            int filled = 0, missing = 0;
            foreach (var row in Table)
            {
                var entry = library.cues.Find(e => e != null && e.cue == row.cue);
                if (entry == null) continue;
                if (!overwrite && HasClip(entry)) continue;

                var clips = new List<AudioClip>();
                foreach (var path in row.files)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip == null) { Debug.LogWarning($"[AudioDefaultsSetup] Missing clip {path}"); missing++; continue; }
                    clips.Add(clip);
                }

                if (clips.Count == 0) continue;
                entry.clips = clips.ToArray();
                entry.volume = row.volume;
                filled++;
            }

            int music = 0;
            foreach (var row in MusicTable)
            {
                var entry = library.music.Find(e => e != null && e.state == row.state);
                if (entry == null || (!overwrite && entry.clip != null)) continue;

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(row.file);
                if (clip == null) { Debug.LogWarning($"[AudioDefaultsSetup] Missing music {row.file}"); missing++; continue; }

                StreamFromDisk(row.file);
                entry.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(row.file);
                entry.volume = row.volume;
                music++;
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AudioDefaultsSetup] {(overwrite ? "Reset" : "Filled")} {filled} cue(s) and {music} music state(s) with the default sounds ({missing} clip(s) missing).");
        }

        /// <summary>Long loops should stream (compressed) instead of decompressing 12+ MB of PCM into memory when the scene loads.</summary>
        static void StreamFromDisk(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) return;

            var settings = importer.defaultSampleSettings;
            if (settings.loadType == AudioClipLoadType.Streaming && settings.compressionFormat == AudioCompressionFormat.Vorbis) return;

            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        static bool HasClip(AudioLibrary.Entry entry)
        {
            if (entry.clips == null) return false;
            foreach (var clip in entry.clips) if (clip != null) return true;
            return false;
        }

        static AudioLibrary EnsureLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (library != null) return library;

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources/Audio"))
                AssetDatabase.CreateFolder("Assets/_Project/Resources", "Audio");
            library = ScriptableObject.CreateInstance<AudioLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
            return library;
        }
    }
}
