using Game.Overworld;
using NUnit.Framework;
using UnityEngine;

namespace Game.Overworld.Tests.EditMode
{
    public class OverworldLogicTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void CameraRelativeMove_NoYaw_ForwardInputMovesAlongWorldZ()
        {
            var move = PlayerController.GetCameraRelativeMove(new Vector2(0f, 1f), 0f);
            Assert.AreEqual(0f, move.x, Eps);
            Assert.AreEqual(0f, move.y, Eps);
            Assert.AreEqual(1f, move.z, Eps);
        }

        [Test]
        public void CameraRelativeMove_Yaw90_ForwardInputMovesAlongWorldX()
        {
            var move = PlayerController.GetCameraRelativeMove(new Vector2(0f, 1f), 90f);
            Assert.AreEqual(1f, move.x, Eps);
            Assert.AreEqual(0f, move.z, Eps);
        }

        [Test]
        public void CameraRelativeMove_Yaw180_RightInputMovesAlongNegativeX()
        {
            var move = PlayerController.GetCameraRelativeMove(new Vector2(1f, 0f), 180f);
            Assert.AreEqual(-1f, move.x, Eps);
            Assert.AreEqual(0f, move.z, Eps);
        }

        [Test]
        public void CameraRelativeMove_DiagonalInput_IsClampedToUnitLength()
        {
            var move = PlayerController.GetCameraRelativeMove(new Vector2(1f, 1f), 37f);
            Assert.AreEqual(1f, move.magnitude, Eps);
        }

        [Test]
        public void CameraRelativeMove_NoInput_IsZero()
        {
            Assert.AreEqual(Vector3.zero, PlayerController.GetCameraRelativeMove(Vector2.zero, 123f));
        }

        [Test]
        public void DefeatedEnemies_AddedIdIsRemembered_UnknownAndEmptyIdsAreNot()
        {
            DefeatedEnemies.Clear();
            Assert.IsFalse(DefeatedEnemies.Contains("goblin_1"));

            DefeatedEnemies.Add("goblin_1");
            Assert.IsTrue(DefeatedEnemies.Contains("goblin_1"));
            Assert.IsFalse(DefeatedEnemies.Contains("goblin_2"));

            DefeatedEnemies.Add(null);
            DefeatedEnemies.Add("");
            Assert.IsFalse(DefeatedEnemies.Contains(""));
            Assert.IsFalse(DefeatedEnemies.Contains(null));
        }

        [Test]
        public void DefeatedEnemies_ClearForgetsEverything()
        {
            DefeatedEnemies.Add("goblin_1");
            DefeatedEnemies.Clear();
            Assert.IsFalse(DefeatedEnemies.Contains("goblin_1"));
        }
    }
}
