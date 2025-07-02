using UnityEngine;

namespace MenuNavigation.Samples
{
    public class CloseableMenuExample : MonoBehaviour, ICloseableMenu
    {
        public void Close()
        {
            // Implement your close logic here. Could be animation --> disabling the menu, etc.
            Debug.Log($"Closing menu: {name}");
            gameObject.SetActive(false);
        }
    }
}
