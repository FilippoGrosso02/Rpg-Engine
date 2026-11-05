using NUnit.Framework;

namespace Game.Overworld.Tests.EditMode
{
    public class PartyRulesTests
    {
        [SetUp]
        public void SetUp()
        {
            PartyRules.SetMode(PartyMode.Attached);
            PartyRules.EndHidden();
        }

        [Test]
        public void Attached_CannotHide_ButCanSwitchLeader()
        {
            Assert.IsTrue(PartyRules.CanSwitchLeader);
            Assert.IsFalse(PartyRules.BecomeHidden());
            Assert.IsFalse(PartyRules.Hidden);
        }

        [Test]
        public void Detached_CanHide_ButCannotSwitchLeader()
        {
            PartyRules.SetMode(PartyMode.Detached);

            Assert.IsFalse(PartyRules.CanSwitchLeader);
            Assert.IsTrue(PartyRules.BecomeHidden());
            Assert.IsTrue(PartyRules.Hidden);
        }

        [Test]
        public void Attaching_EndsHidden()
        {
            PartyRules.SetMode(PartyMode.Detached);
            PartyRules.BecomeHidden();

            PartyRules.SetMode(PartyMode.Attached);

            Assert.IsFalse(PartyRules.Hidden);
        }

        [Test]
        public void Check_IsD10PlusStatAgainstDifficulty()
        {
            // Stat 7 against DV 7: a roll of 6 or more passes, below fails.
            Assert.IsTrue(PartyRules.Check(6, 7, 7));
            Assert.IsFalse(PartyRules.Check(5, 7, 7));
            Assert.IsFalse(PartyRules.Check(1, 12, 2), "a natural 1 always fails");
            Assert.IsTrue(PartyRules.Check(10, 2, 12), "a natural 10 always passes");
        }

        [Test]
        public void HighestScore_SingleWinner()
        {
            CollectionAssert.AreEqual(new[] { 1 }, PartyRules.HighestScoreIndices(new[] { 7, 10, 8 }));
        }

        [Test]
        public void HighestScore_TieReturnsEveryTiedMember()
        {
            CollectionAssert.AreEqual(new[] { 0, 2 }, PartyRules.HighestScoreIndices(new[] { 9, 7, 9 }));
        }

        [Test]
        public void HighestScore_NoScores_IsEmpty()
        {
            Assert.IsEmpty(PartyRules.HighestScoreIndices(new int[0]));
        }
    }
}
