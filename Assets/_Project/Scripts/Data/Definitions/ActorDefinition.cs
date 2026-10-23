using System.Collections.Generic;
using Game.Data.Rules;
using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>
    /// A playable hero under the d12 OSR rules: five ability scores, a class (hit die and spell list), and starting gear.
    /// Everything combat needs (hit points, armor class, attack bonus, damage) is derived from these, never stored.
    /// </summary>
    [CreateAssetMenu(fileName = "NewActor", menuName = "Database/Actor")]
    public class ActorDefinition : GameDefinition
    {
        public Sprite portrait;
        public GameObject battleModelPrefab;
        public AnimationSetDefinition animationSet;
        public string classId;

        [Header("Ability scores (start at 6, prime stat up to 10)")]
        public int strength = 6;
        public int dexterity = 6;
        public int vitality = 6;
        public int mind = 6;
        public int charm = 6;

        public int level = 1;

        /// <summary>How far (in world units) this character can move on its own turn.</summary>
        public int moveRange = 6;

        /// <summary>Attack range expressed as a multiplier of ActionResolver.RangeUnit — 1 is
        /// the melee baseline (must close to melee distance to attack); higher values give a
        /// longer ranged attack distance.</summary>
        public int attackRangeMultiplier = 1;

        /// <summary>True for casters: they gain AP each turn and pay a spell's level in AP to cast it.</summary>
        public bool usesAp;

        /// <summary>Short flavor label for the HUD (e.g. "Front-line fighter") — purely cosmetic.</summary>
        public string roleLabel;

        /// <summary>Basic Attack first, then any spells.</summary>
        public List<string> startingSkillIds = new List<string>();

        public string startingWeaponId;

        /// <summary>Worn armor pieces (body armor and shield are separate items); their AC bonuses add up.</summary>
        public List<string> startingArmorIds = new List<string>();

        public int StrengthModifier => OsrRules.Modifier(strength);
        public int DexterityModifier => OsrRules.Modifier(dexterity);
        public int VitalityModifier => OsrRules.Modifier(vitality);
    }
}
