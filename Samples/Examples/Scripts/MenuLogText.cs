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
            MenuNavigatorManager.Instance.OnMenuSwitched += OnMenuSwitched;
            UpdateText();
        }

        private void OnMenuSwitched(MenuNavigator navigator1, MenuNavigator navigator2) => UpdateText();

        private void UpdateText() => menuLogText.SetText(MenuNavigatorManager.Instance.ToString());

        private void OnDestroy()
        {
            MenuNavigatorManager.Instance.OnMenuSwitched -= OnMenuSwitched;
        }
    }
}
