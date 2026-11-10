using System.Collections.Generic;
using UnityEngine;

namespace Sentinal
{
    /// <summary>
    /// Project-wide Sentinal settings, edited in Project Settings › Sentinal. The asset is registered in
    /// Preloaded Assets so builds load it at startup; the editor registers it on load.
    /// </summary>
    public sealed class SentinalSettings : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/Settings/Sentinal Settings.asset";

        [Header("Layers")]
        [SerializeField]
        [Tooltip("Global layers, top to bottom as they stack on screen. A view on a higher layer always wins focus.")]
        private List<ViewLayer> globalLayers = new();

        [SerializeField]
        [Tooltip("Each player has an independent copy of these layers. Global and Player orders never compare.")]
        private List<ViewLayer> playerLayers = new();

        [Header("Input")]
        [SerializeField]
        [Tooltip("Maps kept enabled beside each player's current map, resolved by GUID in that player's asset.")]
        private List<ActionMapRef> alwaysOnActionMaps = new();

        [SerializeField]
        private ActionMapRef uiActionMap;

        [SerializeField]
        private List<SceneBaseMap> sceneBaseMaps = new();

        [SerializeField]
        [HideInInspector]
        private int nextLayerId = 1;

        private static SentinalSettings instance;

        /// <summary>The loaded settings, or null with an error naming the expected asset when none is registered.</summary>
        public static SentinalSettings Instance
        {
            get
            {
                if (instance == null && !reportedMissing)
                {
                    reportedMissing = true;
                    Debug.LogError(
                        $"[Sentinal] Expected a {nameof(SentinalSettings)} asset registered in Preloaded Assets (default path '{DefaultAssetPath}'); actual: none loaded. Create it in Project Settings › Sentinal."
                    );
                }

                return instance;
            }
        }

        private static bool reportedMissing;

        public IReadOnlyList<ViewLayer> GlobalLayers => globalLayers;

        public IReadOnlyList<ViewLayer> PlayerLayers => playerLayers;

        public IReadOnlyList<ActionMapRef> AlwaysOnActionMaps => alwaysOnActionMaps;

        public ActionMapRef UIActionMap => uiActionMap;

        public bool IsPlayerLayer(ViewLayerRef reference) => playerLayers.Exists(layer => layer.Id == reference.Id);

        public bool TryGetBaseMap(string scenePath, out ActionMapRef map)
        {
            foreach (SceneBaseMap policy in sceneBaseMaps)
                if (policy.ScenePath == scenePath)
                {
                    map = policy.Map;
                    return true;
                }

            map = default;
            return false;
        }

        /// <summary>Makes this asset the active settings. Called by the asset itself and by the editor bootstrap.</summary>
        public static void Register(SentinalSettings settings)
        {
            instance = settings;
            reportedMissing = false;
        }

        /// <summary>Finds a global layer and its stacking order (higher is on top).</summary>
        public bool TryGetLayer(ViewLayerRef layerRef, out ViewLayer layer, out int order)
        {
            for (int i = 0; i < globalLayers.Count; i++)
            {
                if (globalLayers[i].Id != layerRef.Id)
                    continue;

                layer = globalLayers[i];
                order = globalLayers.Count - 1 - i;
                return true;
            }

            for (int i = 0; i < playerLayers.Count; i++)
            {
                if (playerLayers[i].Id != layerRef.Id)
                    continue;

                layer = playerLayers[i];
                order = playerLayers.Count - 1 - i;
                return true;
            }

            layer = null;
            order = -1;
            return false;
        }

        private void OnEnable()
        {
            if (instance == null)
                Register(this);
        }
    }
}
