using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace Sentinal
{
    /// <summary>
    /// Configuration asset for managing view groups.
    /// Groups allow filtering exclusive/hide behaviors to only affect views within the same group(s).
    /// Stored as a global asset in a Resources folder and auto-loaded by Sentinal.
    /// </summary>
    [CreateAssetMenu(fileName = "SentinalViewGroups", menuName = "Sentinal/View Groups")]
    public class ViewGroupConfig : ScriptableObject
    {
        /// <summary>
        /// The Resources path used when loading the shared group config.
        /// This should match the asset name and relative path inside a Resources folder.
        /// </summary>
        public const string DefaultResourcePath = "SentinalViewGroups";

        [Header("Groups")]
        [Tooltip("List of user-defined group names. Duplicates are automatically cleaned up.")]
        public List<string> Groups = new();

        /// <summary>
        /// Gets the total number of groups, including the hardcoded "Default" group.
        /// </summary>
        public int GroupCount => Groups.Count + 1;

        /// <summary>
        /// Resolves the group name at a specific index, handling "Default" at index 0.
        /// </summary>
        public string GetGroupName(int index)
        {
            if (index == 0)
                return "Default";

            int userIndex = index - 1;
            if (userIndex < 0 || userIndex >= Groups.Count)
                return null;

            return Groups[userIndex];
        }

        /// <summary>
        /// Loads the shared ViewGroupConfig from Resources.
        /// Searches all resources if not found at the default path.
        /// </summary>
        public static ViewGroupConfig LoadShared()
        {
            var config = Resources.Load<ViewGroupConfig>(DefaultResourcePath);
            if (config != null)
                return config;

#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:ViewGroupConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var asset = AssetDatabase.LoadAssetAtPath<ViewGroupConfig>(path);
                if (asset != null)
                    return asset;
            }
#endif

            var fallback = Resources.LoadAll<ViewGroupConfig>("");
            if (fallback.Length > 0)
            {
                Debug.LogWarning(
                    $"[Sentinal] ViewGroupConfig not found at '{DefaultResourcePath}'. "
                        + $"Using fallback at '{fallback[0].name}'. Move it to Resources/{DefaultResourcePath}.asset.",
                    fallback[0]
                );
                return fallback[0];
            }

            return null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Ensures a single shared ViewGroupConfig asset exists at Assets/Resources/SentinalViewGroups.asset.
        /// Does not overwrite an existing asset at that path; only creates it if missing.
        /// </summary>
        public static ViewGroupConfig EnsureSharedInProject()
        {
            const string directory = "Assets/Resources";
            const string assetPath = "Assets/Resources/SentinalViewGroups.asset";

            var existing = AssetDatabase.LoadAssetAtPath<ViewGroupConfig>(assetPath);
            if (existing != null)
                return existing;

            var instance = CreateInstance<ViewGroupConfig>();

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            AssetDatabase.CreateAsset(instance, assetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Sentinal] Created <b>SentinalViewGroups</b> asset in <b>{directory}</b>.", instance);

            return instance;
        }

        [InitializeOnLoadMethod]
        private static void EnsureSharedAssetOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                EnsureSharedInProject();
            };
        }

        private void OnValidate()
        {
            var uniqueGroups = new List<string>();
            bool changed = false;

            for (int i = 0; i < Groups.Count; i++)
            {
                string group = Groups[i];
                if (string.IsNullOrWhiteSpace(group))
                {
                    group = $"Group {uniqueGroups.Count + 1}";
                    changed = true;
                    Debug.LogWarning($"[Sentinal] Empty group name replaced with '{group}'.", this);
                }

                if (group.Equals("Default", System.StringComparison.OrdinalIgnoreCase))
                {
                    changed = true;
                    Debug.LogWarning(
                        $"[Sentinal] Removed 'Default' from user groups list because it is automatically prepended at index 0.",
                        this
                    );
                    continue;
                }

                if (uniqueGroups.Contains(group))
                {
                    changed = true;
                    Debug.LogWarning($"[Sentinal] Removed duplicate group name '{group}' from user groups list.", this);
                    continue;
                }

                uniqueGroups.Add(group);
            }

            if (changed || Groups.Count != uniqueGroups.Count)
                Groups = uniqueGroups;

            var path = AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrEmpty(path))
            {
                string normalizedPath = path.Replace('\\', '/');
                if (!normalizedPath.Contains("/Resources/") && !normalizedPath.StartsWith("Assets/Resources/"))
                {
                    Debug.LogWarning(
                        "[Sentinal] ViewGroupConfig must be inside a Resources folder to be loaded at runtime.",
                        this
                    );
                }
            }
        }
#endif
    }
}
