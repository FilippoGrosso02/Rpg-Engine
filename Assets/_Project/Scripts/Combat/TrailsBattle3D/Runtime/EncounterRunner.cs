using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Combat.Contracts;
using Game.Common;
using Game.Common.Area;
using Game.Data.Definitions;
using Game.Data.Enums;
using Game.Data.Registry;
using Game.Data.Rules;
using Game.Data.State;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Implementation A behind IEncounterRunner. Loads BattleArena_01 additively, spawns
    /// combatants at its formation markers, runs the Duelwright SPD-ordered round loop to a
    /// win/loss, reports an EncounterResult, and unloads the scene. Everything outside this
    /// assembly only ever talks to it through IEncounterRunner/ICombatEvents/IPlayerCommandInput
    /// plus the plain Contracts data types — a UI layer never needs to know this class exists.
    /// </summary>
    public class EncounterRunner : MonoBehaviour, IEncounterRunner, ICombatEvents, IPlayerCommandInput
    {
        private const string BattleSceneName = "BattleArena_01";
        private const int MaxTurns = 500;
        private const float MoveSpeed = 6f;
        private const float MaxReactionWait = 0.6f;
        private const float DefeatWait = 0.9f;
        private const float ResultReadHold = 2f; // after impact; the damage dice animation (about 1.7 s) started at the click, so this leaves time to read the number
        private const float DefeatHideDelay = 0.8f;

        private readonly Dictionary<CombatantRuntime, List<AnimationAction>> _weaponAttackAnimations = new Dictionary<CombatantRuntime, List<AnimationAction>>();
        private readonly HashSet<CombatantRuntime> _dyingViews = new HashSet<CombatantRuntime>();
        private const int TurnOrderPreviewCount = 6;

        [Tooltip("Both sides use 'attack nearest' AI — set by the PlayMode smoke test to run headless.")]
        public bool autoPilotAllSides;

        /// <summary>
        /// Set by CombatSystemConfig when a HUD is actually bound, so the result panel has
        /// time to be read before the arena unloads. Left false (default) for direct/bare
        /// use — e.g. the PlayMode smoke test — so nothing waits on a "Continue" click that
        /// will never come.
        /// </summary>
        [field: SerializeField]
        public bool WaitForResultAcknowledgementBeforeUnload { get; set; }

        public event Action<CombatantRuntime> OnTurnStarted;
        public event Action<CombatantRuntime> OnCombatantStateChanged;
        public event Action<CombatantRuntime> OnCombatantDefeated;
        public event Action<IReadOnlyList<CombatantRuntime>> OnTurnOrderChanged;
        public event Action<EncounterResult> OnEncounterEnded;
        public event Action<CombatantRuntime> OnHoverChanged;
        public event Action<DamageRollInfo> OnDamageRolled;
        public event Action<AimReadout> OnAimChanged;
        public event Action OnPlayerActionCommitted;

        private List<CombatantRuntime> _combatants;
        private Dictionary<CombatantRuntime, GameObject> _views;
        private Scene _battleScene;
        private BattleArenaSpawnPoints _spawnPoints;
        private Action<EncounterResult> _onComplete;
        private EncounterDefinition _encounter;
        private TurnScheduler _scheduler;
        private CombatantRuntime _lastHovered;

        private CombatantRuntime _currentPlayerActor;
        private PlayerCommand? _pendingCommand;
        private SkillDefinition _pendingSkill;
        private bool _resultAcknowledged;
        private AreaIndicator _movementArea;
        private AreaIndicator _attackRangeArea;
        private AreaIndicator _aoeArea;
        private HoverHighlightRing _hoverHighlightRing;
        private MovementPathPreview _movementPathPreview;
        private Vector3 _turnStartPosition;
        private NavMeshPath _sharedMovePath;

        // Third-person control: camera rig in the arena scene, cursor lock state, WASD walking animation flag.
        private BattleCameraRig _cameraRig;
        private CombatantRuntime _cameraFocus;
        private bool _manualMoving;
        private bool _actionsSuspended;
        private bool _fleeRequested;
        private bool _encounterFled;
        private CombatantRuntime _lastAimTarget;
        private int _lastAimChance = -1;
        private bool _lastAimBasic;
        private string _lastAimDamageText;
        private bool _encounterOver;
        private bool _cursorStateSaved;
        private CursorLockMode _savedLockState;
        private bool _savedCursorVisible;
        private int _appliedCursorFree = -1;

        private static readonly Color AllyHighlightColor = new Color(0.35f, 0.55f, 0.95f);
        private static readonly Color EnemyHighlightColor = new Color(0.85f, 0.35f, 0.35f);

        public void StartEncounter(EncounterDefinition encounter, Action<EncounterResult> onComplete)
        {
            _encounter = encounter;
            _onComplete = onComplete;
            _encounterOver = false;
            _actionsSuspended = false;
            _fleeRequested = false;
            _encounterFled = false;
            _appliedCursorFree = -1;
            if (!_cursorStateSaved)
            {
                _savedLockState = Cursor.lockState;
                _savedCursorVisible = Cursor.visible;
                _cursorStateSaved = true;
            }

            StartCoroutine(RunEncounter());
        }

        private void OnDestroy()
        {
            RestoreCursor();
        }

        /// <summary>
        /// The battle UI is keyboard-only and the mouse only aims, so the cursor is locked and hidden for the whole fight. It is
        /// freed once the encounter ends so the result panel's Continue button can also be clicked. Skipped when autoPiloting
        /// (headless tests).
        /// </summary>
        private void UpdateCursorMode()
        {
            if (autoPilotAllSides || (_encounterOver && !_cursorStateSaved))
            {
                return;
            }

            bool wantFree = _encounterOver;
            _cameraRig?.SetLookEnabled(!wantFree);

            int applied = wantFree ? 1 : 0;
            if (applied == _appliedCursorFree)
            {
                return;
            }

            _appliedCursorFree = applied;
            Cursor.lockState = wantFree ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = wantFree;
        }

        private void RestoreCursor()
        {
            if (!_cursorStateSaved)
            {
                return;
            }

            Cursor.lockState = _savedLockState;
            Cursor.visible = _savedCursorVisible;
            _cursorStateSaved = false;
        }

        /// <summary>
        /// Points the battle camera at <paramref name="actor"/>. When the focus actually changes it also swings
        /// round to look towards the nearest opponent, so each new turn starts facing the fight; taking
        /// consecutive turns keeps whatever angle the player chose. Enemy turns never call this, so the camera
        /// stays on the last ally.
        /// </summary>
        private void FocusCameraOn(CombatantRuntime actor, List<CombatantRuntime> opponents)
        {
            if (_cameraRig == null || actor == null)
            {
                return;
            }

            if (_views.TryGetValue(actor, out var go) && go != null)
            {
                var view = go.GetComponent<CombatantView>();
                _cameraRig.Follow(view != null && view.CameraTarget != null ? view.CameraTarget : go.transform);
            }

            if (_cameraFocus != actor && opponents != null && opponents.Count > 0)
            {
                var nearest = opponents.OrderBy(o => Vector3.Distance(o.position, actor.position)).First();
                _cameraRig.FaceTowards(actor.position, nearest.position);
            }

            _cameraFocus = actor;
        }

        /// <summary>
        /// WASD walking, relative to the camera, spent from the same per-turn budget as everything else: the
        /// actor may stray at most moveRange from where the turn began (that circle is what the movement ring shows).
        /// </summary>
        private void UpdateManualMovement(CombatantRuntime actor, TargetingInput targeting)
        {
            bool moving = false;
            var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            if (input.sqrMagnitude > 0.01f && targeting != null && targeting.arenaCamera != null)
            {
                var cameraTransform = targeting.arenaCamera.transform;
                var forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                var right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
                var direction = forward * input.y + right * input.x;
                if (direction.sqrMagnitude > 1f)
                {
                    direction.Normalize();
                }

                if (MovementController.TryStep(actor.position, direction * (MoveSpeed * Time.deltaTime), _turnStartPosition, EffectiveMoveRange(actor), out var next))
                {
                    actor.position = next;
                    SyncViewPosition(actor);
                    moving = true;

                    if (_views.TryGetValue(actor, out var go) && go != null && direction.sqrMagnitude > 0.0001f)
                    {
                        go.transform.rotation = Quaternion.RotateTowards(
                            go.transform.rotation, Quaternion.LookRotation(direction), 720f * Time.deltaTime);
                    }
                }
            }

            if (moving != _manualMoving)
            {
                _manualMoving = moving;
                GetAnimator(actor)?.SetSpeed(moving ? 1f : 0f);
            }
        }

        /// <summary>
        /// Polls the hover raycast every frame regardless of whose turn it is (unlike the
        /// ring/path-preview visuals in UpdateHoverPreview, which only make sense during the
        /// player's own turn) — so a hover-stats panel can work any time, including while
        /// watching an enemy's turn play out.
        /// </summary>
        private void Update()
        {
            if (_combatants == null)
            {
                return;
            }

            UpdateCursorMode();

            var targeting = GetComponent<TargetingInput>();
            if (targeting == null)
            {
                return;
            }

            CombatantRuntime hovered = null;
            if (targeting.TryRaycastCombatant(out var view) && view.Runtime != null && !view.Runtime.IsDefeated)
            {
                hovered = view.Runtime;
            }

            if (hovered != _lastHovered)
            {
                _lastHovered = hovered;
                OnHoverChanged?.Invoke(hovered);
            }
        }

        /// <summary>
        /// Called by UI code (via IPlayerCommandInput) when the player picks a command from
        /// the menu, or dismisses the post-encounter result panel.
        /// </summary>
        public void SubmitCommand(PlayerCommand command, string skillId = null)
        {
            if (command == PlayerCommand.ContinueAfterResult)
            {
                _resultAcknowledged = true;
                return;
            }

            if (command == PlayerCommand.SuspendActions)
            {
                _actionsSuspended = true;
                return;
            }

            if (command == PlayerCommand.ResumeActions)
            {
                _actionsSuspended = false;
                return;
            }

            if (_currentPlayerActor == null)
            {
                return;
            }

            if (command == PlayerCommand.Flee)
            {
                _fleeRequested = true;
                return;
            }

            if (command == PlayerCommand.CancelCommand)
            {
                _pendingCommand = null;
                _pendingSkill = null;
                return;
            }

            _pendingCommand = command;
            _pendingSkill = command switch
            {
                PlayerCommand.Attack => GetFirstUsableSkill(_currentPlayerActor),
                PlayerCommand.Craft => !string.IsNullOrEmpty(skillId) ? DatabaseRegistry.GetSkill(skillId) : null,
                _ => null
            };
        }

        private IEnumerator RunEncounter()
        {
            yield return SceneManager.LoadSceneAsync(BattleSceneName, LoadSceneMode.Additive);

            _battleScene = SceneManager.GetSceneByName(BattleSceneName);
            _spawnPoints = FindSpawnPoints(_battleScene);
            BindArenaCamera();

            _combatants = new List<CombatantRuntime>();
            _views = new Dictionary<CombatantRuntime, GameObject>();
            _dyingViews.Clear();
            _weaponAttackAnimations.Clear();

            SpawnParty(ResolvePartyIds(_encounter));
            SpawnEnemies(_encounter.enemyIds);

            // Start on the first ally (looking at the nearest enemy) so the camera is sensible even if an
            // enemy acts first; from then on it only moves when an ally's turn begins.
            _cameraFocus = null;
            var firstAlly = _combatants.FirstOrDefault(c => c.isPlayerSide);
            FocusCameraOn(firstAlly, _combatants.Where(c => !c.isPlayerSide).ToList());

            _scheduler = new TurnScheduler(_combatants);
            RaiseTurnOrderChanged();

            EncounterResult result = CheckWinLoss();
            int turnCount = 0;

            while (result == null && turnCount < MaxTurns)
            {
                turnCount++;

                var actor = _scheduler.AdvanceToNextActor();
                if (actor == null)
                {
                    break;
                }

                // Guard ends at the start of the unit's own turn, and a caster gains its AP.
                ActionResolver.BeginTurn(actor);

                OnTurnStarted?.Invoke(actor);

                if (!actor.IsDefeated && !StatusEffectRuntime.IsActionPrevented(actor))
                {
                    yield return ProcessTurn(actor);
                    UpdateViewPositions();
                }

                // Statuses (a web, poison...) count down at the end of the unit's own turn.
                var hpBeforeTick = actor.currentHp;
                StatusEffectRuntime.TickAndExpire(actor);
                RaiseStateChanged(actor);
                if (hpBeforeTick > 0 && actor.IsSlain)
                {
                    OnCombatantDefeated?.Invoke(actor);
                    ResolveMorale();
                }

                if (_encounterFled)
                {
                    result = new EncounterResult { victory = false, fled = true };
                    break;
                }

                RaiseTurnOrderChanged();
                result = CheckWinLoss();
            }

            if (result == null)
            {
                result = BuildTimeoutResult();
            }

            if (result.victory || result.fled)
            {
                SavePartyState();
            }

            if (result.victory)
            {
                // The grimoire learns every monster type the party just beat.
                foreach (var enemy in _combatants.Where(c => !c.isPlayerSide && c.IsDefeated))
                {
                    Bestiary.RecordDefeat(enemy.definitionId);
                }
            }

            // Free the cursor so the result panel can be clicked.
            _encounterOver = true;
            OnEncounterEnded?.Invoke(result);

            if (WaitForResultAcknowledgementBeforeUnload)
            {
                _resultAcknowledged = false;
                yield return new WaitUntil(() => _resultAcknowledged);
            }

            CleanupViews();
            _cameraRig = null;
            yield return SceneManager.UnloadSceneAsync(_battleScene);
            RestoreCursor();

            _onComplete?.Invoke(result);
        }

        private IEnumerator ProcessTurn(CombatantRuntime actor)
        {
            var opponents = _combatants.Where(c => !c.IsDefeated && c.isPlayerSide != actor.isPlayerSide).ToList();
            if (opponents.Count == 0)
            {
                yield break;
            }

            // Anchor for this turn's whole move budget (MoveWithinRange/free ground clicks both
            // clamp against this, not the actor's live position) — set here, once, for both the
            // AI and player paths, rather than only inside ResolvePlayerTurn.
            _turnStartPosition = actor.position;

            if (!actor.isPlayerSide || autoPilotAllSides)
            {
                yield return ResolveAiTurn(actor, opponents);
            }
            else
            {
                FocusCameraOn(actor, opponents);
                yield return ResolvePlayerTurn(actor, opponents);
            }
        }

        private IEnumerator ResolveAiTurn(CombatantRuntime actor, List<CombatantRuntime> opponents)
        {
            var (skill, target) = ChooseAiAction(actor, opponents);
            if (skill == null || target == null)
            {
                yield break;
            }

            var attackRange = AttackRangeFor(actor);
            yield return MoveWithinRange(actor, target.position, attackRange);
            if (Vector3.Distance(actor.position, target.position) > attackRange + 0.1f)
            {
                // Couldn't close the distance (e.g. path blocked, webbed, or out of move range) — the action fizzles this turn.
                yield break;
            }

            yield return PerformSkill(actor, skill, target);
        }

        /// <summary>Actual attack distance in world units: attackRangeMultiplier * RangeUnit.
        /// A multiplier of 1 (melee) gives RangeUnit reach; ranged combatants use a higher
        /// multiplier instead of the old unlimited-range Ranged trait.</summary>
        /// <summary>Move range right now: 0 while webbed (or otherwise held in place), else the combatant's own.</summary>
        private static float EffectiveMoveRange(CombatantRuntime combatant)
        {
            return StatusEffectRuntime.IsMovementPrevented(combatant) ? 0f : combatant.moveRange;
        }

        private static float AttackRangeFor(CombatantRuntime combatant)
        {
            return combatant.attackRangeMultiplier * ActionResolver.RangeUnit;
        }

        /// <summary>
        /// A simple, deterministic AI policy: a caster uses the first spell it can afford that targets enemies (so a wizard on
        /// autopilot actually casts), everything else attacks the nearest opponent with its Basic Attack.
        /// </summary>
        private (SkillDefinition skill, CombatantRuntime target) ChooseAiAction(CombatantRuntime actor, List<CombatantRuntime> opponents)
        {
            var nearestOpponent = opponents.OrderBy(o => Vector3.Distance(actor.position, o.position)).First();

            for (int i = 1; i < actor.skillIds.Count; i++)
            {
                var spell = DatabaseRegistry.GetSkill(actor.skillIds[i]);
                if (spell == null || actor.currentAp < spell.apCost)
                {
                    continue;
                }

                switch (spell.targetType)
                {
                    case TargetType.SingleEnemy:
                    case TargetType.AllEnemies:
                    case TargetType.AreaCircle:
                    case TargetType.AreaCone:
                    case TargetType.AreaLine:
                        // Prefer the most expensive spell it can afford: build up AP, then let it go.
                        return (BestAffordableSpell(actor) ?? spell, nearestOpponent);
                }
            }

            return (GetFirstUsableSkill(actor), nearestOpponent);
        }

        private static SkillDefinition BestAffordableSpell(CombatantRuntime actor)
        {
            SkillDefinition best = null;
            for (int i = 1; i < actor.skillIds.Count; i++)
            {
                var spell = DatabaseRegistry.GetSkill(actor.skillIds[i]);
                if (spell == null || actor.currentAp < spell.apCost || IsAllyTargetedSkill(spell) || spell.targetType == TargetType.Self)
                {
                    continue;
                }

                if (best == null || spell.apCost > best.apCost)
                {
                    best = spell;
                }
            }

            return best;
        }

        /// <summary>
        /// Waits for the command menu (via SubmitCommand) to pick an action, then a 3D click
        /// (via TargetingInput, unchanged from the combat-loop phase) to pick who it hits.
        /// The menu only decides *which* action — targeting still happens entirely by
        /// clicking in the arena. A ground click is always free to reposition the actor
        /// within its movement ring, whether or not a command has been chosen yet.
        /// </summary>
        private IEnumerator ResolvePlayerTurn(CombatantRuntime actor, List<CombatantRuntime> opponents)
        {
            _currentPlayerActor = actor;
            _pendingCommand = null;
            _pendingSkill = null;

            EnsureRangeAreas();
            EnsureHoverVisuals();
            _movementArea.ShowCircle(_turnStartPosition, EffectiveMoveRange(actor));

            var targeting = GetComponent<TargetingInput>();
            if (targeting != null)
            {
                // The crosshair ray starts behind the actor, so it must not hit the actor's own body.
                targeting.ignoredView = _views.TryGetValue(actor, out var actorGo) && actorGo != null
                    ? actorGo.GetComponent<CombatantView>()
                    : null;
            }

            _manualMoving = false;

            while (true)
            {
                if (!_actionsSuspended)
                {
                    UpdateManualMovement(actor, targeting);
                }

                UpdateHoverPreview(actor, targeting);
                UpdateAttackRangeArea(actor, targeting);
                UpdateAoePreview(actor, targeting);
                UpdateAimReadout(actor, targeting);

                if (_fleeRequested)
                {
                    _fleeRequested = false;
                    _encounterFled = true;
                    OnPlayerActionCommitted?.Invoke();
                    break;
                }

                if (_pendingCommand == PlayerCommand.Guard)
                {
                    OnPlayerActionCommitted?.Invoke();
                    ActionResolver.ApplyGuard(actor);
                    RaiseStateChanged(actor);
                    break;
                }

                // Commit with left click or Space. While a UI prompt is open (the flee confirmation) neither does anything.
                if (targeting != null && !_actionsSuspended && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)))
                {
                    // Heal / revive: pick among allies on screen (defeated allies are hidden and have no
                    // collider, so this is a screen-proximity pick rather than a raycast). A click that
                    // misses every ally falls through to ground movement below.
                    if (IsAllyTargetedSkill(_pendingSkill)
                        && targeting.TryPickNearestOnScreen(AllyTargetCandidates(actor, _pendingSkill), out var ally))
                    {
                        if (actor.currentAp >= _pendingSkill.apCost)
                        {
                            var allySkill = _pendingSkill;
                            var allyRange = AttackRangeFor(actor);
                            yield return MoveWithinRange(actor, ally.position, allyRange);
                            if (Vector3.Distance(actor.position, ally.position) <= allyRange + 0.1f)
                            {
                                OnPlayerActionCommitted?.Invoke();
                                yield return PerformSkill(actor, allySkill, ally);
                                break;
                            }
                        }
                    }
                    else if (!IsAllyTargetedSkill(_pendingSkill)
                        && targeting.TryRaycastCombatant(out var view)
                        && view.Runtime != null
                        && !view.Runtime.IsDefeated
                        && view.Runtime.isPlayerSide != actor.isPlayerSide)
                    {
                        // Clicking an enemy always attacks — a Craft chosen from the menu
                        // first takes priority, otherwise it falls back to the Basic Attack
                        // so no menu step is needed for the common case.
                        var skillToUse = _pendingSkill ?? GetFirstUsableSkill(actor);

                        if (skillToUse != null && actor.currentAp >= skillToUse.apCost)
                        {
                            var attackRange = AttackRangeFor(actor);
                            yield return MoveWithinRange(actor, view.Runtime.position, attackRange);
                            bool canAct = Vector3.Distance(actor.position, view.Runtime.position) <= attackRange + 0.1f;

                            if (canAct)
                            {
                                OnPlayerActionCommitted?.Invoke();
                                yield return PerformSkill(actor, skillToUse, view.Runtime);
                                break;
                            }
                        }
                    }
                }

                yield return null;
            }

            if (_manualMoving)
            {
                _manualMoving = false;
                GetAnimator(actor)?.SetSpeed(0f);
            }

            _movementArea.Hide();
            _attackRangeArea.Hide();
            _aoeArea.Hide();
            _hoverHighlightRing.Hide();
            _movementPathPreview.Hide();

            ClearAimReadout();
            _currentPlayerActor = null;
            _pendingCommand = null;
            _pendingSkill = null;
        }

        /// <summary>
        /// Tells the HUD which living enemy the reticle is on and the chance the current action (the selected spell, or the Basic
        /// Attack) works on it. Fires only when the target or the chance actually changes.
        /// </summary>
        private void UpdateAimReadout(CombatantRuntime actor, TargetingInput targeting)
        {
            CombatantRuntime aimed = null;
            int chance = 0;
            bool basic = false;
            string damageText = null;

            if (targeting != null
                && !_actionsSuspended
                && targeting.TryRaycastCombatant(out var view)
                && view.Runtime != null
                && !view.Runtime.IsDefeated
                && view.Runtime.isPlayerSide != actor.isPlayerSide
                && !IsAllyTargetedSkill(_pendingSkill))
            {
                aimed = view.Runtime;
                var skill = _pendingSkill ?? GetFirstUsableSkill(actor);
                basic = _pendingSkill == null;
                if (skill != null)
                {
                    chance = ActionResolver.GetHitChancePercent(actor, aimed, skill);
                    damageText = (skill.usesWeaponDamage ? actor.attackDamage : skill.damage).ToString();
                }
            }

            if (aimed == _lastAimTarget && chance == _lastAimChance && basic == _lastAimBasic && damageText == _lastAimDamageText)
            {
                return;
            }

            _lastAimTarget = aimed;
            _lastAimChance = chance;
            _lastAimBasic = basic;
            _lastAimDamageText = damageText;
            OnAimChanged?.Invoke(aimed == null
                ? AimReadout.None
                : new AimReadout { target = aimed, hitChancePercent = chance, isBasicAttack = basic, damageText = damageText });
        }

        private void ClearAimReadout()
        {
            if (_lastAimTarget == null && _lastAimChance < 0)
            {
                return;
            }

            _lastAimTarget = null;
            _lastAimChance = -1;
            _lastAimBasic = false;
            _lastAimDamageText = null;
            OnAimChanged?.Invoke(AimReadout.None);
        }

        /// <summary>
        /// Continuous (every-frame, not just on click) feedback for whatever the targeting
        /// ray currently points at: a colored ring under a hovered combatant, or the NavMesh
        /// path + destination marker for a hovered ground point.
        /// </summary>
        private void UpdateHoverPreview(CombatantRuntime actor, TargetingInput targeting)
        {
            if (targeting == null)
            {
                return;
            }

            if (IsAllyTargetedSkill(_pendingSkill))
            {
                _movementPathPreview.Hide();
                if (targeting.TryPickNearestOnScreen(AllyTargetCandidates(actor, _pendingSkill), out var hoveredAlly))
                {
                    _hoverHighlightRing.ShowOn(hoveredAlly.viewTransform, AllyHighlightColor);
                }
                else
                {
                    _hoverHighlightRing.Hide();
                }
                return;
            }

            if (targeting.TryRaycastCombatant(out var hoveredView)
                && hoveredView.Runtime != null
                && !hoveredView.Runtime.IsDefeated)
            {
                var color = hoveredView.Runtime.isPlayerSide ? AllyHighlightColor : EnemyHighlightColor;
                _hoverHighlightRing.ShowOn(hoveredView.Runtime.viewTransform, color);
                _movementPathPreview.Hide();
                return;
            }

            _hoverHighlightRing.Hide();

            // Sample the raw hovered point onto the NavMesh (not clamped toward the actor)
            // so an out-of-range destination can still be shown — in red — rather than
            // silently snapping to the clamped point the player didn't actually point at.
            // The click-to-move path preview only applies while the cursor is free; with the cursor locked
            // you walk with WASD, so a "move here" marker under the crosshair would be misleading.
            if (!targeting.CursorLocked
                && targeting.TryRaycastGround(out var point)
                && NavMesh.SamplePosition(point, out var navHit, EffectiveMoveRange(actor) + 5f, NavMesh.AllAreas))
            {
                _movementPathPreview.Show(actor.position, navHit.position, _turnStartPosition, EffectiveMoveRange(actor));
            }
            else
            {
                _movementPathPreview.Hide();
            }
        }

        private static bool IsAllyTargetedSkill(SkillDefinition skill)
        {
            return skill != null && skill.targetType == TargetType.SingleAlly;
        }

        /// <summary>Who a single-ally skill can be aimed at: fallen allies for a revive, living allies (self included) otherwise.</summary>
        private IEnumerable<CombatantRuntime> AllyTargetCandidates(CombatantRuntime actor, SkillDefinition skill)
        {
            return _combatants.Where(c => c.isPlayerSide == actor.isPlayerSide && !c.IsDefeated);
        }

        private static SkillDefinition GetFirstUsableSkill(CombatantRuntime actor)
        {
            // Convention: the first known skill is the actor's Basic Attack — matches what
            // the command menu's "Attack" button and the AI both use. Everything after it is
            // a selectable Craft (see CommandMenuController.GetCraftSkillIds).
            var skillId = actor.skillIds.FirstOrDefault();
            return skillId != null ? DatabaseRegistry.GetSkill(skillId) : null;
        }

        /// <summary>
        /// Approaches <paramref name="targetPosition"/> until within <paramref name="desiredRange"/>,
        /// spending from the same per-turn move budget as free ground-click movement — clamped
        /// against _turnStartPosition (not the actor's current, possibly-already-moved
        /// position). Anchoring to the current position instead would hand out a second, full
        /// moveRange budget for the approach on top of whatever a free-move click already
        /// spent this turn, letting a unit travel roughly double its actual move range.
        /// </summary>
        private IEnumerator MoveWithinRange(CombatantRuntime actor, Vector3 targetPosition, float desiredRange)
        {
            var distance = Vector3.Distance(actor.position, targetPosition);
            if (distance <= desiredRange)
            {
                yield break;
            }

            var directionToTarget = (targetPosition - actor.position).normalized;
            var moveGoal = targetPosition - directionToTarget * desiredRange;

            if (MovementController.TryGetMovePosition(_turnStartPosition, moveGoal, EffectiveMoveRange(actor), out var newPosition))
            {
                yield return MoveAlongPath(actor, newPosition);
            }
        }

        /// <summary>
        /// Walks the actor along the NavMesh path to <paramref name="destination"/> over
        /// several frames instead of teleporting — used for every movement in an encounter
        /// (free repositioning, the auto-approach before a Craft/Attack lands, and enemy AI
        /// closing distance) so combat doesn't read as units snapping around instantly.
        /// </summary>
        private IEnumerator MoveAlongPath(CombatantRuntime actor, Vector3 destination)
        {
            _sharedMovePath ??= new NavMeshPath();
            Vector3[] waypoints;

            if (NavMesh.CalculatePath(actor.position, destination, NavMesh.AllAreas, _sharedMovePath)
                && _sharedMovePath.corners.Length > 0)
            {
                waypoints = _sharedMovePath.corners;
            }
            else
            {
                waypoints = new[] { actor.position, destination };
            }

            _views.TryGetValue(actor, out var moveView);
            var moveAnimator = moveView != null ? moveView.GetComponent<CombatantAnimator>() : null;
            moveAnimator?.SetSpeed(1f);

            for (int i = 1; i < waypoints.Length; i++)
            {
                var from = waypoints[i - 1];
                var to = waypoints[i];
                var segmentDistance = Vector3.Distance(from, to);
                if (segmentDistance < 0.001f)
                {
                    continue;
                }

                var duration = segmentDistance / MoveSpeed;
                var elapsed = 0f;
                var flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
                var facing = flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat) : (Quaternion?)null;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    actor.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                    SyncViewPosition(actor);
                    if (facing.HasValue && moveView != null)
                    {
                        moveView.transform.rotation = Quaternion.RotateTowards(
                            moveView.transform.rotation, facing.Value, 720f * Time.deltaTime);
                    }
                    yield return null;
                }

                actor.position = to;
                SyncViewPosition(actor);
            }

            moveAnimator?.SetSpeed(0f);
        }

        /// <summary>
        /// Points TargetingInput at the arena's own camera instead of trusting its Awake-time
        /// Camera.main fallback. TargetingInput lives on this same GameObject, instantiated
        /// (and Awake-initialized) *before* the battle scene loads — at that instant
        /// Camera.main can only see SampleScene's base camera, since the arena's camera
        /// doesn't exist yet. Both cameras stay tagged MainCamera and enabled afterward, and
        /// since the arena camera has the higher depth (renders on top — what the player
        /// actually sees), a raycast built from the base camera's completely different
        /// position/angle would almost never land where the player is visually clicking.
        /// </summary>
        private void BindArenaCamera()
        {
            var targeting = GetComponent<TargetingInput>();
            if (targeting == null)
            {
                return;
            }

            foreach (var root in _battleScene.GetRootGameObjects())
            {
                if (_cameraRig == null)
                {
                    _cameraRig = root.GetComponentInChildren<BattleCameraRig>();
                }

                var camera = root.GetComponentInChildren<Camera>();
                if (camera != null)
                {
                    targeting.arenaCamera = camera;
                }
            }
        }

        private void EnsureRangeAreas()
        {
            // Range areas (reach, not impact): very light fill and a dashed edge, so they read as "you could go/hit here".
            if (_movementArea == null)
            {
                _movementArea = CreateAreaIndicator("MovementRangeArea", new Color(0.3f, 0.9f, 1f), fillAlpha: 0.05f, edgeAlpha: 0.8f, dashed: true);
            }

            if (_attackRangeArea == null)
            {
                _attackRangeArea = CreateAreaIndicator("AttackRangeArea", new Color(1f, 0.35f, 0.25f), fillAlpha: 0.05f, edgeAlpha: 0.8f, dashed: true);
            }

            // Area-of-effect preview (impact): stronger fill and a solid edge.
            if (_aoeArea == null)
            {
                _aoeArea = CreateAreaIndicator("AoePreviewArea", new Color(1f, 0.75f, 0.2f), fillAlpha: 0.22f, edgeAlpha: 0.95f, dashed: false);
            }
        }

        private AreaIndicator CreateAreaIndicator(string objectName, Color color, float fillAlpha, float edgeAlpha, bool dashed)
        {
            var areaObject = new GameObject(objectName);
            SceneManager.MoveGameObjectToScene(areaObject, _battleScene);
            var area = areaObject.AddComponent<AreaIndicator>();
            area.fillColor = new Color(color.r, color.g, color.b, fillAlpha);
            area.edgeColor = new Color(color.r, color.g, color.b, edgeAlpha);
            if (dashed)
            {
                area.edgeWidth = 0.07f;
                area.dashLength = 0.45f;
                area.dashGap = 0.3f;
            }

            return area;
        }

        /// <summary>
        /// While an area skill is selected and an enemy is hovered, shows exactly the shape that skill would hit
        /// (a circle on the target, or a cone / line from the caster towards it).
        /// </summary>
        private void UpdateAoePreview(CombatantRuntime actor, TargetingInput targeting)
        {
            if (_pendingSkill != null
                && targeting != null
                && ActionResolver.TryGetAreaShape(_pendingSkill, out var shape)
                && targeting.TryRaycastCombatant(out var view)
                && view.Runtime != null
                && !view.Runtime.IsDefeated
                && view.Runtime.isPlayerSide != actor.isPlayerSide)
            {
                ActionResolver.GetAreaFrame(_pendingSkill, actor.position, view.Runtime.position, out var origin, out var forward);
                _aoeArea.shape = shape;
                _aoeArea.Show(origin, forward);
            }
            else
            {
                _aoeArea.Hide();
            }
        }

        /// <summary>Red range circle around the acting unit while an attack is selected or an enemy is hovered, so reach is visible before committing.</summary>
        private void UpdateAttackRangeArea(CombatantRuntime actor, TargetingInput targeting)
        {
            bool show = _pendingSkill != null && !IsAllyTargetedSkill(_pendingSkill);

            if (!show
                && targeting != null
                && targeting.TryRaycastCombatant(out var hoveredView)
                && hoveredView.Runtime != null
                && !hoveredView.Runtime.IsDefeated
                && hoveredView.Runtime.isPlayerSide != actor.isPlayerSide)
            {
                show = true;
            }

            if (show)
            {
                _attackRangeArea.ShowCircle(actor.position, AttackRangeFor(actor));
            }
            else
            {
                _attackRangeArea.Hide();
            }
        }

        private void EnsureHoverVisuals()
        {
            if (_hoverHighlightRing == null)
            {
                var hoverObject = new GameObject("HoverHighlightRing");
                SceneManager.MoveGameObjectToScene(hoverObject, _battleScene);
                _hoverHighlightRing = hoverObject.AddComponent<HoverHighlightRing>();
            }

            if (_movementPathPreview == null)
            {
                var previewObject = new GameObject("MovementPathPreview");
                SceneManager.MoveGameObjectToScene(previewObject, _battleScene);
                _movementPathPreview = previewObject.AddComponent<MovementPathPreview>();
            }
        }

        private void SyncViewPosition(CombatantRuntime combatant)
        {
            if (_views.TryGetValue(combatant, out var view) && view != null)
            {
                view.transform.position = combatant.position;
            }
        }

        /// <summary>
        /// Plays the user's attack/skill animation, applies the skill's effects at the clip's
        /// impact moment, then plays hit / defeat reactions on whoever lost HP. Rules resolve in
        /// <see cref="RollSkill"/>/<see cref="ApplySkill"/> exactly as before - this only decides *when*. Missing clips
        /// degrade to an immediate resolve, so a turn can never stall on animation data.
        /// </summary>
        private IEnumerator PerformSkill(CombatantRuntime actor, SkillDefinition skill, CombatantRuntime primaryTarget)
        {
            // The dice are rolled the moment the attack starts, so the HUD's dice animation runs alongside the swing; HP only changes at impact.
            var rolls = RollSkill(actor, skill, primaryTarget);

            var animator = GetAnimator(actor);
            var action = animator != null ? ResolveActionAnimation(actor, animator, skill) : null;

            if (primaryTarget != null && primaryTarget != actor)
            {
                yield return FaceTowards(actor, primaryTarget.position);
            }

            float duration = 0f;
            float impact = 0f;
            if (action != null && action.HasClip)
            {
                duration = animator.Play(action);
                impact = Mathf.Min(action.ImpactTime, duration);
                if (impact > 0f)
                {
                    yield return new WaitForSeconds(impact);
                }
            }

            var hpBefore = new Dictionary<CombatantRuntime, int>();
            foreach (var c in _combatants)
            {
                hpBefore[c] = c.currentHp;
            }

            ApplySkill(actor, skill, rolls);

            float reactionWait = 0f;
            foreach (var c in _combatants)
            {
                if (c.currentHp >= hpBefore[c] || c.IsDefeated)
                {
                    continue; // defeat animation is started by SyncViewVisibility
                }

                var targetAnimator = GetAnimator(c);
                if (targetAnimator != null)
                {
                    reactionWait = Mathf.Max(reactionWait, Mathf.Min(targetAnimator.PlayHit(), MaxReactionWait));
                }
            }

            if (_dyingViews.Count > 0)
            {
                reactionWait = Mathf.Max(reactionWait, DefeatWait);
            }

            // Hold the turn long enough to read the damage roll and floating numbers, even when clips are missing or short.
            var resultHold = autoPilotAllSides ? 0f : ResultReadHold;
            var wait = Mathf.Max(duration - impact, reactionWait, resultHold);
            if (wait > 0f)
            {
                yield return new WaitForSeconds(wait);
            }
        }

        private CombatantAnimator GetAnimator(CombatantRuntime combatant)
        {
            return _views.TryGetValue(combatant, out var view) && view != null
                ? view.GetComponent<CombatantAnimator>()
                : null;
        }

        /// <summary>Skill clip first; Basic Attack then falls to the weapon's list and the set's
        /// combo, any other skill to the set's default skill animation.</summary>
        private AnimationAction ResolveActionAnimation(CombatantRuntime actor, CombatantAnimator animator, SkillDefinition skill)
        {
            if (skill.animation != null && skill.animation.HasClip)
            {
                return skill.animation;
            }

            bool isBasic = actor.skillIds.Count > 0 && skill.id == actor.skillIds[0];
            if (isBasic)
            {
                _weaponAttackAnimations.TryGetValue(actor, out var weaponClips);
                return animator.NextBasicAttack(weaponClips);
            }

            return animator.Set != null ? animator.Set.skillDefault : null;
        }

        private IEnumerator FaceTowards(CombatantRuntime actor, Vector3 worldPoint)
        {
            if (!_views.TryGetValue(actor, out var view) || view == null)
            {
                yield break;
            }

            var flat = new Vector3(worldPoint.x - actor.position.x, 0f, worldPoint.z - actor.position.z);
            if (flat.sqrMagnitude < 0.0001f)
            {
                yield break;
            }

            var goal = Quaternion.LookRotation(flat);
            while (view != null && Quaternion.Angle(view.transform.rotation, goal) > 1f)
            {
                view.transform.rotation = Quaternion.RotateTowards(view.transform.rotation, goal, 720f * Time.deltaTime);
                yield return null;
            }
        }

        /// <summary>
        /// Rolls the skill against everyone it hits and announces each result right away (the HUD starts its dice animation), without
        /// touching HP yet - <see cref="ApplySkill"/> does that at the animation's impact moment.
        /// </summary>
        private List<DamageRollInfo> RollSkill(CombatantRuntime actor, SkillDefinition skill, CombatantRuntime primaryTarget)
        {
            var rolls = new List<DamageRollInfo>();
            foreach (var target in ActionResolver.ResolveTargets(skill, actor, primaryTarget, _combatants))
            {
                var rollInfo = ActionResolver.ResolveAttack(actor, target, skill);
                rolls.Add(rollInfo);
                OnDamageRolled?.Invoke(rollInfo);
            }

            return rolls;
        }

        private void ApplySkill(CombatantRuntime actor, SkillDefinition skill, List<DamageRollInfo> rolls)
        {
            actor.currentAp = Mathf.Max(0, actor.currentAp - Mathf.Max(0, skill.apCost));

            foreach (var rollInfo in rolls)
            {
                var target = rollInfo.target;
                var wasAlive = !target.IsDefeated;

                if (rollInfo.damage > 0)
                {
                    target.currentHp = Mathf.Max(0, target.currentHp - rollInfo.damage);
                }

                ActionResolver.ApplyAcidEffects(rollInfo);

                if (rollInfo.hit && !rollInfo.effectNegated && !string.IsNullOrEmpty(skill.statusEffectId))
                {
                    StatusEffectRuntime.TryApply(target, skill.statusEffectId, skill.statusEffectChance);
                }

                RaiseStateChanged(target);

                if (wasAlive && target.IsSlain)
                {
                    OnCombatantDefeated?.Invoke(target);
                }
            }

            RaiseStateChanged(actor); // AP spent, armor or weapon corroded
            ResolveMorale();
        }

        /// <summary>
        /// After anything that can thin a monster group, lets the monsters roll morale. A monster that flees can itself
        /// push the rest over the "half the group is down" line, so this repeats until nobody else runs.
        /// </summary>
        private void ResolveMorale()
        {
            bool anyFled;
            do
            {
                anyFled = false;
                foreach (var check in MoraleRuntime.CheckSide(_combatants))
                {
                    Debug.Log($"[Morale] {check.monster.displayName} ({check.trigger}): 2d6 rolled {check.roll} vs morale {check.morale} — {(check.fled ? "flees!" : "holds")}");

                    if (check.fled)
                    {
                        anyFled = true;
                        RaiseStateChanged(check.monster);
                        OnCombatantDefeated?.Invoke(check.monster);
                    }
                }
            }
            while (anyFled);
        }

        private void RaiseStateChanged(CombatantRuntime combatant)
        {
            SyncViewVisibility(combatant);
            OnCombatantStateChanged?.Invoke(combatant);
        }

        /// <summary>
        /// Hides a defeated combatant's model instead of destroying it — the GameObject (and
        /// its CombatantRuntime) stays alive so a future revival effect has a body to bring
        /// back. Driven purely by IsDefeated, so it self-heals in both directions: any HP
        /// change that crosses 0 (damage taking it there, or a later heal/revive skill
        /// bringing it back above 0) re-evaluates visibility with no extra bookkeeping.
        /// </summary>
        private void SyncViewVisibility(CombatantRuntime combatant)
        {
            if (!_views.TryGetValue(combatant, out var view) || view == null)
            {
                return;
            }

            var animator = view.GetComponent<CombatantAnimator>();

            if (!combatant.IsDefeated)
            {
                _dyingViews.Remove(combatant);
                if (!view.activeSelf)
                {
                    view.SetActive(true);
                }

                animator?.ClearAction();
                return;
            }

            if (combatant.hasFled && !combatant.IsSlain)
            {
                // A monster that fled the fight just leaves — no fall animation, nothing to revive.
                _dyingViews.Remove(combatant);
                view.SetActive(false);
                return;
            }

            if (!view.activeSelf || !_dyingViews.Add(combatant))
            {
                return;
            }

            // Play the fall, hold the pose briefly, then hide (the body is kept for revives).
            if (animator != null && animator.PlayDefeated() > 0f)
            {
                StartCoroutine(HideAfterDefeat(combatant, view));
            }
            else
            {
                _dyingViews.Remove(combatant);
                view.SetActive(false);
            }
        }

        private IEnumerator HideAfterDefeat(CombatantRuntime combatant, GameObject view)
        {
            yield return new WaitForSeconds(DefeatHideDelay + DefeatWait);
            if (view != null && combatant.IsDefeated)
            {
                view.SetActive(false);
            }

            _dyingViews.Remove(combatant);
        }

        private void RaiseTurnOrderChanged()
        {
            if (_scheduler == null)
            {
                return;
            }

            OnTurnOrderChanged?.Invoke(_scheduler.PreviewUpcomingOrder(TurnOrderPreviewCount));
        }

        /// <summary>The player's roster if one is set up (and the encounter doesn't force its own party); otherwise the encounter's list.</summary>
        private static List<string> ResolvePartyIds(EncounterDefinition encounter)
        {
            if (!encounter.useFixedParty && PartyRoster.Count > 0)
            {
                return new List<string>(PartyRoster.Members);
            }

            return encounter.partyActorIds;
        }

        private void SpawnParty(List<string> actorIds)
        {
            for (int i = 0; i < actorIds.Count; i++)
            {
                var actorDef = DatabaseRegistry.GetActor(actorIds[i]);
                if (actorDef == null)
                {
                    continue;
                }

                var spawnPoint = GetSpawnPoint(_spawnPoints != null ? _spawnPoints.partySpawnPoints : null, i);
                var runtime = BuildAllyRuntime(actorDef);
                runtime.position = spawnPoint != null ? spawnPoint.position : new Vector3(-i * 2f, 0f, -3f);

                _combatants.Add(runtime);
                var wornWeaponId = Loadout.GetWeaponId(actorDef);
                if (!string.IsNullOrEmpty(wornWeaponId))
                {
                    var weapon = DatabaseRegistry.GetWeapon(wornWeaponId);
                    if (weapon != null && weapon.basicAttackAnimations != null && weapon.basicAttackAnimations.Count > 0)
                    {
                        _weaponAttackAnimations[runtime] = weapon.basicAttackAnimations;
                    }
                }

                SpawnView(actorDef.battleModelPrefab, runtime, actorDef.animationSet);
            }
        }

        private void SpawnEnemies(List<string> enemyIds)
        {
            for (int i = 0; i < enemyIds.Count; i++)
            {
                var enemyDef = DatabaseRegistry.GetEnemy(enemyIds[i]);
                if (enemyDef == null)
                {
                    continue;
                }

                var spawnPoint = GetSpawnPoint(_spawnPoints != null ? _spawnPoints.enemySpawnPoints : null, i);
                var runtime = BuildEnemyRuntime(enemyDef);
                runtime.position = spawnPoint != null ? spawnPoint.position : new Vector3(-i * 2f, 0f, 3f);

                _combatants.Add(runtime);
                SpawnView(enemyDef.battleModelPrefab, runtime, enemyDef.animationSet);
            }
        }

        private static Transform GetSpawnPoint(Transform[] points, int index)
        {
            if (points == null || points.Length == 0)
            {
                return null;
            }

            return points[index % points.Length];
        }

        /// <summary>
        /// Derives a hero's combat numbers from the d12 sheet: HP = hit die (max) + VIT modifier, AC = 6 + armor + DEX modifier,
        /// attack = STR (melee) or DEX (ranged) score, damage = weapon dice + STR modifier (bows excepted), initiative = DEX.
        /// </summary>
        public static CombatantRuntime BuildAllyRuntime(ActorDefinition actorDef)
        {
            var classDef = DatabaseRegistry.GetClass(actorDef.classId);
            int hitDie = classDef != null ? classDef.hitDieSides : 8;
            int maxHp = OsrRules.MaxHitPoints(hitDie, actorDef.vitality);

            int armorBonus = 0;
            foreach (var armorId in Loadout.GetArmorIds(actorDef))
            {
                var armor = DatabaseRegistry.GetArmor(armorId);
                if (armor != null)
                {
                    armorBonus += armor.acBonus;
                }
            }

            var wornWeaponId = Loadout.GetWeaponId(actorDef);
            var weapon = string.IsNullOrEmpty(wornWeaponId) ? null : DatabaseRegistry.GetWeapon(wornWeaponId);
            bool ranged = weapon != null && weapon.isRanged;
            var dice = weapon != null ? weapon.damage : new DiceRoll(1, 3, 0); // bare hands: d3
            int strengthBonus = weapon == null || weapon.addsStrengthModifier ? OsrRules.Modifier(actorDef.strength) : 0;

            var corrosion = PartyState.GetCorrosion(actorDef.id);

            return new CombatantRuntime
            {
                definitionId = actorDef.id,
                displayName = actorDef.displayName,
                roleLabel = actorDef.roleLabel,
                isPlayerSide = true,
                maxHp = maxHp,
                currentHp = PartyState.GetStartingHp(actorDef.id, maxHp),
                strength = actorDef.strength,
                dexterity = actorDef.dexterity,
                vitality = actorDef.vitality,
                mind = actorDef.mind,
                charm = actorDef.charm,
                baseArmorClass = OsrRules.ArmorClass(armorBonus),
                armorBonus = armorBonus,
                armorCorrosion = Mathf.Min(corrosion.armor, armorBonus),
                attackBonus = OsrRules.Modifier(ranged ? actorDef.dexterity : actorDef.strength),
                attackDamage = new DiceRoll(dice.count, dice.sides, dice.bonus + strengthBonus),
                weaponIsMetal = weapon != null && weapon.isMetal,
                weaponCorrosion = corrosion.weapon,
                saveVitality = actorDef.vitality,
                saveMind = actorDef.mind,
                initiative = actorDef.dexterity,
                usesAp = actorDef.usesAp,
                currentAp = actorDef.usesAp ? 1 : 0,
                moveRange = actorDef.moveRange,
                attackRangeMultiplier = actorDef.attackRangeMultiplier,
                skillIds = new List<string>(actorDef.startingSkillIds)
            };
        }

        public static CombatantRuntime BuildEnemyRuntime(EnemyDefinition enemyDef)
        {
            return new CombatantRuntime
            {
                definitionId = enemyDef.id,
                displayName = enemyDef.displayName,
                roleLabel = enemyDef.roleLabel,
                isPlayerSide = false,
                maxHp = enemyDef.hp,
                currentHp = enemyDef.hp,
                baseArmorClass = enemyDef.armorClass,
                attackBonus = enemyDef.attackBonus,
                attackDamage = enemyDef.damage,
                saveVitality = enemyDef.saveBonus,
                saveMind = enemyDef.saveBonus,
                morale = enemyDef.morale,
                initiative = enemyDef.initiative,
                corrodesArmor = enemyDef.corrodesArmor,
                corrodesWeapons = enemyDef.corrodesWeapons,
                immuneToCharm = enemyDef.immuneToCharm,
                immuneToFear = enemyDef.immuneToFear,
                immuneToSleep = enemyDef.immuneToSleep,
                moveRange = enemyDef.moveRange,
                attackRangeMultiplier = enemyDef.attackRangeMultiplier,
                skillIds = new List<string>(enemyDef.skillIds)
            };
        }

        private void SpawnView(GameObject prefab, CombatantRuntime runtime, AnimationSetDefinition animationSet)
        {
            if (prefab == null)
            {
                _views[runtime] = null;
                return;
            }

            var instance = Instantiate(prefab, runtime.position, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(instance, _battleScene);

            var view = instance.GetComponent<CombatantView>();
            if (view == null)
            {
                view = instance.AddComponent<CombatantView>();
            }

            view.Runtime = runtime;
            runtime.viewTransform = instance.transform;

            // Head-height point for the third-person camera to follow (a child, so it moves with the unit).
            var cameraTarget = new GameObject("CameraTarget").transform;
            cameraTarget.SetParent(instance.transform, false);
            cameraTarget.localPosition = new Vector3(0f, 1.5f, 0f);
            view.CameraTarget = cameraTarget;

            var animator = instance.GetComponent<CombatantAnimator>();
            if (animator == null)
            {
                animator = instance.AddComponent<CombatantAnimator>();
            }

            animator.Initialize(animationSet);
            instance.transform.rotation = Quaternion.LookRotation(runtime.isPlayerSide ? Vector3.forward : Vector3.back);
            _views[runtime] = instance;

            AddHoverHitbox(instance);
        }

        /// <summary>
        /// A generously-sized collider covering the whole combatant column (model + the
        /// floating enemy HP bar above it), separate from the model's own tight mesh
        /// collider. Without this, hovering/clicking an enemy only registered when aimed
        /// exactly at the small placeholder mesh — missing the HP bar most players actually
        /// aim at, which has no collider of its own. Bigger and more forgiving for allies too.
        /// </summary>
        private static void AddHoverHitbox(GameObject instance)
        {
            var hitboxObject = new GameObject("HoverHitbox");
            hitboxObject.transform.SetParent(instance.transform, false);
            hitboxObject.transform.localPosition = new Vector3(0f, 1.25f, 0f);

            var box = hitboxObject.AddComponent<BoxCollider>();
            box.size = new Vector3(1.4f, 2.6f, 1.4f);
            box.isTrigger = true;
        }

        private void UpdateViewPositions()
        {
            foreach (var kvp in _views)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.transform.position = kvp.Key.position;
                }
            }
        }

        private void CleanupViews()
        {
            foreach (var kvp in _views)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
            }

            _views.Clear();
        }

        private static BattleArenaSpawnPoints FindSpawnPoints(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<BattleArenaSpawnPoints>();
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>Carries each ally's remaining HP into the next fight (knocked-out allies come back at 1 HP).</summary>
        private void SavePartyState()
        {
            foreach (var ally in _combatants.Where(c => c.isPlayerSide))
            {
                PartyState.SetHp(ally.definitionId, ally.currentHp);
                PartyState.SetCorrosion(ally.definitionId, ally.armorCorrosion, ally.weaponCorrosion);
            }
        }

        private EncounterResult CheckWinLoss()
        {
            bool alliesAlive = _combatants.Any(c => c.isPlayerSide && !c.IsDefeated);
            bool enemiesAlive = _combatants.Any(c => !c.isPlayerSide && !c.IsDefeated);

            if (alliesAlive && enemiesAlive)
            {
                return null;
            }

            var defeatedAllyIds = _combatants
                .Where(c => c.isPlayerSide && c.IsDefeated)
                .Select(c => c.definitionId)
                .ToList();

            if (!enemiesAlive)
            {
                var defeatedEnemies = _combatants.Where(c => !c.isPlayerSide && c.IsDefeated).ToList();
                return new EncounterResult
                {
                    victory = true,
                    defeatedAllyIds = defeatedAllyIds,
                    xpAwarded = ComputeXpAwarded(defeatedEnemies),
                    lootItemIds = RollLoot(defeatedEnemies)
                };
            }

            return new EncounterResult
            {
                victory = false,
                defeatedAllyIds = defeatedAllyIds,
                xpAwarded = 0,
                lootItemIds = new List<string>()
            };
        }

        private static int ComputeXpAwarded(List<CombatantRuntime> defeatedEnemies)
        {
            // Placeholder formula: EnemyDefinition has no xpReward field yet — derives a rough value from its stat block
            // (hit points + armor class + attack). Monsters that fled count too: they were beaten, just not slain.
            int total = 0;
            foreach (var enemy in defeatedEnemies)
            {
                total += enemy.maxHp + enemy.baseArmorClass + enemy.attackBonus;
            }

            return total;
        }

        private static List<string> RollLoot(List<CombatantRuntime> defeatedEnemies)
        {
            var loot = new List<string>();
            foreach (var enemy in defeatedEnemies.Where(e => e.IsSlain)) // a fleeing monster takes its loot with it
            {
                var enemyDef = DatabaseRegistry.GetEnemy(enemy.definitionId);
                if (enemyDef == null)
                {
                    continue;
                }

                foreach (var entry in enemyDef.lootTable)
                {
                    if (UnityEngine.Random.value <= entry.dropChance)
                    {
                        loot.Add(entry.itemId);
                    }
                }
            }

            return loot;
        }

        private EncounterResult BuildTimeoutResult()
        {
            Debug.LogWarning("[EncounterRunner] Encounter exceeded MaxTurns without a winner — resolving as a loss.");
            return new EncounterResult
            {
                victory = false,
                defeatedAllyIds = _combatants.Where(c => c.isPlayerSide && c.IsDefeated).Select(c => c.definitionId).ToList(),
                xpAwarded = 0,
                lootItemIds = new List<string>()
            };
        }
    }
}
