using UnityEngine;

namespace MenuNavigation
{
    public interface INavigableMenu
    {
        public bool IsOpen { get; }
        public GameObject FirstSelected { get; }
        public void Select();
    }
}
