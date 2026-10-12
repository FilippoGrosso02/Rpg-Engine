using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat.Contracts
{
    /// <summary>
    /// The mutable stand-in for one combatant for the duration of a single encounter.
    /// Never mutate ActorDefinition/EnemyDefinition directly — stats here start as a copy
    /// of the definition's base stats (plus starting equipment bonuses for allies) and
    /// change freely as the fight plays out.
    /// </summary>
    public class CombatantRuntime
    {
        public string definitionId;
        public string displayName;
        public string roleLabel;
        public bool isPlayerSide;

        public int maxHp;
        public int currentHp;
        public int maxEp;
        public int currentEp;

        public int atk;
        public int def;
        public int speed;

        /// <summary>AP cap is a universal 10 per the Duelwright rules, but lives here (not a
        /// hardcoded constant) so Contracts-only UI code can display "X/Y AP" without needing
        /// to know the rule that sets Y.</summary>
        public int currentAp;
        public int maxAp = 10;

        /// <summary>0-10, clamped. See FearRuntime (TrailsBattle3D) for thresholds/decay/freeze.</summary>
        public int fear;

        /// <summary>Reset false at this combatant's own turn start; set true whenever it takes
        /// a hit before its next turn. Fear decays -1 at turn start only if this stayed false.</summary>
        public bool wasHitSinceLastTurn;

        /// <summary>How far (in world units) this combatant can move on its own turn.</summary>
        public float moveRange;

        /// <summary>Attack range as a multiplier of ActionResolver.RangeUnit — 1 is the melee
        /// baseline (must close to melee distance); higher values give a longer ranged attack
        /// distance instead of the old unlimited-range Ranged trait.</summary>
        public int attackRangeMultiplier;

        /// <summary>Horror-type monsters (EnemyDefinition.isHorror) inflict +1 extra fear per hit.</summary>
        public bool isHorror;

        public List<string> skillIds = new List<string>();

        public Vector3 position;
        public bool guarding;

        /// <summary>Set by Guard; consumed (added to AP regen, then cleared) at this
        /// combatant's next turn start — Guard's "+2 AP then" bonus.</summary>
        public bool pendingGuardApBonus;

        /// <summary>Smoke Bomb: untargetable by Basic Attacks until this combatant's next turn.</summary>
        public bool untargetable;

        /// <summary>Noble Challenge: this combatant is forced to attack forcedAttackTarget on
        /// its own next turn instead of picking a target normally. Consumed (cleared) when read.</summary>
        public CombatantRuntime forcedAttackTarget;

        /// <summary>Life Bond: half of this combatant's incoming damage is redirected to
        /// lifeBondProtector instead, for lifeBondTurnsRemaining of this combatant's own turns.</summary>
        public CombatantRuntime lifeBondProtector;
        public int lifeBondTurnsRemaining;

        /// <summary>Bodyguard: the next attack aimed at bodyguardTarget redirects onto this
        /// combatant instead, until this combatant's own next turn.</summary>
        public CombatantRuntime bodyguardTarget;

        /// <summary>
        /// The instantiated battleModelPrefab's transform, if any. Plain UnityEngine.Transform
        /// (not the TrailsBattle3D-specific CombatantView component) so UI code that only
        /// depends on Contracts can still parent world-space elements (e.g. an enemy HP bar)
        /// onto a combatant's visual without referencing the runtime implementation assembly.
        /// </summary>
        public Transform viewTransform;

        public List<StatusEffectInstance> statuses = new List<StatusEffectInstance>();

        public bool IsDefeated => currentHp <= 0;
    }
}
