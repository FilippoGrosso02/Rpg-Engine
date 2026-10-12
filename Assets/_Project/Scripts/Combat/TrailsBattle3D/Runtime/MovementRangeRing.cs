using UnityEngine;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Procedural ground ring showing how far the acting player unit can still move this
    /// turn, anchored to its position at turn-start. Built from a LineRenderer at runtime so
    /// no prefab or material asset is needed.
    /// </summary>
    public class MovementRangeRing : MonoBehaviour
    {
        private const int Segments = 48;
        private LineRenderer _line;

        private void Awake()
        {
            _line = gameObject.AddComponent<LineRenderer>();
            _line.loop = true;
            _line.useWorldSpace = true;
            _line.widthMultiplier = 0.08f;
            _line.positionCount = Segments;
            _line.material = new Material(Shader.Find("Sprites/Default"));
            _line.startColor = _line.endColor = new Color(0.3f, 0.9f, 1f, 0.85f);
            gameObject.SetActive(false);
        }

        public void ShowAt(Vector3 center, float radius)
        {
            for (int i = 0; i < Segments; i++)
            {
                float angle = (float)i / Segments * Mathf.PI * 2f;
                var point = center + new Vector3(Mathf.Cos(angle) * radius, 0.05f, Mathf.Sin(angle) * radius);
                _line.SetPosition(i, point);
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
