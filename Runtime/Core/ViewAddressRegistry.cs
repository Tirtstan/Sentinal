using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Sentinal
{
    /// <summary>
    /// Static registry for views identified by <see cref="ViewAddress"/> ScriptableObject keys.
    /// Views self-register on enable and unregister on disable.
    /// Use <see cref="Resolve"/> to find or instantiate a view by its address.
    /// </summary>
    public static class ViewAddressRegistry
    {
        private static readonly Dictionary<ViewAddress, ViewSelector> registry = new();

        /// <summary>
        /// Registers a view for the given address key. First registration wins.
        /// </summary>
        public static void Register(ViewAddress key, ViewSelector view)
        {
            if (key == null || view == null)
                return;

            if (registry.TryGetValue(key, out var existing) && existing != null && existing != view)
                return;

            registry[key] = view;
        }

        /// <summary>
        /// Unregisters a view for the given address key.
        /// </summary>
        public static void Unregister(ViewAddress key)
        {
            if (key != null)
                registry.Remove(key);
        }

        /// <summary>
        /// Gets an existing scene view for the address, or instantiates the fallback prefab.
        /// Returns null if no view exists and no fallback is configured.
        /// </summary>
        public static ViewSelector Resolve(ViewAddress key)
        {
            if (key == null)
            {
                Debug.LogWarning("[Sentinal] Cannot resolve a null ViewAddress.");
                return null;
            }

            if (registry.TryGetValue(key, out var view) && view != null)
                return view;

            // Instance was destroyed, clean up stale entry
            registry.Remove(key);

            // Registration happens on enable, so inactive scene views may not be in the registry yet.
            // Try to locate a loaded scene instance by address before instantiating a fallback prefab.
            view = FindLoadedSceneViewByAddress(key);
            if (view != null)
            {
                registry[key] = view;
                return view;
            }

            if (key.FallbackPrefab != null)
            {
                ViewSelector instance = Object.Instantiate(key.FallbackPrefab);
                return instance; // will self-register via its OnEnable
            }

            Debug.LogWarning($"[Sentinal] No view found for address '{key.name}' and no fallback prefab set.", key);
            return null;
        }

        /// <summary>
        /// Checks if a view is registered for the given address.
        /// </summary>
        public static bool IsRegistered(ViewAddress key) =>
            key != null && registry.TryGetValue(key, out var view) && view != null;

        /// <summary>
        /// Registers all currently loaded scene views with non-null addresses, including inactive views.
        /// Useful for address lookups before views have been enabled.
        /// </summary>
        public static void PrewarmLoadedSceneViews()
        {
            var loadedViews = Resources.FindObjectsOfTypeAll<ViewSelector>();
            for (int i = 0; i < loadedViews.Length; i++)
            {
                ViewSelector view = loadedViews[i];
                if (view == null || view.Address == null)
                    continue;

                GameObject viewGameObject = view.gameObject;
                if (!viewGameObject.scene.IsValid() || !viewGameObject.scene.isLoaded)
                    continue;

                Register(view.Address, view);
            }
        }

        private static ViewSelector FindLoadedSceneViewByAddress(ViewAddress key)
        {
            var loadedViews = Resources.FindObjectsOfTypeAll<ViewSelector>();
            for (int i = 0; i < loadedViews.Length; i++)
            {
                ViewSelector candidate = loadedViews[i];
                if (candidate == null || candidate.Address != key)
                    continue;

                GameObject candidateGameObject = candidate.gameObject;
                if (!candidateGameObject.scene.IsValid() || !candidateGameObject.scene.isLoaded)
                    continue;

                return candidate;
            }

            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => registry.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => PrewarmLoadedSceneViews();

#if UNITY_EDITOR
        private static readonly StringBuilder builder = new();

        /// <summary>
        /// Editor-only helper for inspecting current address registrations.
        /// </summary>
        public static string DumpRegistry()
        {
            builder.Clear();
            builder.AppendLine($"[Sentinal] ViewAddressRegistry entries: {registry.Count}");

            foreach (var pair in registry)
            {
                ViewAddress key = pair.Key;
                ViewSelector view = pair.Value;
                string keyName = key != null ? key.name : "null";
                string viewName = view != null ? view.name : "null";
                string sceneName = view != null ? view.gameObject.scene.name : "n/a";
                bool isActive = view != null && view.gameObject.activeInHierarchy;

                builder.AppendLine($"- {keyName} -> {viewName} (scene: {sceneName}, active: {isActive})");
            }

            string dump = builder.ToString();
            Debug.Log(dump);
            return dump;
        }
#endif
    }
}
