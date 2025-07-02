using TMPro;
using UnityEngine;

namespace MenuNavigation.Samples
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class MenuLogText : MonoBehaviour
    {
        private TextMeshProUGUI menuLogText;

        private void Awake()
        {
            menuLogText = GetComponent<TextMeshProUGUI>();
        }

        private void Update()
        {
            menuLogText.SetText(MenuNavigatorManager.Instance.ToString());
        }
    }
}
