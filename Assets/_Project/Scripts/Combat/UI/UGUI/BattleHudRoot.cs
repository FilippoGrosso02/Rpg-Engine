using System.Collections.Generic;
using Game.Combat.Contracts;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// The only class in this assembly that touches ICombatEvents/IPlayerCommandInput
    /// directly — every other view here is dumb and only ever gets pushed state. Bind/Unbind
    /// are called per-encounter (by Services) rather than assuming a singleton combat session.
    /// </summary>
    public class BattleHudRoot : MonoBehaviour
    {
        [SerializeField] private CommandMenuController commandMenu;
        [SerializeField] private ActingUnitCard actingUnitCard;
        [SerializeField] private TurnBanner turnBanner;
        [SerializeField] private TurnOrderStrip turnOrderStrip;
        [SerializeField] private EncounterResultPanel resultPanel;
        [SerializeField] private CombatantNameplate allyNameplatePrefab;
        [FormerlySerializedAs("enemyHpBarTemplate")]
        [SerializeField] private CombatantNameplate enemyNameplatePrefab;
        [Tooltip("Full-screen RectTransform on the Overlay canvas that nameplates are spawned under — first sibling so they draw beneath the rest of the HUD.")]
        [SerializeField] private RectTransform nameplateLayer;
        [SerializeField] private ActiveTurnIndicator activeTurnIndicator;
        [SerializeField] private HoverStatsPanel hoverStatsPanel;
        [SerializeField] private DamageRollBar damageRollBar;

        private ICombatEvents _events;
        private readonly Dictionary<CombatantRuntime, CombatantNameplate> _nameplates = new Dictionary<CombatantRuntime, CombatantNameplate>();
        private Camera _arenaCamera;

        /// <summary>Exposed for the HUD PlayMode test.</summary>
        public bool IsResultPanelVisible => resultPanel != null && resultPanel.IsShowing;

        public ActingUnitCard ActingUnitCard => actingUnitCard;

        public void Bind(ICombatEvents events, IPlayerCommandInput input)
        {
            Unbind();

            _events = events;

            _events.OnTurnStarted += HandleTurnStarted;
            _events.OnCombatantStateChanged += HandleCombatantStateChanged;
            _events.OnCombatantDefeated += HandleCombatantDefeated;
            _events.OnTurnOrderChanged += HandleTurnOrderChanged;
            _events.OnEncounterEnded += HandleEncounterEnded;
            _events.OnHoverChanged += HandleHoverChanged;
            _events.OnDamageRolled += HandleDamageRolled;

            if (commandMenu != null)
            {
                commandMenu.Bind(input);
            }

            if (resultPanel != null)
            {
                resultPanel.Bind(input);
            }

            actingUnitCard?.Clear();
            turnBanner?.Hide();
            turnOrderStrip?.Clear();
            activeTurnIndicator?.Hide();
            hoverStatsPanel?.Hide();
            damageRollBar?.Hide();
        }

        public void Unbind()
        {
            if (_events != null)
            {
                _events.OnTurnStarted -= HandleTurnStarted;
                _events.OnCombatantStateChanged -= HandleCombatantStateChanged;
                _events.OnCombatantDefeated -= HandleCombatantDefeated;
                _events.OnTurnOrderChanged -= HandleTurnOrderChanged;
                _events.OnEncounterEnded -= HandleEncounterEnded;
                _events.OnHoverChanged -= HandleHoverChanged;
                _events.OnDamageRolled -= HandleDamageRolled;
            }

            _events = null;
            _arenaCamera = null;

            commandMenu?.Unbind();
            resultPanel?.Unbind();
            activeTurnIndicator?.Hide();
            hoverStatsPanel?.Hide();
            damageRollBar?.Hide();
            ClearNameplates();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void HandleTurnStarted(CombatantRuntime combatant)
        {
            EnsureNameplate(combatant);
            actingUnitCard?.SetActingCombatant(combatant);
            turnBanner?.Show(combatant);
            commandMenu?.SetActingCombatant(combatant.isPlayerSide ? combatant : null);
            activeTurnIndicator?.ShowOn(combatant.viewTransform);
        }

        private void HandleCombatantStateChanged(CombatantRuntime combatant)
        {
            EnsureNameplate(combatant);
            actingUnitCard?.RefreshIfDisplayed(combatant);

            if (_nameplates.TryGetValue(combatant, out var plate) && plate != null)
            {
                plate.Refresh(combatant);
            }
        }

        private void HandleCombatantDefeated(CombatantRuntime combatant)
        {
            actingUnitCard?.RefreshIfDisplayed(combatant);

            // Kept, not destroyed: the nameplate hides itself while the combatant's view is
            // inactive (which is how the runner hides defeated bodies), so it comes back on its
            // own if a future revive effect reactivates the view.
            if (_nameplates.TryGetValue(combatant, out var plate) && plate != null)
            {
                plate.Refresh(combatant);
            }
        }

        private void HandleTurnOrderChanged(IReadOnlyList<CombatantRuntime> upcomingOrder)
        {
            foreach (var combatant in upcomingOrder)
            {
                EnsureNameplate(combatant);
            }

            turnOrderStrip?.Rebuild(upcomingOrder);
        }

        private void HandleEncounterEnded(EncounterResult result)
        {
            commandMenu?.SetActingCombatant(null);
            activeTurnIndicator?.Hide();
            turnBanner?.Hide();
            hoverStatsPanel?.Hide();
            damageRollBar?.Hide();
            resultPanel?.Show(result);
        }

        private void HandleHoverChanged(CombatantRuntime combatant)
        {
            hoverStatsPanel?.SetHoveredCombatant(combatant);
        }

        private void HandleDamageRolled(DamageRollInfo info)
        {
            damageRollBar?.Show(info);
        }

        private void EnsureNameplate(CombatantRuntime combatant)
        {
            if (combatant == null || combatant.viewTransform == null || _nameplates.ContainsKey(combatant))
            {
                return;
            }

            var prefab = combatant.isPlayerSide ? allyNameplatePrefab : enemyNameplatePrefab;
            if (prefab == null || nameplateLayer == null)
            {
                return;
            }

            var plate = Instantiate(prefab, nameplateLayer);
            plate.gameObject.SetActive(true);
            plate.Initialize(combatant, ResolveArenaCamera(combatant));
            _nameplates[combatant] = plate;
        }

        /// <summary>
        /// Camera.main is ambiguous during battle (SampleScene's Main Camera stays enabled next to
        /// the arena's Battle Camera, both tagged MainCamera), so take the camera from the scene
        /// the combatant's view was spawned into instead — the same lookup the runner uses.
        /// </summary>
        private Camera ResolveArenaCamera(CombatantRuntime combatant)
        {
            if (_arenaCamera != null)
            {
                return _arenaCamera;
            }

            foreach (var rootObject in combatant.viewTransform.gameObject.scene.GetRootGameObjects())
            {
                var sceneCamera = rootObject.GetComponentInChildren<Camera>();
                if (sceneCamera != null)
                {
                    _arenaCamera = sceneCamera;
                    return _arenaCamera;
                }
            }

            return Camera.main;
        }

        private void ClearNameplates()
        {
            foreach (var plate in _nameplates.Values)
            {
                if (plate != null)
                {
                    Destroy(plate.gameObject);
                }
            }

            _nameplates.Clear();
        }
    }
}
