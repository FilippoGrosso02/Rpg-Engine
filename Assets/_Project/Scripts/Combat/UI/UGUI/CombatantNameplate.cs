using Game.Combat.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// A screen-space (Overlay canvas) name + HP bar that tracks a combatant's
    /// CombatantRuntime.viewTransform every frame — constant pixel size regardless of camera
    /// distance, always drawn on top of the 3D scene. Used for both sides; BattleHudRoot picks
    /// the ally or enemy prefab, so each side's look lives entirely in its own prefab asset.
    /// </summary>
    public class CombatantNameplate : MonoBehaviour
    {
        [SerializeField] private PixelText nameLabel;
        [Tooltip("World-space offset from the combatant's view origin that the nameplate is pinned to.")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 2f, 0f);

        private Transform _target;
        private Camera _camera;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Nameplates sit directly on top of units — they must never swallow the click that
            // TargetingInput's IsPointerOverUi() gate would otherwise treat as a UI click.
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        /// <summary>
        /// Builds the plate: a one-line name over a 44x5 HP bar (dark frame, coloured fill). Anchored bottom-centre so the plate's
        /// origin is the point it hangs from above the unit.
        /// </summary>
        public void Build(Color fillColor)
        {
            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(60f, 16f);
            rect.pivot = new Vector2(0.5f, 0f);

            nameLabel = UiFactory.Text(transform, "Name", "", 0, 0, 60, 8, PixelText.Align.Center, UiFactory.Cream);

            var frame = UiFactory.Box(transform, "BarFrame", UiFactory.Dim, 8, 10, 44, 6);
            var back = UiFactory.Box(transform, "BarBack", new Color32(0x3A, 0x2A, 0x1E, 255), 9, 11, 42, 4);
            var fill = UiFactory.Box(transform, "BarFill", fillColor, 9, 11, 42, 4);
            frame.raycastTarget = false;
            back.raycastTarget = false;
            fill.raycastTarget = false;

            // Left-anchored so shrinking the width drains the bar from the right.
            fill.rectTransform.anchorMin = new Vector2(0f, 1f);
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);

            _fillRect = fill.rectTransform;
            _fillFullWidth = 42f;
        }

        [SerializeField] private RectTransform _fillRect;
        [SerializeField] private float _fillFullWidth = 42f;

        public void Initialize(CombatantRuntime combatant, Camera trackingCamera)
        {
            _target = combatant.viewTransform;
            _camera = trackingCamera;

            if (nameLabel != null)
            {
                nameLabel.text = combatant.displayName;
            }

            Refresh(combatant);
            UpdateScreenPosition();
        }

        public void Refresh(CombatantRuntime combatant)
        {
            if (combatant.maxHp > 0)
            {
                float fraction = Mathf.Clamp01((float)combatant.currentHp / combatant.maxHp);
                if (_fillRect != null)
                {
                    // Whole pixels only: a 42 px bar at 7/22 HP is 13 px, never 13.4.
                    _fillRect.sizeDelta = new Vector2(Mathf.Round(_fillFullWidth * fraction), _fillRect.sizeDelta.y);
                }
            }
        }

        private void LateUpdate()
        {
            UpdateScreenPosition();
        }

        /// <summary>
        /// Hidden (alpha 0, not SetActive — LateUpdate has to keep running to un-hide it) while
        /// the target's view is inactive, which is how the runner hides defeated combatants, or
        /// while the point is behind the camera.
        /// </summary>
        private void UpdateScreenPosition()
        {
            if (_target == null || _camera == null)
            {
                _canvasGroup.alpha = 0f;
                return;
            }

            var screenPoint = _camera.WorldToScreenPoint(_target.position + offset);
            var visible = _target.gameObject.activeInHierarchy && screenPoint.z > 0f;
            _canvasGroup.alpha = visible ? 1f : 0f;

            if (visible)
            {
                // On a Screen Space - Overlay canvas, world position == screen pixels.
                transform.position = new Vector3(screenPoint.x, screenPoint.y, 0f);
            }
        }
    }
}
