using Game.Combat.TrailsBattle3D.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Game.Combat.Tests.EditMode
{
    public class InPlaceFormationTests
    {
        static Vector3 Open(Vector3 from, Vector3 wanted) => wanted;

        [Test]
        public void GroupsInContactArePushedApartToTheMinimumGap()
        {
            var layout = InPlaceFormation.Compute(Vector3.zero, new Vector3(0f, 0f, 1f), Vector3.forward, 1, 1, Open);

            float gap = Vector3.Distance(layout.party[0], layout.enemies[0]);
            Assert.AreEqual(InPlaceFormation.MinGroupGap, gap, 0.01f);
        }

        [Test]
        public void GroupsAlreadyFarApartStayWhereTheyWere()
        {
            var party = new Vector3(0f, 0f, 0f);
            var enemy = new Vector3(0f, 0f, 9f);

            var layout = InPlaceFormation.Compute(party, enemy, Vector3.forward, 1, 1, Open);

            Assert.AreEqual(party, layout.party[0]);
            Assert.AreEqual(enemy, layout.enemies[0]);
        }

        [Test]
        public void PartyFacesTheEnemyGroup()
        {
            var layout = InPlaceFormation.Compute(Vector3.zero, new Vector3(6f, 0f, 0f), Vector3.forward, 2, 2, Open);

            Assert.AreEqual(Vector3.right, layout.forward);
        }

        [Test]
        public void FollowersStandBehindTheLeaderAndEnemiesBehindTheirFirst()
        {
            var layout = InPlaceFormation.Compute(Vector3.zero, new Vector3(0f, 0f, 8f), Vector3.forward, 3, 3, Open);

            Assert.Less(layout.party[1].z, layout.party[0].z);
            Assert.Less(layout.party[2].z, layout.party[0].z);
            Assert.Greater(layout.enemies[1].z, layout.enemies[0].z);
            Assert.Greater(layout.enemies[2].z, layout.enemies[0].z);
        }

        [Test]
        public void SlotsPastTheTableGetAnExtraRowInsteadOfOverlapping()
        {
            var layout = InPlaceFormation.Compute(Vector3.zero, new Vector3(0f, 0f, 8f), Vector3.forward, 7, 7, Open);

            for (int i = 0; i < 7; i++)
            {
                for (int j = i + 1; j < 7; j++)
                {
                    Assert.Greater(Vector3.Distance(layout.party[i], layout.party[j]), 0.5f, $"party {i}/{j}");
                    Assert.Greater(Vector3.Distance(layout.enemies[i], layout.enemies[j]), 0.5f, $"enemies {i}/{j}");
                }
            }
        }

        [Test]
        public void IdenticalPositionsFallBackToTheGivenDirection()
        {
            var layout = InPlaceFormation.Compute(Vector3.zero, Vector3.zero, Vector3.left, 1, 1, Open);

            Assert.AreEqual(Vector3.left, layout.forward);
        }

        [Test]
        public void ABlockedBackOffIsMadeUpByPushingTheEnemiesInstead()
        {
            // The party stands against a wall behind it and cannot move at all.
            Vector3 Wall(Vector3 from, Vector3 wanted) => wanted.z < from.z ? from : wanted;

            var layout = InPlaceFormation.Compute(Vector3.zero, new Vector3(0f, 0f, 1f), Vector3.forward, 1, 1, Wall);

            Assert.AreEqual(Vector3.zero, layout.party[0]);
            Assert.AreEqual(InPlaceFormation.MinGroupGap, Vector3.Distance(layout.party[0], layout.enemies[0]), 0.01f);
        }
    }
}
