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

        // --- Ability scores (heroes; a monster only fills in DEX, which breaks initiative ties) ---
        public int strength;
        public int dexterity;
        public int vitality;
        public int mind;
        public int charm;

        // --- Defence ---
        /// <summary>DEF before acid and Guard: 2 + armor + shield (+ DEX minor modifier for an evade class) for a hero; the stat block's DEF for a monster.</summary>
        public int baseArmorClass;

        /// <summary>The DEF worn as armor and shield (heroes). Acid can corrode this, but never below 0.</summary>
        public int armorBonus;

        /// <summary>DEF lost to acid so far (persisted between fights until repaired).</summary>
        public int armorCorrosion;

        /// <summary>Guarding: +2 DEF until this combatant's next turn.</summary>
        public bool guarding;

        /// <summary>DEF right now (base, minus corrosion, plus Guard, never below 2). Status modifiers are added by ActionResolver.</summary>
        public int ArmorClass => Mathf.Max(OsrRules.MinDefense, baseArmorClass - armorCorrosion + (guarding ? OsrRules.GuardDefenseBonus : 0));

        // --- Offence ---
        /// <summary>ATK: the STR or DEX score plus the weapon's ATK modifier for a hero, the stat block's ATK for a monster.</summary>
        public int attackBonus;

        /// <summary>Basic Attack damage: the weapon die only, no ability bonus (shortsword d6, ogre 2d6).</summary>
        public DiceRoll attackDamage;

        public bool weaponIsMetal;

        /// <summary>Damage lost to acid from hitting slimes with a metal weapon (cumulative, persisted until sharpened).</summary>
        public int weaponCorrosion;

        // --- Saves, morale and monster traits ---
        /// <summary>Rolled with d10 against a DV for physical saves. A hero's VIT score; a monster's flat save stat.</summary>
        public int saveVitality;

        /// <summary>Rolled with d10 against a DV for mental saves. A hero's MIND score; a monster's flat save stat.</summary>
        public int saveMind;

        /// <summary>2d6 target number: the monster flees when a 2d6 roll is higher. 0 = never checks.</summary>
        public int morale;

        /// <summary>Added to the d10 initiative roll, which is made once at the start of the fight (the DEX minor modifier).</summary>
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

        /// <summary>Own turns started so far. A caster's AP is 1 at the start of a fight and grows by 1 every turn after the first.</summary>
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
