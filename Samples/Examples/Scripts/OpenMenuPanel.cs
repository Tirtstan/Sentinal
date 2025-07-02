using UnityEngine;
using UnityEngine.UI;

namespace MenuNavigation.Samples
{
    public class OpenMenuPanel : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField]
        private GameObject menu1;

        [SerializeField]
        private GameObject menu2;

        [SerializeField]
        private Button toggleMenu1Button;

        [SerializeField]
        private Button toggleMenu2Button;

        private void Awake()
        {
            toggleMenu1Button.onClick.AddListener(ToggleMenu1);
            toggleMenu2Button.onClick.AddListener(ToggleMenu2);
        }

        private void Update() // legacy input to avoid action maps switching edge cases
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
                ToggleMenu1();
            else if (Input.GetKeyDown(KeyCode.Alpha2))
                ToggleMenu2();
        }

        private void ToggleMenu1() => menu1.SetActive(!menu1.activeInHierarchy);

        private void ToggleMenu2() => menu2.SetActive(!menu2.activeInHierarchy);

        private void OnDestroy()
        {
            toggleMenu1Button.onClick.RemoveAllListeners();
            toggleMenu2Button.onClick.RemoveAllListeners();
        }
    }
}
