using UnityEngine;

namespace Sentinal
{
    public interface IViewSelector
    {
        public GameObject FirstSelected { get; }
        public void Select();
    }
}
