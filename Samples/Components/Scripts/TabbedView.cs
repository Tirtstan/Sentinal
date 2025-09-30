using Sentinal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TabbedView : MonoBehaviour
{
    [Header("Components")]
    [SerializeField]
    private Toggle[] groupToggles;

    [SerializeField]
    private ViewSelector[] groupPanels;

    [SerializeField]
    [Tooltip("Input action for switching tabs. Make sure the action is a Value type with Axis Control.")]
    private InputActionReference switchTabAction;

    [Header("Configs")]
    [SerializeField]
    [Tooltip("Whether tab switching wraps around from last to first tab and vice versa")]
    private bool wrapTabs = true;
    private PlayerInput playerInput;
    private int currentTabIndex = 0;

    private void Awake()
    {
        playerInput = PlayerInput.GetPlayerByIndex(0);

        for (int i = 0; i < groupToggles.Length; i++)
        {
            int index = i;
            groupToggles[i].onValueChanged.AddListener(isOn => OnTabToggle(index, isOn));
        }

        groupToggles[0].isOn = true;
        SetActiveTab(0);
    }

    private void OnEnable()
    {
        playerInput.actions.FindAction(switchTabAction.action.id).performed += OnSwitchTab;
    }

    private void OnTabToggle(int tabIndex, bool isOn)
    {
        if (!isOn)
            return;

        SetActiveTab(tabIndex);
    }

    private void OnSwitchTab(InputAction.CallbackContext context)
    {
        float input = context.ReadValue<float>();

        if (input > 0)
        {
            if (wrapTabs)
                currentTabIndex = (currentTabIndex + 1) % groupToggles.Length;
            else
                currentTabIndex = Mathf.Min(currentTabIndex + 1, groupToggles.Length - 1);
        }
        else if (input < 0)
        {
            if (wrapTabs)
                currentTabIndex = (currentTabIndex - 1 + groupToggles.Length) % groupToggles.Length;
            else
                currentTabIndex = Mathf.Max(currentTabIndex - 1, 0);
        }

        groupToggles[currentTabIndex].isOn = true;
        SetActiveTab(currentTabIndex);
    }

    private void SetActiveTab(int index)
    {
        currentTabIndex = index;
        for (int i = 0; i < groupPanels.Length; i++)
        {
            if (groupPanels[i] != null)
                groupPanels[i].gameObject.SetActive(false);
        }

        groupPanels[index].gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (playerInput != null)
            playerInput.actions.FindAction(switchTabAction.action.id).performed -= OnSwitchTab;
    }
}
