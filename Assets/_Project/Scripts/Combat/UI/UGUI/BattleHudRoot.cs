using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Combat.Contracts;
using Game.Data.Definitions;
using Game.Data.Registry;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// The only class in this assembly that touches ICombatEvents/IPlayerCommandInput directly — every other view here is dumb and only ever
    /// gets pushed state. It owns the wiring between three things: combat events (what happened), the keyboard menu model (what the
    /// player is choosing), and the views (how it looks). Bind/Unbind are called per-encounter (by Services) rather than assuming a
    /// singleton combat session.
    /// </summary>
    public class BattleHudRoot : MonoBehaviour
    {
        [Header("Structure")]
        [SerializeField] private BattleUiSprites sprites;
        [SerializeField] private BattleInputController inputController;
        [SerializeField] private RectTransform uiRoot;

        [Header("Views")]
        [SerializeField] private UnitCardView heroCard;
        [SerializeField] private UnitCardView enemyCard;
        [SerializeField] private ApPipMeter apPips;
        [SerializeField] private SpellHandView spellHand;
        [SerializeField] private DetailTooltipView detailTooltip;
        [SerializeField] private HitBadgeView hitBadge;
        [SerializeField] private TurnSliderView turnSlider;
        [SerializeField] private CommandBarView commandBar;
        [SerializeField] private ModeRibbonView modeRibbon;
        [SerializeField] private FleePromptView fleePrompt;
        [SerializeField] private ReticleView reticle;
        [SerializeField] private FloatingNumbersView floatingNumbers;
        [SerializeField] private DamageRollBar rollBar;
        [SerializeField] private EncounterResultPanel resultPanel;

        [Header("World-space markers")]
        [SerializeField] private CombatantNameplate allyNameplatePrefab;
        [FormerlySerializedAs("enemyHpBarTemplate")]
        [SerializeField] private CombatantNameplate enemyNameplatePrefab;
        [Tooltip("Full-screen RectTransform on the Overlay canvas that nameplates are spawned under — first sibling so they draw beneath the rest of the HUD.")]
        [SerializeField] private RectTransform nameplateLayer;
        [SerializeField] private ActiveTurnIndicator activeTurnIndicator;

        [Header("Options")]
        [Tooltip("Also show the hit-chance badge for the plain Basic Attack. The spec shows it only while a spell is selected, which would leave heroes without spells with no readout at all.")]
        [SerializeField] private bool showHitChanceForBasicAttack = true;

        private ICombatEvents _events;
        private IPlayerCommandInput _input;
        private BattleMenuModel _model;
        private AimReadout _aim = AimReadout.None;
        private bool _suspended;
        private Coroutine _shake;
        private readonly Dictionary<CombatantRuntime, CombatantNameplate> _nameplates = new Dictionary<CombatantRuntime, CombatantNameplate>();
        private Camera _arenaCamera;
        private CombatantRuntime _acting;
        private IReadOnlyList<CombatantRuntime> _lastOrder = new List<CombatantRuntime>();

        /// <summary>Exposed for the HUD PlayMode test.</summary>
        public bool IsResultPanelVisible => resultPanel != null && resultPanel.IsShowing;

        public UnitCardView HeroCard => heroCard;
        public UnitCardView EnemyCard => enemyCard;
        public BattleMenuModel Menu => inputController != null ? inputController.Model : null;

        /// <summary>Called by the HUD builder: hands this component every view it drives.</summary>
        public void Assemble(BattleUiSprites spriteSource, BattleInputController input, RectTransform root, UnitCardView hero, UnitCardView enemy,
            ApPipMeter pips, SpellHandView hand, DetailTooltipView tooltip, HitBadgeView badge,
            TurnSliderView slider, CommandBarView bar, ModeRibbonView ribbon, FleePromptView flee, ReticleView aimReticle,
            FloatingNumbersView numbers, DamageRollBar log, EncounterResultPanel result, CombatantNameplate allyPlate,
            CombatantNameplate enemyPlate, RectTransform plateLayer, ActiveTurnIndicator indicator)
        {
            sprites = spriteSource;
            inputController = input;
            uiRoot = root;
            heroCard = hero;
            enemyCard = enemy;
            apPips = pips;
            spellHand = hand;
            detailTooltip = tooltip;
            hitBadge = badge;
            turnSlider = slider;
            commandBar = bar;
            modeRibbon = ribbon;
            fleePrompt = flee;
            reticle = aimReticle;
            floatingNumbers = numbers;
            rollBar = log;
            resultPanel = result;
            allyNameplatePrefab = allyPlate;
            enemyNameplatePrefab = enemyPlate;
            nameplateLayer = plateLayer;
            activeTurnIndicator = indicator;
        }

        public void Bind(ICombatEvents events, IPlayerCommandInput input)
        {
            Unbind();
            _events = events;
            _input = input;

            _events.OnTurnStarted += HandleTurnStarted;
            _events.OnCombatantStateChanged += HandleCombatantStateChanged;
            _events.OnCombatantDefeated += HandleCombatantDefeated;
            _events.OnTurnOrderChanged += HandleTurnOrderChanged;
            _events.OnEncounterEnded += HandleEncounterEnded;
            _events.OnHoverChanged += HandleHoverChanged;
            _events.OnDamageRolled += HandleDamageRolled;
            _events.OnAimChanged += HandleAimChanged;
            _events.OnPlayerActionCommitted += HandleActionCommitted;

            _model = inputController != null ? inputController.Model : new BattleMenuModel();
            _model.Changed += RefreshMenu;
            _model.GuardChosen += HandleGuardChosen;
            _model.FleeConfirmed += HandleFleeConfirmed;
            _model.SelectionChanged += HandleSelectionChanged;
            _model.CommitRejected += HandleCommitRejected;

            resultPanel?.Bind(input);
            commandBar?.Bind(inputController);

            heroCard?.Show(null);
            enemyCard?.Show(null);
            apPips?.gameObject.SetActive(false);
            turnSlider?.Clear();
            activeTurnIndicator?.Hide();
            hitBadge?.Hide();
            rollBar?.Hide();
            fleePrompt?.Hide();
            detailTooltip?.Hide();
            spellHand?.Hide();
            _model.Lock();
            RefreshMenu();
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
                _events.OnAimChanged -= HandleAimChanged;
                _events.OnPlayerActionCommitted -= HandleActionCommitted;
            }

            if (_model != null)
            {
                _model.Changed -= RefreshMenu;
                _model.GuardChosen -= HandleGuardChosen;
                _model.FleeConfirmed -= HandleFleeConfirmed;
                _model.SelectionChanged -= HandleSelectionChanged;
                _model.CommitRejected -= HandleCommitRejected;
            }

            _events = null;
            _input = null;
            _model = null;
            _arenaCamera = null;
            resultPanel?.Unbind();
            activeTurnIndicator?.Hide();
            ClearNameplates();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        // ------------------------------------------------------------------ combat events

        private void HandleTurnStarted(CombatantRuntime combatant)
        {
            _acting = combatant;
            EnsureNameplate(combatant);
            activeTurnIndicator?.ShowOn(combatant.viewTransform);
            turnSlider?.SetCurrent(combatant);

            if (combatant.isPlayerSide)
            {
                heroCard?.Show(combatant);
                RefreshHeroResources(combatant);
                _model.BeginTurn(BuildSpellEntries(combatant), new List<MenuEntry>());
            }
            else
            {
                enemyCard?.Show(combatant);
                enemyCard?.SetEmphasis(true);
                _model.Lock();
            }
        }

        private void HandleCombatantStateChanged(CombatantRuntime combatant)
        {
            EnsureNameplate(combatant);

            if (_nameplates.TryGetValue(combatant, out var plate) && plate != null)
            {
                plate.Refresh(combatant);
            }

            if (heroCard != null && heroCard.Shown == combatant)
            {
                heroCard.Refresh();
                RefreshHeroResources(combatant);
                if (combatant.isPlayerSide && _model != null)
                {
                    _model.RefreshEntries(BuildSpellEntries(combatant), new List<MenuEntry>());
                }
            }

            if (enemyCard != null && enemyCard.Shown == combatant)
            {
                enemyCard.Refresh();
            }
        }

        private void HandleCombatantDefeated(CombatantRuntime combatant)
        {
            HandleCombatantStateChanged(combatant);

            // Kept, not destroyed: the nameplate hides itself while the combatant's view is inactive (which is how the runner hides
            // defeated or fled bodies), so it comes back on its own if the view is ever reactivated.
            if (enemyCard != null && enemyCard.Shown == combatant && combatant.IsDefeated)
            {
                ShowDefaultEnemy();
            }
        }

        private void HandleTurnOrderChanged(IReadOnlyList<CombatantRuntime> upcomingOrder)
        {
            _lastOrder = upcomingOrder;
            foreach (var combatant in upcomingOrder)
            {
                EnsureNameplate(combatant);
            }

            turnSlider?.SetUpcoming(upcomingOrder);

            if (enemyCard != null && (enemyCard.Shown == null || enemyCard.Shown.IsDefeated))
            {
                ShowDefaultEnemy();
            }
        }

        private void HandleEncounterEnded(EncounterResult result)
        {
            _model?.Lock();
            fleePrompt?.Hide();
            hitBadge?.Hide();
            detailTooltip?.Hide();
            spellHand?.Hide();
            activeTurnIndicator?.Hide();
            rollBar?.Hide();
            SetSuspended(false);
            resultPanel?.Show(result);
        }

        private void HandleHoverChanged(CombatantRuntime combatant)
        {
            if (enemyCard == null)
            {
                return;
            }

            if (combatant != null && !combatant.IsDefeated)
            {
                // Allies and enemies alike: the right-hand card shows whoever the reticle is on.
                enemyCard.Show(combatant);
                enemyCard.SetEmphasis(true);
            }
            else
            {
                // Looking away: the card stays on the last combatant but rests desaturated (unless that enemy is the one acting).
                enemyCard.SetEmphasis(_acting != null && !_acting.isPlayerSide && enemyCard.Shown == _acting);
            }
        }

        private void HandleDamageRolled(DamageRollInfo info)
        {
            rollBar?.Show(info);
            SpawnFloatingNumber(info);
        }

        private void HandleAimChanged(AimReadout readout)
        {
            _aim = readout ?? AimReadout.None;
            reticle?.SetOverEnemy(_aim.target != null);
            RefreshHitBadge();
        }

        private void HandleActionCommitted()
        {
            // The action is spent: input stays ignored until the next turn starts.
            _model?.Lock();
        }

        // ------------------------------------------------------------------ menu model

        private void HandleGuardChosen()
        {
            _input?.SubmitCommand(PlayerCommand.Guard);
        }

        private void HandleFleeConfirmed()
        {
            _input?.SubmitCommand(PlayerCommand.Flee);
        }

        private void HandleSelectionChanged(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                _input?.SubmitCommand(PlayerCommand.CancelCommand);
            }
            else
            {
                _input?.SubmitCommand(PlayerCommand.Craft, skillId);
            }
        }

        private void HandleCommitRejected(MenuEntry entry)
        {
            if (_shake != null)
            {
                StopCoroutine(_shake);
            }

            if (isActiveAndEnabled && spellHand != null)
            {
                _shake = StartCoroutine(ShakeRoutine(((RectTransform)spellHand.transform)));
            }
        }

        /// <summary>A short 2 px horizontal shake: the "no" feedback for committing something that cannot be used.</summary>
        private IEnumerator ShakeRoutine(RectTransform target)
        {
            var home = target.anchoredPosition;
            float[] offsets = { 2f, -2f, 2f, -2f, 0f };
            foreach (float offset in offsets)
            {
                target.anchoredPosition = home + new Vector2(offset, 0f);
                yield return new WaitForSecondsRealtime(0.03f);
            }

            target.anchoredPosition = home;
            _shake = null;
        }

        /// <summary>Redraws every menu-driven view from the model's current state.</summary>
        private void RefreshMenu()
        {
            if (_model == null)
            {
                return;
            }

            var state = _model.State;
            modeRibbon?.SetState(state);
            commandBar?.SetState(state, _model.Spells.Count > 0, _model.Items.Count > 0);

            bool listIsSpells = state == MenuState.Spells || (state == MenuState.FleeConfirm && _model.StateBeforePrompt == MenuState.Spells);
            bool listIsItems = state == MenuState.Items || (state == MenuState.FleeConfirm && _model.StateBeforePrompt == MenuState.Items);

            // The hand only appears while the spell command is open (E); Esc or right click closes it again.
            if (listIsSpells && _model.Spells.Count > 0)
            {
                spellHand?.Show(_model.Spells, _model.SpellIndex, raised: true);
            }
            else
            {
                spellHand?.Hide();
            }

            RefreshDetails(listIsSpells, listIsItems);

            bool prompt = state == MenuState.FleeConfirm;
            if (prompt)
            {
                fleePrompt?.Show();
            }
            else
            {
                fleePrompt?.Hide();
            }

            reticle?.SetDimmed(prompt);
            if (floatingNumbers != null)
            {
                floatingNumbers.Paused = prompt;
            }

            SetSuspended(prompt);
            RefreshHitBadge();
        }

        private void RefreshDetails(bool listIsSpells, bool listIsItems)
        {
            var spell = listIsSpells ? _model.SelectedSpell : null;
            if (spell != null)
            {
                var skill = DatabaseRegistry.GetSkill(spell.id);
                if (skill != null)
                {
                    detailTooltip?.Show(SpellText.Title(skill), SpellText.Effect(skill));
                    return;
                }
            }

            if (listIsItems)
            {
                var item = _model.SelectedItem;
                detailTooltip?.Show(item != null ? item.displayName.ToUpperInvariant() : "NO ITEMS", item != null ? "" : "NOTHING TO USE");
                return;
            }

            detailTooltip?.Hide();
        }

        private void RefreshHitBadge()
        {
            if (hitBadge == null || _model == null)
            {
                return;
            }

            bool spellSelected = _model.SelectedSpellId != null;
            bool basicAllowed = showHitChanceForBasicAttack && _aim.isBasicAttack;
            bool open = _model.State != MenuState.Locked && _model.State != MenuState.FleeConfirm;

            if (open && _aim.target != null && (spellSelected || basicAllowed))
            {
                hitBadge.Show(_aim.hitChancePercent);
            }
            else
            {
                hitBadge.Hide();
            }
        }

        /// <summary>Tells the runner to ignore commit input and movement while a modal is open, so dismissing it cannot also attack.</summary>
        private void SetSuspended(bool suspended)
        {
            if (_suspended == suspended || _input == null)
            {
                return;
            }

            _suspended = suspended;
            _input.SubmitCommand(suspended ? PlayerCommand.SuspendActions : PlayerCommand.ResumeActions);
        }

        // ------------------------------------------------------------------ helpers

        private static List<MenuEntry> BuildSpellEntries(CombatantRuntime combatant)
        {
            var entries = new List<MenuEntry>();
            // Convention: the first known skill is the Basic Attack; everything after it is a spell.
            foreach (var id in combatant.skillIds.Skip(1))
            {
                var skill = DatabaseRegistry.GetSkill(id);
                if (skill == null)
                {
                    continue;
                }

                entries.Add(new MenuEntry
                {
                    id = skill.id,
                    displayName = skill.displayName,
                    cost = skill.apCost,
                    enabled = !combatant.usesAp || combatant.currentAp >= skill.apCost
                });
            }

            return entries;
        }

        private void RefreshHeroResources(CombatantRuntime hero)
        {
            if (apPips == null)
            {
                return;
            }

            apPips.gameObject.SetActive(hero.usesAp);
            if (hero.usesAp)
            {
                apPips.SetPips(hero.currentAp, hero.maxAp);
            }
        }

        /// <summary>With nothing aimed at, the enemy card rests on the first living enemy in the turn order.</summary>
        private void ShowDefaultEnemy()
        {
            if (enemyCard == null)
            {
                return;
            }

            var next = _lastOrder.FirstOrDefault(c => !c.isPlayerSide && !c.IsDefeated)
                ?? (_acting != null && !_acting.isPlayerSide && !_acting.IsDefeated ? _acting : null);

            enemyCard.Show(next);
            enemyCard.SetEmphasis(false);
        }

        private void SpawnFloatingNumber(DamageRollInfo info)
        {
            if (floatingNumbers == null || info == null || info.target == null || info.target.viewTransform == null || uiRoot == null)
            {
                return;
            }

            var camera = ResolveArenaCamera(info.target);
            if (camera == null)
            {
                return;
            }

            var screen = camera.WorldToScreenPoint(info.target.viewTransform.position + Vector3.up * 1.8f);
            if (screen.z <= 0f)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(uiRoot, screen, null, out var local))
            {
                return;
            }

            var position = new Vector2(local.x, -local.y);
            if (!info.hit || info.effectNegated)
            {
                floatingNumbers.SpawnMiss(position);
            }
            else if (info.damage > 0)
            {
                floatingNumbers.SpawnNumber(position, info.damage, heal: false);
            }
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
        /// Camera.main is ambiguous during battle (SampleScene's Main Camera stays enabled next to the arena's Battle Camera, both tagged
        /// MainCamera), so take the camera from the scene the combatant's view was spawned into instead — the same lookup the runner uses.
        /// </summary>
        private Camera ResolveArenaCamera(CombatantRuntime combatant)
        {
            if (_arenaCamera != null)
            {
                return _arenaCamera;
            }

            if (combatant != null && combatant.viewTransform != null)
            {
                foreach (var rootObject in combatant.viewTransform.gameObject.scene.GetRootGameObjects())
                {
                    var sceneCamera = rootObject.GetComponentInChildren<Camera>();
                    if (sceneCamera != null)
                    {
                        _arenaCamera = sceneCamera;
                        return _arenaCamera;
                    }
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
