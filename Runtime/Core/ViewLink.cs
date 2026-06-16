using UnityEngine;
using UnityEngine.UI;

namespace Sentinal
{
    /// <summary>
    /// Drop-in button component that opens a view by its <see cref="ViewAddress"/>.
    /// No direct scene reference needed — works across prefabs.
    /// </summary>
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("Sentinal/View Link")]
    public class ViewLink : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The ViewAddress to navigate to when clicked.")]
        private ViewAddress targetAddress;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(() =>
            {
                if (targetAddress != null)
                    ViewAddressRegistry.Resolve(targetAddress)?.Open();
            });
        }
    }
}
