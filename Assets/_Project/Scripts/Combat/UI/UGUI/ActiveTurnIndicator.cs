using UnityEngine;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// A single world-space marker that BattleHudRoot repositions onto whichever
    /// combatant's viewTransform is currently acting. One instance covers both allies
    /// and enemies since only one combatant ever acts at a time.
    /// </summary>
    public class ActiveTurnIndicator : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.5f, 0f);
        [SerializeField] private float bobSpeed = 4f;
        [SerializeField] private float bobHeight = 0.15f;

        private Camera _billboardCamera;
        private Transform _target;

        public void ShowOn(Transform target)
        {
            _target = target;

            if (target == null)
            {
                gameObject.SetActive(false);
                return;
            }

            _billboardCamera = Camera.main;
            gameObject.SetActive(true);
            transform.position = target.position + offset;
        }

        public void Hide()
        {
            _target = null;
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = _target.position + offset + new Vector3(0f, bob, 0f);

            if (_billboardCamera != null)
            {
                transform.forward = _billboardCamera.transform.forward;
            }
        }
    }
}
