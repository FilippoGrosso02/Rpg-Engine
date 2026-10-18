using System.Collections.Generic;
using Game.Combat.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Camera raycast into the arena: click a CombatantView to attack it, click a
    /// BattleArenaFloor to move there. Identifies hits by component rather than physics
    /// layer, so no ProjectSettings/layer setup is required to use it.
    /// </summary>
    public class TargetingInput : MonoBehaviour
    {
        public Camera arenaCamera;
        public float maxRayDistance = 200f;

        private void Awake()
        {
            if (arenaCamera == null)
            {
                arenaCamera = Camera.main;
            }
        }

        public bool TryRaycastCombatant(out CombatantView view)
        {
            view = null;

            if (arenaCamera == null || IsPointerOverUi())
            {
                return false;
            }

            var ray = arenaCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, maxRayDistance))
            {
                view = hit.collider.GetComponentInParent<CombatantView>();
                return view != null;
            }

            return false;
        }

        /// <summary>
        /// Picks the candidate whose body is closest to the mouse on screen (within <paramref name="maxPixels"/>).
        /// Needed for hidden, collider-less targets such as a defeated ally about to be revived.
        /// </summary>
        public bool TryPickNearestOnScreen(IEnumerable<CombatantRuntime> candidates, out CombatantRuntime picked, float maxPixels = 90f)
        {
            picked = null;

            if (arenaCamera == null || IsPointerOverUi())
            {
                return false;
            }

            Vector2 mouse = Input.mousePosition;
            float best = maxPixels;
            foreach (var candidate in candidates)
            {
                Vector3 screen = arenaCamera.WorldToScreenPoint(candidate.position + Vector3.up * 1f);
                if (screen.z <= 0f)
                {
                    continue;
                }

                float distance = Vector2.Distance(mouse, new Vector2(screen.x, screen.y));
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

            if (arenaCamera == null || IsPointerOverUi())
            {
                return false;
            }

            var ray = arenaCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, maxRayDistance))
            {
                if (hit.collider.GetComponentInParent<BattleArenaFloor>() != null)
                {
                    point = hit.point;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Physics.Raycast has no idea the HUD's Screen Space canvas exists — without this,
        /// clicking a button (Guard, a Craft entry, Cancel...) also fires a 3D raycast
        /// straight through it into the arena underneath, potentially moving the actor or
        /// hitting a combatant the player never meant to target.
        /// </summary>
        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
