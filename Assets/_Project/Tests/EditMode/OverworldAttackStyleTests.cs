using Game.Data.Enums;
using Game.Data.Registry;
using NUnit.Framework;

namespace Game.Data.Tests.EditMode
{
    /// <summary>The classes' overworld opening moves are data; this pins what the design asks for.</summary>
    public class OverworldAttackStyleTests
    {
        [TestCase("class_thief", OverworldAttackStyle.Stealth)]
        [TestCase("class_ranger", OverworldAttackStyle.Ranged)]
        [TestCase("class_warrior", OverworldAttackStyle.Melee)]
        public void ClassOpensFightsTheWayTheDesignSays(string classId, OverworldAttackStyle expected)
        {
            var classDefinition = DatabaseRegistry.GetClass(classId);

            Assert.IsNotNull(classDefinition, classId);
            Assert.AreEqual(expected, classDefinition.overworldAttack);
        }
    }
}
