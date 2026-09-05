using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Serialization;
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
        [Tooltip("User-defined group names. Default always occupies bit 0.")]
        [FormerlySerializedAs("Groups")]
        [SerializeField]
        private List<string> groups = new();

        [NonSerialized]
        private ReadOnlyCollection<string> readOnlyGroups;

        public IReadOnlyList<string> Groups => readOnlyGroups ??= groups.AsReadOnly();

        /// <summary>
        /// Gets the total number of groups, including the hardcoded "Default" group.
        /// </summary>
        public int GroupCount => groups.Count + 1;

        /// <summary>
        /// Resolves the group name at a specific index, handling "Default" at index 0.
        /// </summary>
        public string GetGroupName(int index)
        {
            if (index == 0)
                return "Default";

            int userIndex = index - 1;
            if (userIndex < 0 || userIndex >= groups.Count)
                return null;

            return groups[userIndex];
        }

        /// <summary>
        /// Adds a custom group at the next available bit.
        /// </summary>
        public bool TryAddGroup(string groupName, out int groupIndex)
        {
            groupIndex = -1;
            if (!TryNormalizeGroupName(groupName, out string normalizedName))
                return false;

            if (groups.Count >= 31 || ContainsGroup(normalizedName))
                return false;

            groups.Add(normalizedName);
            groupIndex = groups.Count;
            return true;
        }

        /// <summary>
        /// Renames a custom group without changing its assigned bit.
        /// </summary>
        public bool RenameGroup(int groupIndex, string groupName)
        {
            int userIndex = groupIndex - 1;
            if (userIndex < 0 || userIndex >= groups.Count)
                return false;

            if (!TryNormalizeGroupName(groupName, out string normalizedName))
                return false;

            for (int i = 0; i < groups.Count; i++)
            {
                if (i != userIndex && string.Equals(groups[i], normalizedName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            groups[userIndex] = normalizedName;
            return true;
        }

#if UNITY_EDITOR
        public bool RemoveLastGroupInEditor()
        {
            if (groups.Count == 0)
                return false;

            groups.RemoveAt(groups.Count - 1);
            return true;
        }
#endif

        private bool ContainsGroup(string groupName)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                if (string.Equals(groups[i], groupName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool TryNormalizeGroupName(string groupName, out string normalizedName)
        {
            normalizedName = groupName?.Trim();
            return !string.IsNullOrEmpty(normalizedName)
                && !string.Equals(normalizedName, "Default", StringComparison.OrdinalIgnoreCase);
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

            for (int i = 0; i < groups.Count; i++)
            {
                string group = groups[i]?.Trim();
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

                bool duplicate = false;
                for (int groupIndex = 0; groupIndex < uniqueGroups.Count; groupIndex++)
                {
                    if (string.Equals(uniqueGroups[groupIndex], group, System.StringComparison.OrdinalIgnoreCase))
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (duplicate)
                {
                    changed = true;
                    Debug.LogWarning($"[Sentinal] Removed duplicate group name '{group}' from user groups list.", this);
                    continue;
                }

                if (uniqueGroups.Count < 31)
                    uniqueGroups.Add(group);
                else
                    changed = true;
            }

            if (changed || groups.Count != uniqueGroups.Count)
            {
                groups = uniqueGroups;
                readOnlyGroups = null;
            }

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
