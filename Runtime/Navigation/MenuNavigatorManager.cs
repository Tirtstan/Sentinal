using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MenuNavigation
{
    [DefaultExecutionOrder(-1)]
    public class MenuNavigatorManager : MonoBehaviour
    {
        public static MenuNavigatorManager Instance { get; private set; }
        public event Action<MenuNavigator> OnMenuOpened;
        public event Action<MenuNavigator> OnMenuClosed;

        /// <summary>
        /// Event triggered when a menu is switched. Provides the previous and current menu respectively.
        /// </summary>
        public event Action<MenuNavigator, MenuNavigator> OnMenuSwitched;

        private readonly LinkedList<MenuNavigator> menuHistory = new();
        private readonly StringBuilder menuInfoBuilder = new();

        public MenuNavigator CurrentMenu => menuHistory.Count > 0 ? menuHistory.Last.Value : null;
        public bool AnyMenusOpen => menuHistory.Count > 0;
        public int MenuCount => menuHistory.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        /// <summary>
        /// Closes the most recently opened menu.
        /// </summary>
        public void CloseCurrentMenu() => CloseMenu(CurrentMenu);

        /// <summary>
        /// Closes all open menus.
        /// </summary>
        public void CloseAllMenus()
        {
            List<MenuNavigator> menusToClose = new(menuHistory);
            foreach (var menu in menusToClose)
                CloseMenu(menu);
        }

        private void CloseMenu(MenuNavigator menu)
        {
            if (menu == null)
                return;

            if (menu.TryGetComponent(out ICloseableMenu closeableMenu))
            {
                closeableMenu.Close();
            }
            else
            {
                menu.gameObject.SetActive(false);
            }
        }

        public void RegisterMenuOpen(MenuNavigator menu)
        {
            MenuNavigator previousMenu = CurrentMenu;

            if (menu == null || menuHistory.Contains(menu))
                return;

            menuHistory.AddLast(menu);
            OnMenuOpened?.Invoke(menu);

            OnMenuSwitched?.Invoke(previousMenu, menu);
        }

        public void RegisterMenuClose(MenuNavigator menu)
        {
            if (menu == null)
                return;

            bool wasCurrentMenu = menu == CurrentMenu;

            menuHistory.Remove(menu);
            OnMenuClosed?.Invoke(menu);

            // only auto-select if we closed the current menu AND there's still a menu open
            if (wasCurrentMenu && CurrentMenu != null)
                CurrentMenu.Select();

            OnMenuSwitched?.Invoke(menu, CurrentMenu);
        }

        public override string ToString()
        {
            if (menuHistory.Count == 0)
                return "No open menus.";

            menuInfoBuilder.Clear();
            menuInfoBuilder.AppendLine("Menu Stack (oldest to newest):");

            int index = 0;
            foreach (MenuNavigator menu in menuHistory)
            {
                string menuName = menu != null ? menu.name : "NULL";
                menuInfoBuilder.AppendLine($"[{index}] {menuName}");
                index++;
            }

            return menuInfoBuilder.ToString().TrimEnd();
        }
    }
}
