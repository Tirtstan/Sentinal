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
        [Tooltip("List of group names. Each group can be assigned to ViewSelectors using a group mask.")]
        public List<string> Groups = new() { "Default" };

        public string GetGroupName(int index)
        {
            if (index < 0 || index >= Groups.Count)
                return null;

            return Groups[index];
        }

        /// <summary>
        /// Loads the shared ViewGroupConfig from Resources.
        /// Searches all resources if not found at the default path.
        /// </summary>
        public static ViewGroupConfig LoadShared()
        {
            var config = Resources.Load<ViewGroupConfig>(DefaultResourcePath);
            if (config != null) return config;

            var fallback = Resources.LoadAll<ViewGroupConfig>("");
            if (fallback.Length > 0)
            {
                Debug.LogWarning(
                    $"[Sentinal] ViewGroupConfig not found at '{DefaultResourcePath}'. " +
                    $"Using fallback at '{fallback[0].name}'. Move it to Resources/{DefaultResourcePath}.asset.",
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
            string directory = Path.Combine("Assets", "Resources");
            string assetPath = Path.Combine(directory, "SentinalViewGroups.asset");

            var existing = AssetDatabase.LoadAssetAtPath<ViewGroupConfig>(assetPath);
            if (existing != null)
                return existing;

            var instance = CreateInstance<ViewGroupConfig>();

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            AssetDatabase.CreateAsset(instance, assetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Sentinal] Created SentinalViewGroups asset in {directory}.", instance);

            return instance;
        }

        [InitializeOnLoadMethod]
        private static void EnsureSharedAssetOnLoad()
        {
            EnsureSharedInProject();
        }

        private void OnValidate()
        {
            for (int i = Groups.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrWhiteSpace(Groups[i]))
                {
                    Groups[i] = $"Group {i}";
                    Debug.LogWarning($"[Sentinal] Empty group name at index {i}, replaced with '{Groups[i]}'.", this);
                }
            }

            var path = AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrEmpty(path) && !path.Contains("/Resources/"))
            {
                Debug.LogWarning("[Sentinal] ViewGroupConfig must be inside a Resources folder to be loaded at runtime.", this);
            }
        }
#endif
    }
}
