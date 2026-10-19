using UnityEngine;

namespace Game.Common.Area
{
    /// <summary>
    /// Dev sandbox: on Play, spawns a circle, a cone and a line indicator at the given anchors and keeps redrawing them every frame,
    /// so changing any value in the Inspector (range, angle, colours, height window...) shows up live. Use it in the
    /// AreaIndicator_Sandbox scene to check how the shader follows stairs and slopes.
    /// </summary>
    public class AreaIndicatorSandbox : MonoBehaviour
    {
        public Transform circleAnchor;
        public Transform coneAnchor;
        public Transform lineAnchor;

        [Header("Circle")]
        [Range(0.5f, 12f)] public float circleRadius = 5f;
        [Range(0f, 8f)] public float circleInnerRadius = 0f;

        [Header("Cone")]
        [Range(1f, 14f)] public float coneLength = 8f;
        [Range(10f, 180f)] public float coneAngle = 70f;

        [Header("Line")]
        [Range(1f, 16f)] public float lineLength = 10f;
        [Range(0.5f, 6f)] public float lineWidth = 2f;

        [Header("Edge style (0 length = solid)")]
        [Min(0f)] public float dashLength = 0.4f;
        [Min(0f)] public float dashGap = 0.25f;

        [Header("Shared")]
        [Range(0.5f, 6f)] public float heightAbove = 2.5f;
        [Range(0.5f, 6f)] public float heightBelow = 1.5f;

        AreaIndicator _circle, _cone, _line;

        void Start()
        {
            _circle = Make("Circle", new Color(0.3f, 0.9f, 1f));
            _cone = Make("Cone", new Color(1f, 0.55f, 0.2f));
            _line = Make("Line", new Color(0.8f, 0.4f, 1f));
        }

        void Update()
        {
            Draw(_circle, circleAnchor, AreaShape.Circle(circleRadius, circleInnerRadius));
            Draw(_cone, coneAnchor, AreaShape.Cone(coneLength, coneAngle));
            Draw(_line, lineAnchor, AreaShape.Line(lineLength, lineWidth));
        }

        void Draw(AreaIndicator indicator, Transform anchor, AreaShape shape)
        {
            if (indicator == null || anchor == null) return;
            indicator.shape = shape;
            indicator.heightAbove = heightAbove;
            indicator.heightBelow = heightBelow;
            indicator.dashLength = dashLength;
            indicator.dashGap = dashGap;
            indicator.Show(anchor.position, anchor.forward);
        }

        AreaIndicator Make(string label, Color color)
        {
            var go = new GameObject("AreaIndicator_" + label);
            go.transform.SetParent(transform, false);
            var indicator = go.AddComponent<AreaIndicator>();
            indicator.fillColor = new Color(color.r, color.g, color.b, 0.18f);
            indicator.edgeColor = new Color(color.r, color.g, color.b, 0.95f);
            return indicator;
        }
    }
}
