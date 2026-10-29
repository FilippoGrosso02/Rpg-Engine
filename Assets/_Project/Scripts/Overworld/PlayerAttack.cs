using System.Collections;
using Game.Combat.Contracts;
using Game.Data.Enums;
using Game.Data.Registry;
using Game.Data.State;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>
    /// Left mouse swings the leader's weapon, and what that does depends on the leader's class (<see cref="OverworldAttackStyle"/>):
    /// a warrior strikes whatever is within reach and never surprises anyone; a thief who has not been spotted points at an enemy
    /// (the crosshair), walks up to it and ambushes it; a ranger shoots an arrow from afar and ambushes an enemy that has not noticed
    /// the party. Hitting an enemy group starts the fight, in the arena or on the map alike. An ambush means every hero acts before
    /// any enemy. The crosshair shows what a click would do: green when a surprise is possible, red (an X) when the target has spotted the party.
    /// Lives next to the <see cref="OverworldDirector"/>.
    /// </summary>
    public class PlayerAttack : MonoBehaviour
    {
        public enum AimKind { None, Melee, Approach, Shoot }

        public struct Aim
        {
            public OverworldEnemy target;
            public AimKind kind;
            /// <summary>The blow would be an ambush (the enemy has not noticed the party and the class can exploit that).</summary>
            public bool ambush;
            /// <summary>The crosshair is on an enemy that has already spotted the party, so no surprise is possible.</summary>
            public bool spotted;
        }

        [Header("Melee")]
        [Tooltip("How close the player must be to an enemy (or one of its group) to hit it.")]
        public float reach = 2.4f;

        [Tooltip("The enemy must be within this angle of where the camera looks.")]
        [Range(10f, 180f)] public float coneDegrees = 140f;

        [Header("Aimed attacks (thief / ranger)")]
        [Tooltip("How far away a thief can point at an enemy to creep up on it.")]
        public float stealthRange = 14f;

        [Tooltip("How far away a ranger can shoot.")]
        public float rangedRange = 22f;

        [Tooltip("How close to the screen centre (pixels) an enemy must be to count as aimed at.")]
        public float aimRadiusPixels = 140f;

        [Tooltip("A thief stops this far from the enemy before striking.")]
        public float stealthStopDistance = 1.7f;

        [Tooltip("Arrow speed in metres per second.")]
        public float arrowSpeed = 40f;

        public Aim CurrentAim => _aim;

        OverworldDirector _director;
        Aim _aim;
        bool _striking;
        bool _walkArrived;
        OverworldEnemy[] _enemies = new OverworldEnemy[0];
        float _nextEnemyRefresh;
        string _styleForLeader;
        OverworldAttackStyle _style;

        void Awake() => _director = GetComponent<OverworldDirector>();

        void Update()
        {
            if (_striking || _director == null || _director.InBattle || _director.player == null)
            {
                _aim = default;
                return;
            }

            if (Time.unscaledTime >= _nextEnemyRefresh)
            {
                _enemies = FindObjectsByType<OverworldEnemy>();
                _nextEnemyRefresh = Time.unscaledTime + 0.5f;
            }

            bool canAct = CanAct();
            _aim = canAct ? Evaluate() : default;

            if (!canAct || !Input.GetMouseButtonDown(0)) return;

            switch (_aim.kind)
            {
                case AimKind.Approach: StartCoroutine(Approach(_aim.target)); break;
                case AimKind.Shoot: StartCoroutine(Shoot(_aim.target, _aim.ambush)); break;
                case AimKind.Melee: StartCoroutine(Strike(_aim.target, _aim.ambush ? EncounterAdvantage.Party : EncounterAdvantage.None)); break;
                default: StartCoroutine(Strike(null, EncounterAdvantage.None)); break; // nothing there: just swing
            }
        }

        bool CanAct() =>
            Cursor.lockState == CursorLockMode.Locked // a free cursor is clicking the menu
            && Time.timeScale > 0f
            && !DemoEndPanel.IsOpen
            && !_director.player.movementLocked // the party is swapping its leader
            && !_director.player.AutoWalking;

        // ------------------------------------------------------------------ what a click would do

        Aim Evaluate()
        {
            var style = CurrentStyle();
            var spottedHint = false;
            OverworldEnemy spottedTarget = null;

            if (style != OverworldAttackStyle.Melee)
            {
                float range = style == OverworldAttackStyle.Stealth ? stealthRange : rangedRange;
                var aimed = PickAtCrosshair(range);
                if (aimed != null)
                {
                    if (style == OverworldAttackStyle.Ranged)
                    {
                        bool unnoticed = !aimed.HasNoticedPlayer;
                        return new Aim { target = aimed, kind = AimKind.Shoot, ambush = unnoticed, spotted = !unnoticed };
                    }

                    if (!aimed.HasNoticedPlayer) return new Aim { target = aimed, kind = AimKind.Approach, ambush = true };

                    spottedHint = true; // a thief cannot creep up on someone who is watching
                    spottedTarget = aimed;
                }
            }

            var melee = FindMeleeTarget();
            if (melee != null)
            {
                // Only a thief gets the drop on someone by walking right up to them.
                bool ambush = style == OverworldAttackStyle.Stealth && !melee.HasNoticedPlayer;
                return new Aim { target = melee, kind = AimKind.Melee, ambush = ambush };
            }

            return new Aim { target = spottedTarget, kind = AimKind.None, spotted = spottedHint };
        }

        OverworldAttackStyle CurrentStyle()
        {
            if (PartyRoster.Count == 0) return OverworldAttackStyle.Melee;
            var leaderId = PartyRoster.Members[0];
            if (leaderId == _styleForLeader) return _style;

            _styleForLeader = leaderId;
            var actor = DatabaseRegistry.GetActor(leaderId);
            var classDefinition = actor != null ? DatabaseRegistry.GetClass(actor.classId) : null;
            _style = classDefinition != null ? classDefinition.overworldAttack : OverworldAttackStyle.Melee;
            return _style;
        }

        /// <summary>The nearest enemy group with a member in reach, in front of the camera.</summary>
        OverworldEnemy FindMeleeTarget()
        {
            var playerPosition = _director.player.transform.position;
            var facing = CameraForward();

            OverworldEnemy best = null;
            float bestDistance = reach;
            foreach (var enemy in _enemies)
            {
                if (!Attackable(enemy)) continue;

                float distance = enemy.NearestDistanceTo(playerPosition, facing, coneDegrees, out bool inCone);
                if (!inCone || distance > bestDistance) continue;
                best = enemy;
                bestDistance = distance;
            }

            return best;
        }

        /// <summary>The enemy group whose nearest model is closest to the crosshair, in range and in line of sight.</summary>
        OverworldEnemy PickAtCrosshair(float range)
        {
            var camera = Camera.main;
            if (camera == null) return null;

            var player = _director.player.transform;
            var eye = player.position + Vector3.up * 1.4f;
            var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            OverworldEnemy best = null;
            float bestPixels = aimRadiusPixels;
            foreach (var enemy in _enemies)
            {
                if (!Attackable(enemy)) continue;

                foreach (var position in enemy.ModelPositions())
                {
                    var chest = position + Vector3.up * 1f;
                    var flat = chest - eye;
                    flat.y = 0f;
                    if (flat.magnitude > range) continue;

                    var screen = camera.WorldToScreenPoint(chest);
                    if (screen.z <= 0f) continue;

                    float pixels = Vector2.Distance(centre, new Vector2(screen.x, screen.y));
                    if (pixels >= bestPixels || !HasLineOfSight(eye, chest, enemy)) continue;

                    best = enemy;
                    bestPixels = pixels;
                }
            }

            return best;
        }

        static bool Attackable(OverworldEnemy enemy) => enemy != null && enemy.contactEnabled && enemy.encounter != null;

        /// <summary>Walls and pillars block the shot; the enemy's own body does not.</summary>
        static bool HasLineOfSight(Vector3 from, Vector3 to, OverworldEnemy enemy)
        {
            if (!Physics.Linecast(from, to, out var hit, ~0, QueryTriggerInteraction.Ignore)) return true;
            return hit.transform.IsChildOf(enemy.transform);
        }

        Vector3 CameraForward()
        {
            var cameraTransform = _director.player.cameraTransform != null ? _director.player.cameraTransform
                : Camera.main != null ? Camera.main.transform : null;
            var facing = cameraTransform != null ? cameraTransform.forward : _director.player.transform.forward;
            facing.y = 0f;
            return facing;
        }

        // ------------------------------------------------------------------ the three ways of striking

        /// <summary>Swings the leader's weapon (at the target, if there is one) and, when something was hit, starts the fight once the blow has landed.</summary>
        IEnumerator Strike(OverworldEnemy target, EncounterAdvantage advantage)
        {
            var player = _director.player;
            _striking = true;
            player.movementLocked = true;
            if (target != null)
            {
                _director.AttackPending = true;
                FaceTowards(target);
                if (advantage == EncounterAdvantage.Party) InteractionSystem.ShowMessage("AMBUSH! The party strikes first.");
            }

            float wait = SwingAnimation();
            if (wait > 0f) yield return new WaitForSeconds(wait);

            Finish();
            if (target != null && !_director.InBattle) _director.BeginEncounter(target, advantage);
        }

        /// <summary>The thief creeps up to the enemy and ambushes it. Being spotted on the way calls it off.</summary>
        IEnumerator Approach(OverworldEnemy target)
        {
            var player = _director.player;
            _striking = true;
            _walkArrived = false;

            var destination = target.NearestModelPosition(player.transform.position);
            bool started = player.WalkTo(destination, stealthStopDistance, arrived => _walkArrived = arrived);
            if (!started)
            {
                InteractionSystem.ShowMessage("There is no way to creep up on it.");
                Finish();
                yield break;
            }

            while (player.AutoWalking)
            {
                if (target == null || target.HasNoticedPlayer)
                {
                    player.CancelAutoWalk();
                    InteractionSystem.ShowMessage("SPOTTED!");
                    Finish();
                    yield break;
                }

                yield return null;
            }

            if (!_walkArrived || target == null || target.HasNoticedPlayer)
            {
                Finish();
                yield break;
            }

            // Close enough: strike.
            player.movementLocked = true;
            _director.AttackPending = true;
            FaceTowards(target);
            InteractionSystem.ShowMessage("AMBUSH! The party strikes first.");
            float wait = SwingAnimation();
            if (wait > 0f) yield return new WaitForSeconds(wait);

            Finish();
            if (target != null && !_director.InBattle) _director.BeginEncounter(target, EncounterAdvantage.Party);
        }

        /// <summary>The ranger looses an arrow; it lands, and the fight starts (an ambush if the enemy had not noticed the party when it was fired).</summary>
        IEnumerator Shoot(OverworldEnemy target, bool ambush)
        {
            var player = _director.player;
            _striking = true;
            player.movementLocked = true;
            _director.AttackPending = true;
            FaceTowards(target);
            if (ambush) InteractionSystem.ShowMessage("AMBUSH! The party strikes first.");

            float release = SwingAnimation(followThrough: 0f);
            if (release > 0f) yield return new WaitForSeconds(release);

            var from = player.transform.position + Vector3.up * 1.3f;
            var to = target != null ? target.NearestModelPosition(from) + Vector3.up * 1f : from + player.transform.forward * 10f;
            bool landed = false;
            OverworldArrow.Fire(from, to, arrowSpeed, () => landed = true);

            float timeout = Time.time + 3f + Vector3.Distance(from, to) / Mathf.Max(1f, arrowSpeed);
            while (!landed && Time.time < timeout) yield return null;

            Finish();
            if (target != null && !_director.InBattle) _director.BeginEncounter(target, ambush ? EncounterAdvantage.Party : EncounterAdvantage.None);
        }

        float SwingAnimation(float followThrough = 0.5f)
        {
            var switcher = _director.player.GetComponent<PlayerModelSwitcher>();
            return switcher != null ? switcher.PlaySwing(followThrough) : 0f;
        }

        void FaceTowards(OverworldEnemy target)
        {
            if (target == null) return;
            var player = _director.player;
            var toward = target.NearestModelPosition(player.transform.position) - player.transform.position;
            toward.y = 0f;
            if (toward.sqrMagnitude > 0.01f) player.transform.rotation = Quaternion.LookRotation(toward);
        }

        void Finish()
        {
            _director.AttackPending = false;
            _director.player.movementLocked = false; // a fight freezes the player itself, right after
            _striking = false;
        }

        // ------------------------------------------------------------------ crosshair

        GUIStyle _labelStyle;

        /// <summary>
        /// A simple crosshair at the screen centre: a small white plus normally, a larger green one when a surprise is possible, and a
        /// red X when the aimed enemy has already spotted the party. A caption says what a click will do.
        /// </summary>
        void OnGUI()
        {
            if (_director == null || _director.InBattle || _striking || Cursor.lockState != CursorLockMode.Locked || Time.timeScale <= 0f) return;
            if (DemoEndPanel.IsOpen) return;

            Color color = new Color(1f, 1f, 1f, 0.8f);
            string caption = null;
            bool cross = false;
            float size = 1f;

            if (_aim.ambush)
            {
                color = new Color(0.45f, 1f, 0.5f, 0.95f);
                caption = _aim.kind == AimKind.Shoot ? "AMBUSH SHOT" : _aim.kind == AimKind.Approach ? "STEALTH ATTACK" : "BACKSTAB";
                size = 1.5f;
            }
            else if (_aim.spotted)
            {
                color = new Color(1f, 0.3f, 0.25f, 0.95f);
                caption = _aim.kind == AimKind.Shoot ? "SPOTTED - NO SURPRISE" : "SPOTTED";
                cross = true;
                size = 1.4f;
            }
            else if (_aim.kind == AimKind.Melee)
            {
                caption = "ATTACK";
                size = 1.2f;
            }

            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            DrawReticle(cx, cy, color, size, cross);

            if (caption == null) return;
            _labelStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };
            var rect = new Rect(cx - 160f, cy + 28f, 320f, 24f);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), caption, _labelStyle);
            GUI.color = color;
            GUI.Label(rect, caption, _labelStyle);
            GUI.color = Color.white;
        }

        static void DrawReticle(float cx, float cy, Color color, float size, bool cross)
        {
            var previous = GUI.matrix;
            GUI.color = color;
            if (cross) GUIUtility.RotateAroundPivot(45f, new Vector2(cx, cy));

            float gap = 5f * size, length = 9f * size, thickness = 2f;
            var white = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(cx - gap - length, cy - thickness * 0.5f, length, thickness), white);
            GUI.DrawTexture(new Rect(cx + gap, cy - thickness * 0.5f, length, thickness), white);
            GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy - gap - length, thickness, length), white);
            GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy + gap, thickness, length), white);

            GUI.matrix = previous;
            if (!cross) GUI.DrawTexture(new Rect(cx - 1f, cy - 1f, 2f, 2f), white);
            GUI.color = Color.white;
        }
    }
}
