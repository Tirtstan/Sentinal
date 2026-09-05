using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sentinal.Editor
{
    internal static class ViewGroupUsageScanner
    {
        public static bool IsGroupBitUsed(int groupIndex, out string firstUsage)
        {
            int groupBit = 1 << groupIndex;
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && ContainsMaskBit(prefab, groupBit))
                {
                    firstUsage = path;
                    return true;
                }
            }

            string[] assetGuids = AssetDatabase.FindAssets("t:ScriptableObject");
            for (int i = 0; i < assetGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
                {
                    if (ContainsMaskBit(assets[assetIndex], groupBit))
                    {
                        firstUsage = path;
                        return true;
                    }
                }
            }

            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                Scene scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    GameObject[] roots = scene.GetRootGameObjects();
                    for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                    {
                        if (!ContainsMaskBit(roots[rootIndex], groupBit))
                            continue;

                        firstUsage = path;
                        return true;
                    }
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }

            firstUsage = null;
            return false;
        }

        private static bool ContainsMaskBit(GameObject root, int groupBit)
        {
            MonoBehaviour[] components = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (ContainsMaskBit(components[i], groupBit))
                    return true;
            }

            return false;
        }

        private static bool ContainsMaskBit(Object target, int groupBit)
        {
            if (target == null || target is ViewGroupConfig)
                return false;

            var serializedObject = new SerializedObject(target);
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.Next(enterChildren))
            {
                enterChildren = true;
                if (iterator.type != nameof(ViewGroupMask))
                    continue;

                SerializedProperty value = iterator.FindPropertyRelative("value");
                if (value != null && (value.intValue & groupBit) != 0)
                    return true;
            }

            return false;
        }
    }
}
