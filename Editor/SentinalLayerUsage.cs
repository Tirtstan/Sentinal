using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Sentinal.Editor
{
    /// <summary>On-demand asset inspection. Never opens or saves the user's scenes.</summary>
    internal static class SentinalLayerUsage
    {
        internal enum UsageKind
        {
            View,
            SceneAssignment,
            ScenePrefabReference,
        }

        internal readonly struct Usage
        {
            public readonly string Description;
            public readonly UnityEngine.Object Target;
            public readonly UsageKind Kind;

            public Usage(string description, UnityEngine.Object target, UsageKind kind = UsageKind.View)
            {
                Description = description;
                Target = target;
                Kind = kind;
            }
        }

        /// <summary>Distinct places to edit a layer assignment, following inherited prefab values to their source.</summary>
        internal static List<Usage> FindAssignments(int layerId)
        {
            List<Usage> references = Scan(layerId);
            var result = new List<Usage>();
            var seen = new HashSet<UnityEngine.Object>();
            var savedScenes = new HashSet<string>(
                references
                    .Where(usage => usage.Kind == UsageKind.SceneAssignment)
                    .Select(usage => AssetDatabase.GetAssetPath(usage.Target))
            );

            foreach (Usage usage in references)
            {
                if (usage.Kind == UsageKind.ScenePrefabReference)
                    continue;
                if (usage.Kind == UsageKind.SceneAssignment)
                {
                    if (seen.Add(usage.Target))
                        result.Add(new Usage(AssetDatabase.GetAssetPath(usage.Target), usage.Target, usage.Kind));
                    continue;
                }

                var view = (SentinalView)usage.Target;
                if (!view.IsTopLevel)
                    continue;

                SentinalView owner = AssignmentOwner(view);
                string path = AssetDatabase.GetAssetPath(owner);
                if (string.IsNullOrEmpty(path))
                {
                    if (!owner.gameObject.scene.isDirty && savedScenes.Contains(owner.gameObject.scene.path))
                        continue;
                    path =
                        $"{owner.gameObject.scene.name} / {AnimationUtility.CalculateTransformPath(owner.transform, null)}";
                }

                if (seen.Add(owner))
                    result.Add(new Usage(path, owner));
            }

            return result.OrderBy(usage => usage.Target.name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static SentinalView AssignmentOwner(SentinalView view)
        {
            while (true)
            {
                SentinalView source = PrefabUtility.GetCorrespondingObjectFromSource(view);
                if (source == null)
                    return view;
                using var data = new SerializedObject(view);
                if (data.FindProperty("layer").FindPropertyRelative("id").prefabOverride)
                    return view;
                view = source;
            }
        }

        /// <summary>All references used to protect layer deletion, including nested and inherited uses.</summary>
        internal static List<Usage> Scan(int layerId)
        {
            var result = new List<Usage>();
            var prefabUsers = new Dictionary<string, string>();
            string viewGuid = AssetDatabase.AssetPathToGUID("Packages/com.tirt.sentinal/Runtime/Core/SentinalView.cs");
            if (string.IsNullOrEmpty(viewGuid))
                throw new InvalidOperationException("Expected SentinalView.cs script GUID; actual: none.");
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    throw new InvalidOperationException($"Expected readable prefab '{path}'; actual: could not load.");
                foreach (SentinalView view in prefab.GetComponentsInChildren<SentinalView>(true))
                {
                    if (view.Window.Layer.Id != layerId)
                        continue;
                    result.Add(
                        new Usage(
                            $"{path} / {AnimationUtility.CalculateTransformPath(view.transform, prefab.transform)} ({view.name})",
                            view
                        )
                    );
                    prefabUsers[guid] = path;
                }
            }

            string layerPattern = @"(?m)^  layer:\s*\r?\n    id: " + layerId + @"\s*$";
            foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string yaml = File.ReadAllText(path);
                if (!yaml.StartsWith("%YAML", StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Expected text-serialized scene '{path}'; actual: binary or unsupported format."
                    );
                UnityEngine.Object scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                foreach (string block in Regex.Split(yaml, @"(?m)^--- "))
                {
                    if (
                        !block.StartsWith("!u!114 ", StringComparison.Ordinal)
                        || !block.Contains("guid: " + viewGuid)
                        || !Regex.IsMatch(block, layerPattern)
                    )
                        continue;
                    Match gameObject = Regex.Match(block, @"m_GameObject: \{fileID: (-?\d+)\}");
                    string name = "SentinalView";
                    if (gameObject.Success)
                    {
                        Match objectBlock = Regex.Match(
                            yaml,
                            @"(?ms)^--- !u!1 &"
                                + gameObject.Groups[1].Value
                                + @"\r?\n(?:(?!^--- ).)*?^  m_Name: ([^\r\n]+)"
                        );
                        if (objectBlock.Success)
                            name = objectBlock.Groups[1].Value;
                    }
                    result.Add(new Usage($"{path} / {name}", scene, UsageKind.SceneAssignment));
                }
                var seenPrefabs = new HashSet<string>();
                foreach (Match match in Regex.Matches(yaml, @"guid: ([a-f0-9]{32})"))
                {
                    string prefabGuid = match.Groups[1].Value;
                    if (seenPrefabs.Add(prefabGuid) && prefabUsers.TryGetValue(prefabGuid, out string prefabPath))
                        result.Add(
                            new Usage($"{path} / views from {prefabPath}", scene, UsageKind.ScenePrefabReference)
                        );
                }
            }

            foreach (SentinalView view in Resources.FindObjectsOfTypeAll<SentinalView>())
                if (
                    !EditorUtility.IsPersistent(view)
                    && view.gameObject.scene.IsValid()
                    && view.Window.Layer.Id == layerId
                )
                    result.Add(new Usage($"Loaded scene '{view.gameObject.scene.name}' / {view.name}", view));
            return result;
        }
    }
}
