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
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Text nameLabel;
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
            if (hpSlider != null && combatant.maxHp > 0)
            {
                hpSlider.value = (float)combatant.currentHp / combatant.maxHp;
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
