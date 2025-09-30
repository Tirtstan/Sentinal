using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class UnderlineOnToggle : MonoBehaviour
{
    [Header("Text")]
    [SerializeField]
    private TextMeshProUGUI text;
    private Toggle targetToggle;

    private void Awake()
    {
        targetToggle = GetComponent<Toggle>();
        targetToggle.onValueChanged.AddListener(OnToggleValueChanged);
    }

    private void OnToggleValueChanged(bool isOn)
    {
        if (isOn)
            text.fontStyle |= FontStyles.Underline;
        else
            text.fontStyle &= ~FontStyles.Underline;
    }
}
