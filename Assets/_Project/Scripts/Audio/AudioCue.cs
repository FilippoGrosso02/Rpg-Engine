namespace Game.Audio
{
    /// <summary>
    /// Every one-shot sound the game can ask for. Gameplay code names a cue; which clip plays for it is decided in the
    /// <see cref="AudioLibrary"/> asset, so changing a sound never needs a code change and a cue with no clip is simply silent.
    /// Add new cues at the END so serialized libraries keep their order.
    /// </summary>
    public enum AudioCue
    {
        None = 0,

        // Menus
        UiMove,
        UiConfirm,
        UiBack,

        // Dungeon
        ChestOpen,
        DoorLocked,
        DoorUnlock,
        ShrineUse,
        TrapTrigger,
        PotionUse,

        // Combat
        BattleStart,
        BattleWin,
        BattleLose,
        AttackHit,
        AttackMiss,
        CombatantDefeated,

        // Added later (append only, so saved libraries keep their order)
        DamageTaken,
        SpellCast,
        Flee,
        MenuOpen,
        MenuClose,
        UiDenied,
        EquipItem,
        UnequipItem,
        Footstep,
    }

    /// <summary>Which music "scene" is playing. Each state can have its own looping track in the library.</summary>
    public enum AudioMusicState
    {
        None = 0,
        Menu,
        Overworld,
        Battle,
        Victory,
    }

    /// <summary>Volume groups the game controls (Master is AudioListener.volume; the rest scale the clips and tracks in their group).</summary>
    public enum AudioBus
    {
        Master,
        Music,
        Sfx,
        Ui,
    }

    public static class AudioCues
    {
        /// <summary>The mixer bus a cue belongs to: menu sounds on UI, everything else on SFX.</summary>
        public static AudioBus BusFor(AudioCue cue)
        {
            switch (cue)
            {
                case AudioCue.UiMove:
                case AudioCue.UiConfirm:
                case AudioCue.UiBack:
                case AudioCue.UiDenied:
                case AudioCue.MenuOpen:
                case AudioCue.MenuClose:
                case AudioCue.EquipItem:
                case AudioCue.UnequipItem:
                    return AudioBus.Ui;
                default:
                    return AudioBus.Sfx;
            }
        }
    }
}
