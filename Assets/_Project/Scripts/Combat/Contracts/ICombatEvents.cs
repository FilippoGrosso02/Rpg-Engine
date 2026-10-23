using System;
using System.Collections.Generic;

namespace Game.Combat.Contracts
{
    /// <summary>
    /// Read-only event surface any combat implementation exposes so a UI layer can render
    /// state without ever depending on the concrete runner type.
    /// </summary>
    public interface ICombatEvents
    {
        event Action<CombatantRuntime> OnTurnStarted;
        event Action<CombatantRuntime> OnCombatantStateChanged;
        event Action<CombatantRuntime> OnCombatantDefeated;
        event Action<IReadOnlyList<CombatantRuntime>> OnTurnOrderChanged;
        event Action<EncounterResult> OnEncounterEnded;

        /// <summary>Fires whenever the combatant under the player's cursor changes — null when
        /// nothing (or no longer anything) is hovered. Independent of whose turn it is.</summary>
        event Action<CombatantRuntime> OnHoverChanged;

        /// <summary>Fires once per damaging hit with the full ATK/roll/DEF breakdown, so the UI
        /// can show the formula (and animate the die) instead of just the final HP change.</summary>
        event Action<DamageRollInfo> OnDamageRolled;

        /// <summary>Fires when the enemy under the reticle, or the hit chance against it, changes. Only fires during a player turn.</summary>
        event Action<AimReadout> OnAimChanged;

        /// <summary>Fires the moment the player's action for this turn is committed (an attack, a spell, Guard or Flee), so the HUD can
        /// lock its input until the next turn starts.</summary>
        event Action OnPlayerActionCommitted;
    }
}
