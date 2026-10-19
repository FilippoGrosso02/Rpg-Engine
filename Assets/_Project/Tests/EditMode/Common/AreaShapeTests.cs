using Game.Common.Area;
using NUnit.Framework;
using UnityEngine;

namespace Game.Common.Tests.EditMode
{
    public class AreaShapeTests
    {
        static readonly Vector3 Origin = new Vector3(10f, 5f, -3f);
        static readonly Vector3 North = Vector3.forward;

        static Vector3 At(float right, float forward, float up = 0f) => Origin + new Vector3(right, up, forward);

        [Test]
        public void Circle_ContainsPointsInsideRadius_AndIgnoresHeight()
        {
            var circle = AreaShape.Circle(3f);
            Assert.IsTrue(circle.Contains(Origin, North, At(0, 0)));
            Assert.IsTrue(circle.Contains(Origin, North, At(2.9f, 0)));
            Assert.IsTrue(circle.Contains(Origin, North, At(0, -2f, up: 40f)));
            Assert.IsFalse(circle.Contains(Origin, North, At(3.1f, 0)));
            Assert.IsFalse(circle.Contains(Origin, North, At(2.5f, 2.5f)));
        }

        [Test]
        public void Ring_ExcludesTheCentre()
        {
            var ring = AreaShape.Circle(4f, innerRadius: 2f);
            Assert.IsFalse(ring.Contains(Origin, North, At(1f, 0)));
            Assert.IsTrue(ring.Contains(Origin, North, At(3f, 0)));
            Assert.IsFalse(ring.Contains(Origin, North, At(4.5f, 0)));
        }

        [Test]
        public void Cone_CoversTheWedgeInFront_NotBehindOrBeside()
        {
            var cone = AreaShape.Cone(length: 6f, angleDegrees: 90f); // +-45 degrees
            Assert.IsTrue(cone.Contains(Origin, North, At(0, 5f)));
            Assert.IsTrue(cone.Contains(Origin, North, At(2f, 3f)));
            Assert.IsFalse(cone.Contains(Origin, North, At(4f, 3f)), "about 53 degrees off-axis");
            Assert.IsFalse(cone.Contains(Origin, North, At(0, -1f)), "behind the origin");
            Assert.IsFalse(cone.Contains(Origin, North, At(0, 6.5f)), "past the length");
        }

        [Test]
        public void Cone_RotatesWithTheFacingDirection()
        {
            var cone = AreaShape.Cone(6f, 60f);
            Vector3 east = Vector3.right;
            Assert.IsTrue(cone.Contains(Origin, east, At(5f, 0f)));
            Assert.IsFalse(cone.Contains(Origin, east, At(0f, 5f)));
        }

        [Test]
        public void Line_IsALongThinBoxStartingAtTheOrigin()
        {
            var line = AreaShape.Line(length: 8f, width: 2f);
            Assert.IsTrue(line.Contains(Origin, North, At(0.9f, 7.9f)));
            Assert.IsTrue(line.Contains(Origin, North, At(-0.9f, 0.1f)));
            Assert.IsFalse(line.Contains(Origin, North, At(1.2f, 4f)), "too wide");
            Assert.IsFalse(line.Contains(Origin, North, At(0f, 8.2f)), "too long");
            Assert.IsFalse(line.Contains(Origin, North, At(0f, -0.3f)), "behind");
        }

        [Test]
        public void ZeroForward_FallsBackToWorldForward()
        {
            var cone = AreaShape.Cone(5f, 60f);
            Assert.IsTrue(cone.Contains(Origin, Vector3.zero, At(0, 3f)));
        }

        [Test]
        public void SignedDistance_IsNegativeInside_PositiveOutside_ZeroOnTheEdge()
        {
            var circle = AreaShape.Circle(3f);
            Assert.Less(circle.SignedDistance(new Vector2(1f, 0f)), 0f);
            Assert.AreEqual(0f, circle.SignedDistance(new Vector2(0f, 3f)), 1e-5f);
            Assert.AreEqual(2f, circle.SignedDistance(new Vector2(5f, 0f)), 1e-5f);
        }

        [Test]
        public void LocalBounds_EnclosesEveryContainedPoint()
        {
            var shapes = new[]
            {
                AreaShape.Circle(4f),
                AreaShape.Cone(6f, 60f),
                AreaShape.Cone(6f, 180f),
                AreaShape.Cone(6f, 270f), // clamped to 180
                AreaShape.Line(8f, 2f),
            };

            foreach (var shape in shapes)
            {
                shape.LocalBounds(out var center, out var half);
                for (float x = -10f; x <= 10f; x += 0.25f)
                for (float y = -10f; y <= 10f; y += 0.25f)
                {
                    if (shape.SignedDistance(new Vector2(x, y)) > 0f) continue;
                    Assert.LessOrEqual(Mathf.Abs(x - center.x), half.x + 1e-3f, $"{shape.kind} x at ({x},{y})");
                    Assert.LessOrEqual(Mathf.Abs(y - center.y), half.y + 1e-3f, $"{shape.kind} y at ({x},{y})");
                }
            }
        }
    }
}
