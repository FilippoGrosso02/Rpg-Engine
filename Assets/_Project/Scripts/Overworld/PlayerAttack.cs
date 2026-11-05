using System.Collections;
using Game.Combat.Contracts;
using Game.Data.Enums;
using Game.Data.Registry;
using Game.Data.State;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>
    /// A plain left click is an ordinary melee attack on the closest enemy. Hold the right mouse button to aim (the camera slides over
    /// the character's shoulder and the crosshair appears), then left mouse performs the class attack. What it does depends on the leader's class (<see cref="OverworldAttackStyle"/>):
    /// a warrior steps up to an enemy within about five steps and strikes it, never surprising anyone; a thief who has not been spotted
    /// does the same but ambushes it (and is called off if the enemy notices them on the way); a ranger shoots an arrow from afar and
    /// ambushes an enemy that has not noticed the party. Hitting an enemy group starts the fight, in the arena or on the map alike. An ambush means every hero acts before
    /// any enemy. The crosshair shows what a click would do: green when a surprise is possible, red (an X) when the target has spotted the party.
    /// Lives next to the <see cref="OverworldDirector"/>.
    /// </summary>
    public class PlayerAttack : MonoBehaviour
    {
        public enum AimKind { None, Engage, Shoot }

        public struct Aim
        {
            public OverworldEnemy target;
            public AimKind kind;
            /// <summary>The blow would be an ambush (the enemy has not noticed the party and the class can exploit that).</summary>
            public bool ambush;
            /// <summary>The crosshair is on an enemy that has already spotted the party, so no surprise is possible.</summary>
            public bool spotted;
        }

        [Header("Melee and stealth")]
        [Tooltip("How far away an enemy can be for a click to step up and strike it (melee classes and the thief): about five steps.")]
        public float engageRange = 4f;

        [Tooltip("The enemy must be within this angle of where the camera looks (melee classes).")]
        [Range(10f, 180f)] public float coneDegrees = 140f;

        [Tooltip("The attacker stops this far from the enemy and swings.")]
        public float strikeDistance = 1.8f;

        [Header("Ranged")]
        [Tooltip("How far away a ranger can shoot.")]
        public float rangedRange = 22f;

        [Tooltip("How close to the crosshair (pixels) an enemy must be to count as aimed at.")]
        public float aimRadiusPixels = 140f;

        [Header("Crosshair")]
        [Tooltip("Where the crosshair sits relative to the exact screen centre, as a fraction of the screen height (x right, y up). A little up and away from the character, who stands to the bottom left while aiming.")]
        public Vector2 reticleOffset = new Vector2(0.02f, 0.03f);

        [Tooltip("Arrow speed in metres per second.")]
        public float arrowSpeed = 40f;

        public Aim CurrentAim => _aim;

        OverworldDirector _director;
        OverworldCameraRig _cameraRig;
        bool _isAiming;
        OverworldEnemy _hover; // the enemy a plain click would attack (closest within range), when not aiming
        Aim _aim;
        bool _striking;
        bool _walkArrived;
        OverworldEnemy[] _enemies = new OverworldEnemy[0];
        float _nextEnemyRefresh;
        string _styleForLeader;
        OverworldAttackStyle _style;

        void Awake() => _director = GetComponent<OverworldDirector>();

        /// <summary>Right mouse held = aiming: the camera slides over the character's shoulder, the crosshair appears and left click performs the attack.</summary>
        void SetAiming(bool aiming)
        {
            if (_isAiming != aiming)
            {
                _isAiming = aiming;
                if (_cameraRig == null) _cameraRig = FindAnyObjectByType<OverworldCameraRig>();
                if (_cameraRig != null) _cameraRig.SetAiming(aiming);
            }

            if (_director != null && _director.player != null) _director.player.aiming = aiming;
        }

        void OnDisable() => SetAiming(false);

        void Update()
        {
            if (_striking || _director == null || _director.InBattle || _director.player == null)
            {
                SetAiming(false);
                _aim = default;
                _hover = null;
                return;
            }

            if (Time.unscaledTime >= _nextEnemyRefresh)
            {
                _enemies = FindObjectsByType<OverworldEnemy>();
                _nextEnemyRefresh = Time.unscaledTime + 0.5f;
            }

            bool canAct = CanAct();
            SetAiming(canAct && Input.GetMouseButton(1));
            _aim = _isAiming ? Evaluate() : default;
            _hover = !_isAiming && canAct ? FindEngageTarget(360f) : null;

            if (!canAct || !Input.GetMouseButtonDown(0)) return;

            if (!_isAiming)
            {
                // A plain click is the ordinary melee attack on whoever is closest, whatever the class. Only a Hidden leader surprises anyone.
                var closest = FindEngageTarget(360f);
                if (closest != null) StartCoroutine(Engage(closest, PartyRules.Hidden));
                else StartCoroutine(Strike(null, EncounterAdvantage.None)); // nobody near: just swing
                return;
            }

            switch (_aim.kind)
            {
                case AimKind.Engage: StartCoroutine(Engage(_aim.target, _aim.ambush)); break;
                case AimKind.Shoot: StartCoroutine(Shoot(_aim.target, _aim.ambush)); break;
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

            // Surprise comes from one place: a Hidden leader (Detached party, successful DEX check) striking. The class only decides how the blow is delivered.
            bool hidden = PartyRules.Hidden;

            if (style != OverworldAttackStyle.Melee)
            {
                float range = style == OverworldAttackStyle.Stealth ? engageRange : rangedRange;
                var aimed = PickAtCrosshair(range);
                if (aimed != null)
                {
                    return new Aim
                    {
                        target = aimed,
                        kind = style == OverworldAttackStyle.Ranged ? AimKind.Shoot : AimKind.Engage,
                        ambush = hidden,
                        spotted = !hidden && aimed.HasNoticedPlayer
                    };
                }
            }

            var near = FindEngageTarget();
            if (near != null) return new Aim { target = near, kind = AimKind.Engage, ambush = hidden, spotted = !hidden && near.HasNoticedPlayer };

            return default;
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

        /// <summary>The nearest enemy group with a member within engage range, inside <paramref name="cone"/> degrees of where the camera looks (default: the usual cone).</summary>
        OverworldEnemy FindEngageTarget(float cone = -1f)
        {
            var playerPosition = _director.player.transform.position;
            var facing = CameraForward();
            if (cone < 0f) cone = coneDegrees;

            OverworldEnemy best = null;
            float bestDistance = engageRange;
            foreach (var enemy in _enemies)
            {
                if (!Attackable(enemy)) continue;

                float distance = enemy.NearestDistanceTo(playerPosition, facing, cone, out bool inCone);
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
            var centre = AimScreenPoint();

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

        /// <summary>The crosshair's position in screen pixels (origin bottom left).</summary>
        Vector2 AimScreenPoint() =>
            new Vector2(Screen.width * 0.5f + reticleOffset.x * Screen.height, Screen.height * 0.5f + reticleOffset.y * Screen.height);

        static bool Attackable(OverworldEnemy enemy) => enemy != null && enemy.contactEnabled && enemy.encounter != null && !enemy.IsLyingInWait;

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

        /// <summary>
        /// Steps up to the enemy and swings. With an ambush in mind (the thief), being spotted on the way calls it off; a melee class
        /// carries on regardless and simply fights an enemy that is now awake.
        /// </summary>
        IEnumerator Engage(OverworldEnemy target, bool ambush)
        {
            var player = _director.player;
            _striking = true;
            _walkArrived = false;

            var destination = target.NearestModelPosition(player.transform.position);
            var flat = destination - player.transform.position;
            flat.y = 0f;

            if (flat.magnitude > strikeDistance)
            {
                bool started = player.WalkTo(destination, strikeDistance, arrived => _walkArrived = arrived);
                if (!started)
                {
                    InteractionSystem.ShowMessage("There is no way to reach it.");
                    Finish();
                    yield break;
                }

                while (player.AutoWalking)
                {
                    if (target == null || (ambush && !PartyRules.Hidden))
                    {
                        player.CancelAutoWalk();
                        if (target != null) InteractionSystem.ShowMessage("SPOTTED!");
                        Finish();
                        yield break;
                    }

                    yield return null;
                }

                if (!_walkArrived || target == null)
                {
                    Finish();
                    yield break;
                }
            }

            // Close enough: strike. Whether it is still an ambush is decided now, by what the enemy knows.
            bool stillAmbush = ambush && PartyRules.Hidden;
            player.movementLocked = true;
            _director.AttackPending = true;
            FaceTowards(target);
            if (stillAmbush) InteractionSystem.ShowMessage("AMBUSH! The party strikes first.");
            float wait = SwingAnimation();
            if (wait > 0f) yield return new WaitForSeconds(wait);

            Finish();
            if (target != null && !_director.InBattle)
                _director.BeginEncounter(target, stillAmbush ? EncounterAdvantage.Party : EncounterAdvantage.None);
        }

        /// <summary>The ranger looses an arrow; it lands, and the fight starts (an ambush if the enemy had not noticed the party when it was fired).</summary>
        IEnumerator Shoot(OverworldEnemy target, bool ambush)
        {
            var player = _director.player;
            _striking = true;
            player.movementLocked = true;
            _director.AttackPending = true;
            FaceTowards(target);
            ambush = ambush && PartyRules.Hidden;
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

            // The cursor only appears when there is something in range to act on.
            if (_isAiming ? _aim.target == null : _hover == null) return;

            Color color = new Color(1f, 1f, 1f, 0.8f);
            string caption = "ATTACK";
            bool cross = false;
            float size = 1.2f;
            Vector2 point;

            if (_isAiming)
            {
                point = AimScreenPoint();
                if (_aim.ambush)
                {
                    color = new Color(0.45f, 1f, 0.5f, 0.95f);
                    caption = _aim.kind == AimKind.Shoot ? "AMBUSH SHOT" : "STEALTH ATTACK";
                    size = 1.5f;
                }
                else if (_aim.spotted)
                {
                    color = new Color(1f, 0.3f, 0.25f, 0.95f);
                    caption = _aim.kind == AimKind.Shoot ? "SPOTTED - NO SURPRISE" : "SPOTTED";
                    cross = true;
                    size = 1.4f;
                }
                else if (_aim.kind != AimKind.Engage)
                {
                    caption = null;
                    size = 1f;
                }
            }
            else
            {
                // Not aiming: a small marker over the enemy that a left click would walk up to and attack.
                var camera = Camera.main;
                if (camera == null) return;
                var from = _director.player.transform.position;
                var onScreen = camera.WorldToScreenPoint(_hover.NearestModelPosition(from) + Vector3.up * 1.2f);
                if (onScreen.z <= 0f) return;
                point = new Vector2(onScreen.x, onScreen.y);
            }

            float cx = point.x, cy = Screen.height - point.y; // IMGUI's y runs downwards
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
