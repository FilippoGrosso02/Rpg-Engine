using Ink.UnityIntegration;
using NUnit.Framework;
using UnityEditor;

namespace Game.Narrative.Tests.EditMode
{
    public class InkPackageSmokeTest
    {
        const string TestStoryPath = "Assets/_Project/Tests/EditMode/Narrative/TestStory.ink";

        [Test]
        public void TestStory_CompilesOnImport()
        {
            var inkFile = AssetDatabase.LoadAssetAtPath<InkFile>(TestStoryPath);

            Assert.IsNotNull(inkFile, "InkFile asset was not imported at " + TestStoryPath);
            Assert.IsTrue(inkFile.isCompiled, "TestStory.ink did not compile to story JSON.");
            Assert.IsFalse(inkFile.hasErrors, "TestStory.ink has compiler errors.");
        }

        [Test]
        public void TestStory_StoryCanContinue()
        {
            var inkFile = AssetDatabase.LoadAssetAtPath<InkFile>(TestStoryPath);
            Assert.IsTrue(inkFile.isCompiled);

            var story = new Ink.Runtime.Story(inkFile.storyJson);

            Assert.IsTrue(story.canContinue);
            string line = story.Continue();

            Assert.AreEqual("Hello, world!\n", line);
            Assert.IsFalse(story.canContinue);
        }
    }
}
