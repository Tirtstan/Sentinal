using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
#if SENTINAL_ADDRESSABLES
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

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
#if SENTINAL_ADDRESSABLES
        private static readonly Dictionary<ViewAddress, Task<ViewSelector>> pendingAddressableResolutions = new();
#endif

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
        /// Unregisters the key only when it still belongs to the given view.
        /// </summary>
        public static void Unregister(ViewAddress key, ViewSelector view)
        {
            if (key != null && registry.TryGetValue(key, out ViewSelector registeredView) && registeredView == view)
            {
                registry.Remove(key);
            }
        }

        /// <summary>
        /// Gets an existing scene view for the address, or instantiates its configured direct prefab.
        /// Addressable prefabs must be resolved with <see cref="ResolveAsync"/>.
        /// </summary>
        public static ViewSelector Resolve(ViewAddress key)
        {
            if (key == null)
            {
                Debug.LogWarning("[Sentinal] Cannot resolve a null ViewAddress.");
                return null;
            }

            ViewSelector view = FindExistingView(key);
            if (view != null)
                return view;

            if (key.PrefabSource == ViewAddressPrefabSource.DirectPrefab)
            {
                if (key.FallbackPrefab == null)
                {
                    Debug.LogError(
                        $"[Sentinal] ViewAddress '{key.name}' is set to {nameof(ViewAddressPrefabSource.DirectPrefab)}, but no direct prefab is assigned.",
                        key
                    );
                    return null;
                }

                ViewSelector instance = UnityEngine.Object.Instantiate(key.FallbackPrefab);
                return ValidateDirectInstance(key, instance);
            }

            LogUnresolvedAddress(key, key.PrefabSource == ViewAddressPrefabSource.AddressablePrefab);
            return null;
        }

        /// <summary>
        /// Gets an existing scene view for the address, or asynchronously instantiates its configured prefab source.
        /// Concurrent Addressable requests for the same address share one instantiation operation.
        /// </summary>
        public static Task<ViewSelector> ResolveAsync(ViewAddress key)
        {
            if (key == null)
            {
                Debug.LogWarning("[Sentinal] Cannot resolve a null ViewAddress.");
                return Task.FromResult<ViewSelector>(null);
            }

            ViewSelector view = FindExistingView(key);
            if (view != null)
                return Task.FromResult(view);

            if (key.PrefabSource == ViewAddressPrefabSource.DirectPrefab)
                return Task.FromResult(Resolve(key));

            if (key.PrefabSource != ViewAddressPrefabSource.AddressablePrefab)
            {
                LogUnresolvedAddress(key, isAddressableSource: false);
                return Task.FromResult<ViewSelector>(null);
            }

#if SENTINAL_ADDRESSABLES
            if (key.AddressablePrefab == null || !key.AddressablePrefab.RuntimeKeyIsValid())
            {
                Debug.LogError(
                    $"[Sentinal] ViewAddress '{key.name}' is set to {nameof(ViewAddressPrefabSource.AddressablePrefab)}, but no valid {nameof(key.AddressablePrefab)} is assigned.",
                    key
                );
                return Task.FromResult<ViewSelector>(null);
            }

            if (pendingAddressableResolutions.TryGetValue(key, out Task<ViewSelector> pendingResolution))
                return pendingResolution;

            var completion = new TaskCompletionSource<ViewSelector>();
            pendingAddressableResolutions[key] = completion.Task;
            InstantiateAddressableAsync(key, completion);
            return completion.Task;
#else
            Debug.LogWarning(
                $"[Sentinal] ViewAddress '{key.name}' is set to {nameof(ViewAddressPrefabSource.AddressablePrefab)}, but Addressables support is unavailable. Install com.unity.addressables to enable it.",
                key
            );
            return Task.FromResult<ViewSelector>(null);
#endif
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

        private static ViewSelector FindExistingView(ViewAddress key)
        {
            if (registry.TryGetValue(key, out ViewSelector view) && view != null)
                return view;

            registry.Remove(key);

            // Registration happens on enable, so inactive scene views may not be in the registry yet.
            // Try to locate a loaded scene instance by address before instantiating a configured prefab.
            view = FindLoadedSceneViewByAddress(key);
            if (view != null)
                registry[key] = view;

            return view;
        }

        private static ViewSelector ValidateDirectInstance(ViewAddress key, ViewSelector instance)
        {
            if (instance != null && instance.Address == key)
                return instance;

            string actualAddress = instance != null && instance.Address != null ? instance.Address.name : "none";
            Debug.LogError(
                $"[Sentinal] Direct fallback prefab for address '{key.name}' must contain a {nameof(ViewSelector)} assigned to that same address. Actual address: {actualAddress}.",
                key
            );

            if (instance != null)
                UnityEngine.Object.Destroy(instance.gameObject);

            return null;
        }

        private static void LogUnresolvedAddress(ViewAddress key, bool isAddressableSource)
        {
            string guidance = isAddressableSource
                ? $"Use {nameof(ResolveAsync)} or {nameof(SentinalViewRouter)}.{nameof(SentinalViewRouter.OpenViewAsync)} to instantiate its Addressable prefab."
                : "Prefab Source is None.";

            Debug.LogWarning($"[Sentinal] No view found for address '{key.name}'. {guidance}", key);
        }

#if SENTINAL_ADDRESSABLES
        private static void InstantiateAddressableAsync(ViewAddress key, TaskCompletionSource<ViewSelector> completion)
        {
            try
            {
                AsyncOperationHandle<GameObject> handle = key.AddressablePrefab.InstantiateAsync();
                handle.Completed += operation => CompleteAddressableInstantiation(key, operation, completion);
            }
            catch (Exception exception)
            {
                pendingAddressableResolutions.Remove(key);
                Debug.LogError(
                    $"[Sentinal] Failed to start Addressable fallback instantiation for address '{key.name}'. {exception.Message}",
                    key
                );
                completion.SetResult(null);
            }
        }

        private static void CompleteAddressableInstantiation(
            ViewAddress key,
            AsyncOperationHandle<GameObject> operation,
            TaskCompletionSource<ViewSelector> completion
        )
        {
            pendingAddressableResolutions.Remove(key);

            if (operation.Status != AsyncOperationStatus.Succeeded || operation.Result == null)
            {
                Debug.LogError(
                    $"[Sentinal] Failed to instantiate Addressable fallback for address '{key.name}'. Status: {operation.Status}.",
                    key
                );
                UnityEngine.AddressableAssets.Addressables.Release(operation);
                completion.SetResult(null);
                return;
            }

            if (!operation.Result.TryGetComponent(out ViewSelector instance) || instance.Address != key)
            {
                string actualAddress = instance != null && instance.Address != null ? instance.Address.name : "none";
                Debug.LogError(
                    $"[Sentinal] Addressable fallback for address '{key.name}' must contain a {nameof(ViewSelector)} assigned to that same address. Actual address: {actualAddress}.",
                    key
                );
                UnityEngine.AddressableAssets.Addressables.ReleaseInstance(operation.Result);
                completion.SetResult(null);
                return;
            }

            completion.SetResult(instance);
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            registry.Clear();
#if SENTINAL_ADDRESSABLES
            pendingAddressableResolutions.Clear();
#endif
        }

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
