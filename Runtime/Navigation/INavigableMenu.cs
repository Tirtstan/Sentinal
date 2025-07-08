using UnityEngine;

namespace MenuNavigation
{
    public interface INavigableMenu
    {
        public GameObject FirstSelected { get; }
        public void Select();
    }
}
