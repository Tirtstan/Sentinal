using UnityEngine;

namespace Sentinal.Samples
{
    public class CloseableMenuExample : MonoBehaviour, ICloseableView
    {
        public void Close()
        {
            // Implement your close logic here. Could be animation --> disabling the menu, etc.
            Debug.Log($"Closing menu: {name}");
            gameObject.SetActive(false);
        }
    }
}
