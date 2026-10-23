using System.Collections.Generic;
using Game.Combat.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Camera raycast into the arena: aim at a CombatantView to attack it, or at a
    /// BattleArenaFloor to move there. Identifies hits by component rather than physics
    /// layer, so no ProjectSettings/layer setup is required to use it.
    /// While the cursor is locked (third-person look) the ray comes from the screen centre, i.e. the
    /// crosshair; while the cursor is free (Alt / Tab) it comes from the mouse as before.
    /// </summary>
    public class TargetingInput : MonoBehaviour
    {
        public Camera arenaCamera;
        public float maxRayDistance = 200f;

        /// <summary>The acting unit. Its own colliders are skipped, otherwise a camera sitting behind it would always hit it first.</summary>
        public CombatantView ignoredView;

        private readonly RaycastHit[] _hits = new RaycastHit[32];

        public bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;

        public Vector2 AimScreenPoint => CursorLocked
            ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
            : (Vector2)Input.mousePosition;

        private void Awake()
        {
            if (arenaCamera == null)
            {
                arenaCamera = Camera.main;
            }
        }

        /// <summary>Nearest hit along the aim ray that does not belong to <see cref="ignoredView"/>.</summary>
        private bool TryAimRaycast(out RaycastHit best)
        {
            best = default;

            if (arenaCamera == null || IsPointerOverUi())
            {
                return false;
            }

            var ray = arenaCamera.ScreenPointToRay(AimScreenPoint);
            int count = Physics.RaycastNonAlloc(ray, _hits, maxRayDistance);

            float bestDistance = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.distance >= bestDistance)
                {
                    continue;
                }

                if (ignoredView != null && hit.collider.GetComponentInParent<CombatantView>() == ignoredView)
                {
                    continue;
                }

                best = hit;
                bestDistance = hit.distance;
                found = true;
            }

            return found;
        }

        public bool TryRaycastCombatant(out CombatantView view)
        {
            view = null;

            if (TryAimRaycast(out var hit))
            {
                view = hit.collider.GetComponentInParent<CombatantView>();
                return view != null;
            }

            return false;
        }

        /// <summary>
        /// Picks the candidate whose body is closest to the aim point on screen (within <paramref name="maxPixels"/>).
        /// Needed for hidden, collider-less targets such as a defeated ally about to be revived.
        /// </summary>
        public bool TryPickNearestOnScreen(IEnumerable<CombatantRuntime> candidates, out CombatantRuntime picked, float maxPixels = 90f)
        {
            picked = null;

            if (arenaCamera == null || IsPointerOverUi())
            {
                return false;
            }

            Vector2 aim = AimScreenPoint;
            float best = maxPixels;
            foreach (var candidate in candidates)
            {
                Vector3 screen = arenaCamera.WorldToScreenPoint(candidate.position + Vector3.up * 1f);
                if (screen.z <= 0f)
                {
                    continue;
                }

                float distance = Vector2.Distance(aim, new Vector2(screen.x, screen.y));
                if (distance < best)
                {
                    best = distance;
                    picked = candidate;
                }
            }

            return picked != null;
        }

        public bool TryRaycastGround(out Vector3 point)
        {
            point = default;

            if (TryAimRaycast(out var hit) && hit.collider.GetComponentInParent<BattleArenaFloor>() != null)
            {
                point = hit.point;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Physics.Raycast has no idea the HUD's Screen Space canvas exists — without this,
        /// clicking a button (Guard, a Craft entry, Cancel...) also fires a 3D raycast
        /// straight through it into the arena underneath, potentially moving the actor or
        /// hitting a combatant the player never meant to target. Only relevant while the
        /// cursor is free; a locked cursor can't be over any UI.
        /// </summary>
        private bool IsPointerOverUi()
        {
            return !CursorLocked && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
