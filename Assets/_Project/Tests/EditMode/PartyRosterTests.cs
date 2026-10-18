using System.Linq;
using Game.Data.Definitions;
using Game.Data.Registry;
using Game.Data.State;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Data.Tests.EditMode
{
    public class PartyRosterTests
    {
        private string[] _ids;

        [SetUp]
        public void SetUp()
        {
            DatabaseRegistry.Initialize();
            _ids = DatabaseRegistry.GetAllActors().Select(a => a.id).ToArray();
            Assert.GreaterOrEqual(_ids.Length, 4, "These tests need at least 4 heroes in the database.");
            PartyRoster.Reset(null);
        }

        [TearDown]
        public void TearDown() => PartyRoster.Reset(null);

        [Test]
        public void Add_AppendsInOrder_AndRejectsDuplicates()
        {
            Assert.IsTrue(PartyRoster.Add(_ids[0]));
            Assert.IsTrue(PartyRoster.Add(_ids[1]));
            Assert.IsFalse(PartyRoster.Add(_ids[0]));

            CollectionAssert.AreEqual(new[] { _ids[0], _ids[1] }, PartyRoster.Members);
        }

        [Test]
        public void Add_RejectsMembersBeyondMaxSize()
        {
            for (int i = 0; i < PartyRoster.MaxSize; i++) Assert.IsTrue(PartyRoster.Add(_ids[i]));

            Assert.IsFalse(PartyRoster.Add(_ids[PartyRoster.MaxSize]));
            Assert.AreEqual(PartyRoster.MaxSize, PartyRoster.Count);
        }

        [Test]
        public void Add_UnknownHero_IsRejectedWithAWarning()
        {
            LogAssert.Expect(LogType.Warning, "PartyRoster: no hero with id 'nobody' in the database.");
            Assert.IsFalse(PartyRoster.Add("nobody"));
            Assert.AreEqual(0, PartyRoster.Count);
        }

        [Test]
        public void Remove_TakesMemberOut_AndFreesASlot()
        {
            for (int i = 0; i < PartyRoster.MaxSize; i++) PartyRoster.Add(_ids[i]);

            Assert.IsTrue(PartyRoster.Remove(_ids[0]));
            Assert.IsFalse(PartyRoster.Remove(_ids[0]));
            Assert.IsTrue(PartyRoster.Add(_ids[PartyRoster.MaxSize]));
            Assert.IsFalse(PartyRoster.Contains(_ids[0]));
        }

        [Test]
        public void Reset_LoadsStartingMembersFromAsset_SkippingNullsAndDuplicates()
        {
            var def = ScriptableObject.CreateInstance<PartyRosterDefinition>();
            var a = DatabaseRegistry.GetActor(_ids[0]);
            var b = DatabaseRegistry.GetActor(_ids[1]);
            def.startingMembers.AddRange(new[] { a, null, b, a });

            PartyRoster.Reset(def);

            CollectionAssert.AreEqual(new[] { _ids[0], _ids[1] }, PartyRoster.Members);
        }

        [Test]
        public void EnsureInitialized_OnlyAppliesOnce_SoLaterEditsSurviveASceneReload()
        {
            var def = ScriptableObject.CreateInstance<PartyRosterDefinition>();
            def.startingMembers.Add(DatabaseRegistry.GetActor(_ids[0]));

            PartyRoster.Reset(null);                  // marks the roster as initialised (and empty)
            PartyRoster.EnsureInitialized(def);        // must NOT overwrite it

            Assert.AreEqual(0, PartyRoster.Count);
        }

        [Test]
        public void Changed_FiresOnAddAndRemove()
        {
            int fired = 0;
            System.Action handler = () => fired++;
            PartyRoster.Changed += handler;
            try
            {
                PartyRoster.Add(_ids[0]);
                PartyRoster.Remove(_ids[0]);
                PartyRoster.Remove(_ids[0]); // no-op, no event
            }
            finally { PartyRoster.Changed -= handler; }

            Assert.AreEqual(2, fired);
        }
    }
}
