using System;
using UnityEngine;

namespace Sentinal
{
    /// <summary>What happens to a layer's open views when another view opens on the same layer.</summary>
    public enum LayerOthers
    {
        /// <summary>Other views on this layer stay visible and only lose focus.</summary>
        StayVisible,

        /// <summary>Other views on this layer are switched off and come back when this view closes.</summary>
        HideUntilThisCloses,

        /// <summary>Other views on this layer close.</summary>
        Close,

        /// <summary>While a view is open on this layer, opening another one fails and the current view stays.</summary>
        RefuseTheNewOne,
    }

    /// <summary>What Back does when it reaches a top-level view on this layer.</summary>
    public enum LayerBack
    {
        /// <summary>Back closes the view.</summary>
        ClosesTheView,

        /// <summary>Back does nothing. Only code closes views on this layer (main menu root, HUD-like screens).</summary>
        Ignored,
    }

    public enum LayerInput
    {
        NoChange,
        EveryoneToUI,
        OwnerToUI,
    }

    /// <summary>
    /// Shared rules for every top-level view on a layer. Layers are authored in Project Settings › Sentinal
    /// and ordered top to bottom: a view on a higher layer always wins focus over a lower one.
    /// </summary>
    [Serializable]
    public sealed class ViewLayer
    {
        [SerializeField]
        [HideInInspector]
        private int id;

        [SerializeField]
        private string name;

        [SerializeField]
        [Tooltip("What happens to this layer's open views when a new one opens on it.")]
        private LayerOthers others;

        [SerializeField]
        [Tooltip("What Back does on a top-level view on this layer.")]
        private LayerBack back;

        [SerializeField]
        [Tooltip("Whose map moves to UI while a view is opening or open. Global UI suspends player menus.")]
        private LayerInput inputWhileOpen;

        public ViewLayer(int id, string name, LayerOthers others, LayerBack back)
        {
            this.id = id;
            this.name = name;
            this.others = others;
            this.back = back;
        }

        /// <summary>Stable identity stored by views; never reused and unaffected by renaming or reordering.</summary>
        public int Id => id;

        public string Name => name;

        public LayerOthers Others => others;

        public LayerBack Back => back;

        public LayerInput InputWhileOpen => inputWhileOpen;
    }

    /// <summary>A view's reference to a <see cref="ViewLayer"/> by its stable id. Zero means no layer.</summary>
    [Serializable]
    public struct ViewLayerRef : IEquatable<ViewLayerRef>
    {
        [SerializeField]
        private int id;

        public ViewLayerRef(int id)
        {
            this.id = id;
        }

        public readonly int Id => id;

        public readonly bool IsSet => id != 0;

        public readonly bool Equals(ViewLayerRef other) => id == other.id;

        public override readonly bool Equals(object obj) => obj is ViewLayerRef other && Equals(other);

        public override readonly int GetHashCode() => id;

        public static bool operator ==(ViewLayerRef left, ViewLayerRef right) => left.id == right.id;

        public static bool operator !=(ViewLayerRef left, ViewLayerRef right) => left.id != right.id;
    }
}
