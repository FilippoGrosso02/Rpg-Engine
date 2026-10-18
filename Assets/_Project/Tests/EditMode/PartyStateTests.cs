using Game.Data.State;
using NUnit.Framework;

namespace Game.Data.Tests.EditMode
{
    public class PartyStateTests
    {
        [SetUp]
        public void SetUp() => PartyState.HealAll();

        [TearDown]
        public void TearDown() => PartyState.HealAll();

        [Test]
        public void UnknownActor_StartsAtFullHp()
        {
            Assert.AreEqual(20, PartyState.GetStartingHp("hero", 20));
        }

        [Test]
        public void SavedHp_IsUsedForNextFight()
        {
            PartyState.SetHp("hero", 7);
            Assert.AreEqual(7, PartyState.GetStartingHp("hero", 20));
        }

        [Test]
        public void KnockedOutMember_IsStoredAsOneHp()
        {
            PartyState.SetHp("hero", 0);
            Assert.AreEqual(1, PartyState.GetStartingHp("hero", 20));
        }

        [Test]
        public void SavedHp_IsClampedToCurrentMaxHp()
        {
            PartyState.SetHp("hero", 30);
            Assert.AreEqual(20, PartyState.GetStartingHp("hero", 20));
        }

        [Test]
        public void HealAll_ForgetsEveryone()
        {
            PartyState.SetHp("hero", 3);
            PartyState.SetHp("nun", 5);
            PartyState.HealAll();
            Assert.AreEqual(20, PartyState.GetStartingHp("hero", 20));
            Assert.AreEqual(12, PartyState.GetStartingHp("nun", 12));
        }
    }
}
