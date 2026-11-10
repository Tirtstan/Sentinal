using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sentinal
{
    /// <summary>A map identity shared by authored assets and their per-player copies. Names are display only.</summary>
    [Serializable]
    public struct ActionMapRef
    {
        [SerializeField]
        private InputActionAsset asset;

        [SerializeField]
        private string id;

        public ActionMapRef(InputActionMap map)
        {
            asset = map.asset;
            id = map.id.ToString();
        }

        public InputActionAsset Asset => asset;
        public string Id => id;
        public bool IsSet => Guid.TryParse(id, out _);

        public InputActionMap Resolve(InputActionAsset playerAsset) =>
            playerAsset != null && Guid.TryParse(id, out Guid guid) ? playerAsset.FindActionMap(guid) : null;
    }

    [Serializable]
    public sealed class SceneBaseMap
    {
        [SerializeField, Tooltip("Full build-scene asset path, including Assets/ and .unity.")]
        private string scenePath;

        [SerializeField]
        private ActionMapRef map;

        public string ScenePath => scenePath;
        public ActionMapRef Map => map;
    }
}
