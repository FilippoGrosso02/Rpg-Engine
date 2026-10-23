using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Rules;
using UnityEngine;

namespace Game.Combat.Contracts
{
    /// <summary>
    /// The mutable stand-in for one combatant for the duration of a single encounter.
    /// Never mutate ActorDefinition/EnemyDefinition directly — stats here start as values derived from the definition
    /// (ability scores, gear, stat block) and change freely as the fight plays out.
    /// </summary>
    public class CombatantRuntime
    {
        public string definitionId;
        public string displayName;
        public string roleLabel;
        public bool isPlayerSide;

        public int maxHp;
        public int currentHp;

        // --- Ability scores (heroes only; monsters have a flat stat block and leave these at 0) ---
        public int strength;
        public int dexterity;
        public int vitality;
        public int mind;
        public int charm;

        // --- Defence ---
        /// <summary>6 + armor + DEX modifier for a hero; the stat block's AC for a monster.</summary>
        public int baseArmorClass;

        /// <summary>The AC worn as armor (heroes). Acid can corrode this, but never below 0.</summary>
        public int armorBonus;

        /// <summary>AC lost to acid so far (persisted between fights until repaired).</summary>
        public int armorCorrosion;

        /// <summary>Guarding: +2 AC until this combatant's next turn.</summary>
        public bool guarding;

        /// <summary>Armor class right now (base, minus corrosion, plus Guard). Status modifiers are added by ActionResolver.</summary>
        public int ArmorClass => Mathf.Max(0, baseArmorClass - armorCorrosion + (guarding ? OsrRules.GuardArmorClassBonus : 0));

        // --- Offence ---
        /// <summary>Added to the d12 attack roll (the STR or DEX score for a hero, the stat block's attack for a monster).</summary>
        public int attackBonus;

        /// <summary>Basic Attack damage, including the STR modifier where it applies (sword d8 + 2, ogre 2d6).</summary>
        public DiceRoll attackDamage;

        public bool weaponIsMetal;

        /// <summary>Damage lost to acid from hitting slimes with a metal weapon (cumulative, persisted until sharpened).</summary>
        public int weaponCorrosion;

        // --- Saves, morale and monster traits ---
        /// <summary>d12 + this >= 12 saves against physical effects. A hero's VIT score; a monster's flat save.</summary>
        public int saveVitality;

        /// <summary>d12 + this >= 12 saves against mental effects. A hero's MIND score; a monster's flat save.</summary>
        public int saveMind;

        /// <summary>2d6 target number: the monster flees when a 2d6 roll is higher. 0 = never checks.</summary>
        public int morale;

        /// <summary>Added to the d12 initiative roll each round (a hero's DEX score).</summary>
        public int initiative;

        public bool corrodesArmor;
        public bool corrodesWeapons;
        public bool immuneToCharm;
        public bool immuneToFear;
        public bool immuneToSleep;

        // Morale triggers already rolled for, so each fires at most once.
        public bool moraleCheckedFirstCasualty;
        public bool moraleCheckedHalfDown;
        public bool moraleCheckedHalfHp;

        /// <summary>Failed a morale check and left the fight. Counts as out of combat (and as a won enemy) but not as slain.</summary>
        public bool hasFled;

        // --- Spell points (casters only) ---
        public bool usesAp;
        public int currentAp;
        /// <summary>Lives here (not a hardcoded constant) so Contracts-only UI code can display "X/Y AP".</summary>
        public int maxAp = 10;

        /// <summary>Set by Guard; consumed at this combatant's next turn start for the caster's "+1 more AP if guarding".</summary>
        public bool pendingGuardApBonus;

        /// <summary>Own turns started so far. A caster's AP is 1 at the start of a fight and grows by 1 from its second turn on.</summary>
        public int turnsTaken;

        /// <summary>How far (in world units) this combatant can move on its own turn.</summary>
        public float moveRange;

        /// <summary>Attack range as a multiplier of ActionResolver.RangeUnit — 1 is the melee
        /// baseline (must close to melee distance); higher values give a longer ranged attack distance.</summary>
        public int attackRangeMultiplier;

        public List<string> skillIds = new List<string>();
        public Vector3 position;

        /// <summary>
        /// The instantiated battleModelPrefab's transform, if any. Plain UnityEngine.Transform
        /// (not the TrailsBattle3D-specific CombatantView component) so UI code that only
        /// depends on Contracts can still parent world-space elements (e.g. an enemy HP bar)
        /// onto a combatant's visual without referencing the runtime implementation assembly.
        /// </summary>
        public Transform viewTransform;

        public List<StatusEffectInstance> statuses = new List<StatusEffectInstance>();

        /// <summary>Slain or fled — either way no longer part of the fight.</summary>
        public bool IsDefeated => currentHp <= 0 || hasFled;

        /// <summary>Reduced to 0 HP (as opposed to having fled).</summary>
        public bool IsSlain => currentHp <= 0;
    }
}
