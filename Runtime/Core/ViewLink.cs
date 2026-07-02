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

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            button.onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            if (targetAddress != null)
            {
                var view = ViewAddressRegistry.Resolve(targetAddress);
                if (view != null)
                    view.Open();
            }
        }

        private void OnDisable()
        {
            button.onClick.RemoveListener(OnClick);
        }
    }
}
