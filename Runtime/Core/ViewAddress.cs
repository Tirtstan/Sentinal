using UnityEngine;
using UnityEngine.Serialization;
#if SENTINAL_ADDRESSABLES
using UnityEngine.AddressableAssets;
#endif

namespace Sentinal
{
    /// <summary>
    /// Selects how a <see cref="ViewAddress"/> creates a view when no loaded scene instance is available.
    /// </summary>
    public enum ViewAddressPrefabSource
    {
        None,
        DirectPrefab,
        AddressablePrefab,
    }

    /// <summary>
    /// ScriptableObject key for cross-prefab view discovery.
    /// Assign to a <see cref="ViewSelector"/> to register it with <see cref="ViewAddressRegistry"/>.
    /// If the view isn't in the scene, the configured prefab source can create it.
    /// </summary>
    [CreateAssetMenu(fileName = "NewViewAddress", menuName = "Sentinal/View Address")]
    public class ViewAddress : ScriptableObject
    {
        [Header("Fallback")]
        [SerializeField]
        [Tooltip("How to create this view when no loaded scene instance is registered for this address.")]
        private ViewAddressPrefabSource prefabSource;

        [SerializeField]
        [FormerlySerializedAs("FallbackPrefab")]
        [Tooltip("Prefab to instantiate when Prefab Source is Direct Prefab.")]
        private ViewSelector fallbackPrefab;

#if SENTINAL_ADDRESSABLES
        [SerializeField]
        [Tooltip("Addressable prefab to instantiate when Prefab Source is Addressable Prefab.")]
        private AssetReferenceGameObject addressablePrefab;
#endif

        public ViewAddressPrefabSource PrefabSource
        {
            get => prefabSource;
            set => prefabSource = value;
        }

        public ViewSelector FallbackPrefab
        {
            get => fallbackPrefab;
            set => fallbackPrefab = value;
        }

#if SENTINAL_ADDRESSABLES
        public AssetReferenceGameObject AddressablePrefab
        {
            get => addressablePrefab;
            set => addressablePrefab = value;
        }
#endif
    }
}
