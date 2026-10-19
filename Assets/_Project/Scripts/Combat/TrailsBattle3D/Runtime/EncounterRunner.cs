using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Combat.Contracts;
using Game.Common.Area;
using Game.Data.Definitions;
using Game.Data.Enums;
using Game.Data.Registry;
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

        private static readonly Color AllyHighlightColor = new Color(0.35f, 0.55f, 0.95f);
        private static readonly Color EnemyHighlightColor = new Color(0.85f, 0.35f, 0.35f);

        public void StartEncounter(EncounterDefinition encounter, Action<EncounterResult> onComplete)
        {
            _encounter = encounter;
            _onComplete = onComplete;
            StartCoroutine(RunEncounter());
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

            if (_currentPlayerActor == null)
            {
                return;
            }

            if (command == PlayerCommand.CancelCommand)
            {
                _pendingCommand = null;
                _pendingSkill = null;
                // Re-firing OnTurnStarted is what actually re-opens the command menu and
                // clears the targeting prompt — both already reset on this event, so
                // cancelling doesn't need its own UI-facing event.
                OnTurnStarted?.Invoke(_currentPlayerActor);
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

                // Guard/Smoke Bomb/Bodyguard all protect until this unit's own next turn, then
                // clear so it can choose again. Life Bond instead counts down its own duration.
                actor.guarding = false;
                actor.untargetable = false;
                actor.bodyguardTarget = null;
                if (actor.lifeBondProtector != null)
                {
                    actor.lifeBondTurnsRemaining--;
                    if (actor.lifeBondTurnsRemaining <= 0)
                    {
                        actor.lifeBondProtector = null;
                    }
                }

                int apGain = Mathf.Max(0, actor.speed);
                if (actor.pendingGuardApBonus)
                {
                    apGain += 2;
                    actor.pendingGuardApBonus = false;
                }
                actor.currentAp = Mathf.Min(actor.maxAp, actor.currentAp + apGain);
                FearRuntime.DecayAtTurnStart(actor);

                OnTurnStarted?.Invoke(actor);

                var hpBeforeTick = actor.currentHp;
                StatusEffectRuntime.TickAndExpire(actor);
                RaiseStateChanged(actor);
                if (hpBeforeTick > 0 && actor.IsDefeated)
                {
                    OnCombatantDefeated?.Invoke(actor);
                }

                if (!actor.IsDefeated && !StatusEffectRuntime.IsActionPrevented(actor))
                {
                    if (FearRuntime.ShouldFreeze(actor))
                    {
                        // Fear >= 10: unit freezes and is forced to Guard this turn instead of
                        // acting normally.
                        ActionResolver.ApplyGuard(actor);
                        RaiseStateChanged(actor);
                    }
                    else
                    {
                        yield return ProcessTurn(actor);
                    }

                    UpdateViewPositions();
                }

                RaiseTurnOrderChanged();
                result = CheckWinLoss();
            }

            if (result == null)
            {
                result = BuildTimeoutResult();
            }

            if (result.victory)
            {
                SavePartyState();
            }

            OnEncounterEnded?.Invoke(result);

            if (WaitForResultAcknowledgementBeforeUnload)
            {
                _resultAcknowledged = false;
                yield return new WaitUntil(() => _resultAcknowledged);
            }

            CleanupViews();
            yield return SceneManager.UnloadSceneAsync(_battleScene);

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
                yield return ResolvePlayerTurn(actor, opponents);
            }
        }

        private IEnumerator ResolveAiTurn(CombatantRuntime actor, List<CombatantRuntime> opponents)
        {
            // Noble Challenge: a forced target overrides "attack nearest" for one turn.
            CombatantRuntime forcedTarget = actor.forcedAttackTarget != null && !actor.forcedAttackTarget.IsDefeated
                ? actor.forcedAttackTarget
                : null;
            actor.forcedAttackTarget = null;

            var allies = _combatants.Where(c => !c.IsDefeated && c.isPlayerSide == actor.isPlayerSide).ToList();
            var (skill, target, guard) = ChooseAiAction(actor, opponents, allies, forcedTarget);

            if (guard)
            {
                ActionResolver.ApplyGuard(actor);
                RaiseStateChanged(actor);
                yield break;
            }

            if (skill == null || target == null)
            {
                yield break;
            }

            bool targetIsOpponent = target.isPlayerSide != actor.isPlayerSide;
            if (targetIsOpponent)
            {
                var attackRange = AttackRangeFor(actor);
                yield return MoveWithinRange(actor, target.position, attackRange);
                if (Vector3.Distance(actor.position, target.position) > attackRange + 0.1f)
                {
                    // Couldn't close the distance (e.g. path blocked, or out of move range) —
                    // action fizzles this turn.
                    yield break;
                }
            }

            yield return PerformSkill(actor, skill, target);
        }

        /// <summary>Actual attack distance in world units: attackRangeMultiplier * RangeUnit.
        /// A multiplier of 1 (melee) gives RangeUnit reach; ranged combatants use a higher
        /// multiplier instead of the old unlimited-range Ranged trait.</summary>
        private static float AttackRangeFor(CombatantRuntime combatant)
        {
            return combatant.attackRangeMultiplier * ActionResolver.RangeUnit;
        }

        /// <summary>
        /// A simple, deterministic AI policy — not tactically deep, just enough for the
        /// roster's cards (and monster kits) to actually see use instead of every AI turn
        /// being a Basic Attack: Guard when badly hurt or fairly afraid; otherwise use the
        /// first affordable Craft whose target type it can satisfy (an enemy-targeting card
        /// vs. the nearest/forced opponent, a self/ally-targeting card vs. itself or its
        /// neediest living ally); otherwise fall back to the Basic Attack.
        /// </summary>
        private (SkillDefinition skill, CombatantRuntime target, bool guard) ChooseAiAction(
            CombatantRuntime actor,
            List<CombatantRuntime> opponents,
            List<CombatantRuntime> allies,
            CombatantRuntime forcedTarget)
        {
            bool lowHp = actor.currentHp <= actor.maxHp / 3;
            bool anxious = actor.fear >= 7;
            if (lowHp || anxious)
            {
                return (null, null, true);
            }

            var nearestOpponent = forcedTarget ?? opponents.OrderBy(o => Vector3.Distance(actor.position, o.position)).First();

            // Bringing a fallen ally back beats everything else the actor could do with its AP.
            var fallenAlly = _combatants.FirstOrDefault(c => c.isPlayerSide == actor.isPlayerSide && c.IsDefeated);
            if (fallenAlly != null)
            {
                foreach (var skillId in actor.skillIds)
                {
                    var reviveSkill = DatabaseRegistry.GetSkill(skillId);
                    if (reviveSkill != null && reviveSkill.isRevive && actor.currentAp >= reviveSkill.apCost)
                    {
                        return (reviveSkill, fallenAlly, false);
                    }
                }
            }

            for (int i = 1; i < actor.skillIds.Count; i++)
            {
                var skill = DatabaseRegistry.GetSkill(actor.skillIds[i]);
                if (skill == null || actor.currentAp < skill.apCost)
                {
                    continue;
                }

                switch (skill.targetType)
                {
                    case TargetType.SingleEnemy:
                    case TargetType.AllEnemies:
                    case TargetType.AreaCircle:
                    case TargetType.AreaCone:
                    case TargetType.AreaLine:
                        return (skill, nearestOpponent, false);

                    case TargetType.SingleAlly:
                    case TargetType.AllAllies:
                    {
                        if (skill.isRevive)
                        {
                            var fallen = _combatants.FirstOrDefault(c => c.isPlayerSide == actor.isPlayerSide && c.IsDefeated);
                            if (fallen != null)
                            {
                                return (skill, fallen, false);
                            }
                            break;
                        }

                        var neediest = allies
                            .Where(c => c != actor)
                            .OrderBy(c => (float)c.currentHp / c.maxHp)
                            .FirstOrDefault();
                        if (neediest != null)
                        {
                            return (skill, neediest, false);
                        }
                        break;
                    }

                    case TargetType.Self:
                        return (skill, actor, false);
                }
            }

            return (GetFirstUsableSkill(actor), nearestOpponent, false);
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
            _movementArea.ShowCircle(_turnStartPosition, actor.moveRange);

            var targeting = GetComponent<TargetingInput>();

            while (true)
            {
                UpdateHoverPreview(actor, targeting);
                UpdateAttackRangeArea(actor, targeting);
                UpdateAoePreview(actor, targeting);

                if (_pendingCommand == PlayerCommand.Guard)
                {
                    ActionResolver.ApplyGuard(actor);
                    RaiseStateChanged(actor);
                    break;
                }

                if (targeting != null && Input.GetMouseButtonDown(0))
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
                        bool isBasicAttack = actor.skillIds.Count > 0 && skillToUse != null && skillToUse.id == actor.skillIds[0];

                        if (skillToUse != null
                            && actor.currentAp >= skillToUse.apCost
                            && !(view.Runtime.untargetable && isBasicAttack))
                        {
                            var attackRange = AttackRangeFor(actor);
                            yield return MoveWithinRange(actor, view.Runtime.position, attackRange);
                            bool canAct = Vector3.Distance(actor.position, view.Runtime.position) <= attackRange + 0.1f;

                            if (canAct)
                            {
                                yield return PerformSkill(actor, skillToUse, view.Runtime);
                                break;
                            }
                        }
                    }
                    else if (targeting.TryRaycastGround(out var point))
                    {
                        // Anchored to the turn-start position (not the actor's current,
                        // possibly-already-moved position) so repeated clicks can't chain
                        // into unlimited movement across a single turn.
                        if (MovementController.TryGetMovePosition(_turnStartPosition, point, actor.moveRange, out var newPosition))
                        {
                            yield return MoveAlongPath(actor, newPosition);
                        }
                    }
                }

                yield return null;
            }

            _movementArea.Hide();
            _attackRangeArea.Hide();
            _aoeArea.Hide();
            _hoverHighlightRing.Hide();
            _movementPathPreview.Hide();

            _currentPlayerActor = null;
            _pendingCommand = null;
            _pendingSkill = null;
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
            if (targeting.TryRaycastGround(out var point)
                && NavMesh.SamplePosition(point, out var navHit, actor.moveRange + 5f, NavMesh.AllAreas))
            {
                _movementPathPreview.Show(actor.position, navHit.position, _turnStartPosition, actor.moveRange);
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
            return _combatants.Where(c => c.isPlayerSide == actor.isPlayerSide && c.IsDefeated == skill.isRevive);
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

            if (MovementController.TryGetMovePosition(_turnStartPosition, moveGoal, actor.moveRange, out var newPosition))
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
                var camera = root.GetComponentInChildren<Camera>();
                if (camera != null)
                {
                    targeting.arenaCamera = camera;
                    return;
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
        /// <see cref="ExecuteSkill"/> exactly as before - this only decides *when*. Missing clips
        /// degrade to an immediate resolve, so a turn can never stall on animation data.
        /// </summary>
        private IEnumerator PerformSkill(CombatantRuntime actor, SkillDefinition skill, CombatantRuntime primaryTarget)
        {
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

            ExecuteSkill(actor, skill, primaryTarget);

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

            var wait = Mathf.Max(duration - impact, reactionWait);
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

        private void ExecuteSkill(CombatantRuntime actor, SkillDefinition skill, CombatantRuntime primaryTarget)
        {
            actor.currentAp = Mathf.Max(0, actor.currentAp - Mathf.Max(0, skill.apCost));

            var targets = ActionResolver.ResolveTargets(skill, actor, primaryTarget, _combatants);
            foreach (var rawTarget in targets)
            {
                // Bodyguard: a full one-time redirect onto the protector, resolved as if the
                // attack targeted them from the start (their DEF/fear, not the original ally's).
                var target = skill.isRevive ? rawTarget : ResolveBodyguardRedirect(rawTarget);
                var wasAlive = !target.IsDefeated;

                if (skill.isRevive)
                {
                    ActionResolver.ApplyRevive(target, skill);
                }
                else if (skill.isHeal)
                {
                    int healed = UnityEngine.Random.Range(1, 7) + skill.healBonus;
                    target.currentHp = Mathf.Min(target.maxHp, target.currentHp + healed);
                }
                else if (skill.skipDamage)
                {
                    if (skill.fearDelta != 0)
                    {
                        target.fear = Mathf.Clamp(target.fear + skill.fearDelta, 0, 10);
                    }
                }
                else
                {
                    var rollInfo = ActionResolver.ResolveDamage(actor, target, skill);
                    ApplyDamageWithLifeBond(target, rollInfo.damage);
                    FearRuntime.ApplyFearFromHit(target, rollInfo.wasCrit, actor.isHorror);
                    if (skill.fearDelta != 0)
                    {
                        target.fear = Mathf.Clamp(target.fear + skill.fearDelta, 0, 10);
                    }
                    OnDamageRolled?.Invoke(rollInfo);
                }

                if (!string.IsNullOrEmpty(skill.statusEffectId))
                {
                    StatusEffectRuntime.TryApply(target, skill.statusEffectId, skill.statusEffectChance);
                }

                RaiseStateChanged(target);

                if (wasAlive && target.IsDefeated)
                {
                    OnCombatantDefeated?.Invoke(target);
                }
            }

            if (skill.selfDamage > 0)
            {
                var wasAlive = !actor.IsDefeated;
                actor.currentHp = Mathf.Max(0, actor.currentHp - skill.selfDamage);
                RaiseStateChanged(actor);
                if (wasAlive && actor.IsDefeated)
                {
                    OnCombatantDefeated?.Invoke(actor);
                }
            }

            CardEffects.TryApply(skill.id, actor, primaryTarget);
        }

        /// <summary>If someone is guarding <paramref name="target"/> with Bodyguard, the attack
        /// redirects onto the protector entirely (consuming the redirect) instead of hitting
        /// the originally-clicked/targeted combatant.</summary>
        private CombatantRuntime ResolveBodyguardRedirect(CombatantRuntime target)
        {
            var protector = _combatants.FirstOrDefault(c => !c.IsDefeated && c.bodyguardTarget == target);
            if (protector == null)
            {
                return target;
            }

            protector.bodyguardTarget = null;
            return protector;
        }

        /// <summary>Life Bond splits incoming HP loss between the bonded target and its
        /// protector; everything else about the hit (DEF, fear) already resolved against the
        /// original target before this runs.</summary>
        private void ApplyDamageWithLifeBond(CombatantRuntime target, int damage)
        {
            var protector = target.lifeBondProtector;
            if (protector != null && !protector.IsDefeated)
            {
                int protectorShare = damage / 2;
                int targetShare = damage - protectorShare;
                var wasProtectorAlive = !protector.IsDefeated;

                protector.currentHp = Mathf.Max(0, protector.currentHp - protectorShare);
                target.currentHp = Mathf.Max(0, target.currentHp - targetShare);

                RaiseStateChanged(protector);
                if (wasProtectorAlive && protector.IsDefeated)
                {
                    OnCombatantDefeated?.Invoke(protector);
                }
            }
            else
            {
                target.currentHp = Mathf.Max(0, target.currentHp - damage);
            }
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
                if (!string.IsNullOrEmpty(actorDef.startingWeaponId))
                {
                    var weapon = DatabaseRegistry.GetWeapon(actorDef.startingWeaponId);
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

        private static CombatantRuntime BuildAllyRuntime(ActorDefinition actorDef)
        {
            int atkBonus = 0;
            int defBonus = 0;

            if (!string.IsNullOrEmpty(actorDef.startingWeaponId))
            {
                var weapon = DatabaseRegistry.GetWeapon(actorDef.startingWeaponId);
                if (weapon != null)
                {
                    atkBonus += weapon.atkBonus;
                }
            }

            if (!string.IsNullOrEmpty(actorDef.startingArmorId))
            {
                var armor = DatabaseRegistry.GetArmor(actorDef.startingArmorId);
                if (armor != null)
                {
                    defBonus += armor.defBonus;
                }
            }

            return new CombatantRuntime
            {
                definitionId = actorDef.id,
                displayName = actorDef.displayName,
                roleLabel = actorDef.roleLabel,
                isPlayerSide = true,
                maxHp = actorDef.baseHp,
                currentHp = PartyState.GetStartingHp(actorDef.id, actorDef.baseHp),
                maxEp = actorDef.baseMp,
                currentEp = actorDef.baseMp,
                atk = actorDef.baseAtk + atkBonus,
                def = actorDef.baseDef + defBonus,
                speed = actorDef.baseSpeed,
                moveRange = actorDef.moveRange,
                attackRangeMultiplier = actorDef.attackRangeMultiplier,
                skillIds = new List<string>(actorDef.startingSkillIds)
            };
        }

        private static CombatantRuntime BuildEnemyRuntime(EnemyDefinition enemyDef)
        {
            return new CombatantRuntime
            {
                definitionId = enemyDef.id,
                displayName = enemyDef.displayName,
                roleLabel = enemyDef.roleLabel,
                isPlayerSide = false,
                maxHp = enemyDef.hp,
                currentHp = enemyDef.hp,
                maxEp = 0,
                currentEp = 0,
                atk = enemyDef.atk,
                def = enemyDef.def,
                speed = enemyDef.speed,
                moveRange = enemyDef.moveRange,
                attackRangeMultiplier = enemyDef.attackRangeMultiplier,
                isHorror = enemyDef.isHorror,
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
            // Placeholder formula: EnemyDefinition has no xpReward field yet (schema gap,
            // out of this phase's scope) — derives a rough value from stats until it does.
            int total = 0;
            foreach (var enemy in defeatedEnemies)
            {
                total += enemy.maxHp + enemy.atk + enemy.def;
            }

            return total;
        }

        private static List<string> RollLoot(List<CombatantRuntime> defeatedEnemies)
        {
            var loot = new List<string>();
            foreach (var enemy in defeatedEnemies)
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
