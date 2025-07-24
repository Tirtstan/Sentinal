using UnityEngine;

namespace Sentinal
{
    public interface ISentinalSelector
    {
        public GameObject FirstSelected { get; }
        public void Select();
    }
}
