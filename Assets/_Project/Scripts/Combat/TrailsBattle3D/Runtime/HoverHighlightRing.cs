using UnityEngine;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Ground ring shown under whichever combatant the targeting raycast is currently
    /// hovering. One shared, re-colored instance — only one thing can be hovered at a time.
    /// </summary>
    public class HoverHighlightRing : MonoBehaviour
    {
        private const int Segments = 32;
        private const float Radius = 0.9f;

        private LineRenderer _line;
        private Transform _target;

        private void Awake()
        {
            _line = gameObject.AddComponent<LineRenderer>();
            _line.loop = true;
            _line.useWorldSpace = true;
            _line.widthMultiplier = 0.06f;
            _line.positionCount = Segments;
            _line.material = new Material(Shader.Find("Sprites/Default"));
            gameObject.SetActive(false);
        }

        public void ShowOn(Transform target, Color color)
        {
            _target = target;

            if (target == null)
            {
                gameObject.SetActive(false);
                return;
            }

            _line.startColor = _line.endColor = color;
            gameObject.SetActive(true);
            Redraw(target.position);
        }

        public void Hide()
        {
            _target = null;
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_target != null)
            {
                Redraw(_target.position);
            }
        }

        private void Redraw(Vector3 center)
        {
            for (int i = 0; i < Segments; i++)
            {
                float angle = (float)i / Segments * Mathf.PI * 2f;
                var point = center + new Vector3(Mathf.Cos(angle) * Radius, 0.05f, Mathf.Sin(angle) * Radius);
                _line.SetPosition(i, point);
            }
        }
    }
}
