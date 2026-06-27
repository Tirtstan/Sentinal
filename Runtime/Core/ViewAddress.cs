using UnityEngine;

namespace Sentinal
{
    /// <summary>
    /// ScriptableObject key for cross-prefab view discovery.
    /// Assign to a <see cref="ViewSelector"/> to register it with <see cref="ViewAddressRegistry"/>.
    /// If the view isn't in the scene, the optional fallback prefab will be instantiated.
    /// </summary>
    [CreateAssetMenu(fileName = "NewViewAddress", menuName = "Sentinal/View Address")]
    public class ViewAddress : ScriptableObject
    {
        [Tooltip("Optional prefab to instantiate if no scene instance is registered for this address.")]
        public ViewSelector FallbackPrefab;
    }
}
