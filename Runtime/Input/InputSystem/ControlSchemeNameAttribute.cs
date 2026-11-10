#if ENABLE_INPUT_SYSTEM
using UnityEngine;

namespace Sentinal.InputSystem
{
    /// <summary>Selects a scheme from the referenced InputActionAsset field on the same object.</summary>
    public sealed class ControlSchemeNameAttribute : PropertyAttribute
    {
        public string AssetField { get; }

        public ControlSchemeNameAttribute(string assetField) => AssetField = assetField;
    }
}
#endif
