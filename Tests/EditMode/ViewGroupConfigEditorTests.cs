using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Sentinal.Tests
{
    public class ViewGroupConfigEditorTests
    {
        private const string AssetPath = "Assets/SentinalViewGroupConfigPersistenceTest.asset";

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(AssetPath);

        [Test]
        public void AddedAndRenamedGroupsPersistAfterReload()
        {
            var config = ScriptableObject.CreateInstance<ViewGroupConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);

            Assert.That(config.TryAddGroup("Player One", out int groupIndex), Is.True);
            Assert.That(config.RenameGroup(groupIndex, "Player UI"), Is.True);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);

            Resources.UnloadAsset(config);
            var reloaded = AssetDatabase.LoadAssetAtPath<ViewGroupConfig>(AssetPath);

            Assert.That(reloaded.GroupCount, Is.EqualTo(2));
            Assert.That(reloaded.GetGroupName(groupIndex), Is.EqualTo("Player UI"));
        }
    }
}
