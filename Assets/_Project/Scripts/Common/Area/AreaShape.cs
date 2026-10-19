using UnityEngine;

namespace Game.Common.Area
{
    public enum AreaShapeKind
    {
        Circle = 0,
        Cone = 1,
        Line = 2
    }

    /// <summary>
    /// A 2D (ground-plane) area anchored at an origin and facing a direction. One definition serves both the visual
    /// (<see cref="AreaIndicator"/> sends it to the shader) and gameplay queries (<see cref="Contains"/>), so a preview always matches
    /// what is actually hit. Local space: x = right, y = forward. <see cref="SignedDistance"/> is mirrored in AreaIndicator.shader.
    /// </summary>
    [System.Serializable]
    public struct AreaShape
    {
        public AreaShapeKind kind;

        /// <summary>Circle: outer radius. Cone and Line: length from the origin.</summary>
        public float range;

        /// <summary>Circle only: radius of the empty centre (0 = solid disc, i.e. a plain circle rather than a ring).</summary>
        public float innerRadius;

        /// <summary>Cone only: full opening angle in degrees (clamped to 1-180).</summary>
        public float angle;

        /// <summary>Line only: full width.</summary>
        public float width;

        public static AreaShape Circle(float radius, float innerRadius = 0f) =>
            new AreaShape { kind = AreaShapeKind.Circle, range = radius, innerRadius = innerRadius };

        public static AreaShape Cone(float length, float angleDegrees) =>
            new AreaShape { kind = AreaShapeKind.Cone, range = length, angle = angleDegrees };

        public static AreaShape Line(float length, float width) =>
            new AreaShape { kind = AreaShapeKind.Line, range = length, width = width };

        /// <summary>Half the cone's opening angle in radians, clamped so the maths stays valid.</summary>
        public float HalfAngleRadians => Mathf.Clamp(angle, 1f, 180f) * 0.5f * Mathf.Deg2Rad;

        /// <summary>Negative inside the shape, positive outside, zero on the edge. Point is in local space (x right, y forward).</summary>
        public float SignedDistance(Vector2 local)
        {
            switch (kind)
            {
                case AreaShapeKind.Cone:
                {
                    float half = HalfAngleRadians;
                    float wedge = Mathf.Abs(local.x) * Mathf.Cos(half) - local.y * Mathf.Sin(half);
                    return Mathf.Max(local.magnitude - range, wedge);
                }

                case AreaShapeKind.Line:
                {
                    float qx = Mathf.Abs(local.x) - width * 0.5f;
                    float qy = Mathf.Abs(local.y - range * 0.5f) - range * 0.5f;
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                    return outside + Mathf.Min(Mathf.Max(qx, qy), 0f);
                }

                default:
                {
                    float d = local.magnitude;
                    float sd = d - range;
                    return innerRadius > 0f ? Mathf.Max(sd, innerRadius - d) : sd;
                }
            }
        }

        /// <summary>True if <paramref name="point"/> lies inside the shape on the ground plane (height is ignored). Forward is flattened; zero means +Z.</summary>
        public bool Contains(Vector3 origin, Vector3 forward, Vector3 point)
        {
            return SignedDistance(ToLocal(origin, forward, point)) <= 0f;
        }

        /// <summary>The shape's bounding box in local space (centre and half-size on the x/y ground axes), for sizing a projector volume.</summary>
        public void LocalBounds(out Vector2 center, out Vector2 halfSize)
        {
            switch (kind)
            {
                case AreaShapeKind.Cone:
                {
                    float half = HalfAngleRadians;
                    float sideways = half >= Mathf.PI * 0.5f ? range : range * Mathf.Sin(half);
                    float backward = half > Mathf.PI * 0.5f ? range * -Mathf.Cos(half) : 0f;
                    float zMin = -backward;
                    center = new Vector2(0f, (zMin + range) * 0.5f);
                    halfSize = new Vector2(sideways, (range - zMin) * 0.5f);
                    break;
                }

                case AreaShapeKind.Line:
                    center = new Vector2(0f, range * 0.5f);
                    halfSize = new Vector2(width * 0.5f, range * 0.5f);
                    break;

                default:
                    center = Vector2.zero;
                    halfSize = new Vector2(range, range);
                    break;
            }
        }

        static Vector2 ToLocal(Vector3 origin, Vector3 forward, Vector3 point)
        {
            forward.y = 0f;
            forward = forward.sqrMagnitude < 1e-6f ? Vector3.forward : forward.normalized;
            var right = new Vector3(forward.z, 0f, -forward.x);
            Vector3 delta = point - origin;
            return new Vector2(Vector3.Dot(delta, right), Vector3.Dot(delta, forward));
        }
    }
}
