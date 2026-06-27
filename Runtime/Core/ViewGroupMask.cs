using System;
using UnityEngine;

namespace Sentinal
{
    /// <summary>
    /// Represents a bitmask for view groups.
    /// </summary>
    [Serializable]
    public struct ViewGroupMask : IEquatable<ViewGroupMask>
    {
        [SerializeField]
        private int value;

        /// <summary>
        /// The raw integer value of the mask.
        /// </summary>
        public int Value
        {
            readonly get => value;
            set => this.value = value;
        }

        public ViewGroupMask(int value)
        {
            this.value = value;
        }

        public static implicit operator int(ViewGroupMask mask) => mask.value;

        public static implicit operator ViewGroupMask(int value) => new(value);

        public static bool operator ==(ViewGroupMask left, ViewGroupMask right) => left.value == right.value;

        public static bool operator !=(ViewGroupMask left, ViewGroupMask right) => left.value != right.value;

        public static bool operator ==(ViewGroupMask left, int right) => left.value == right;

        public static bool operator !=(ViewGroupMask left, int right) => left.value != right;

        public static bool operator ==(int left, ViewGroupMask right) => left == right.value;

        public static bool operator !=(int left, ViewGroupMask right) => left != right.value;

        public static ViewGroupMask operator &(ViewGroupMask left, ViewGroupMask right) =>
            new(left.value & right.value);

        public static ViewGroupMask operator &(ViewGroupMask left, int right) => new(left.value & right);

        public static ViewGroupMask operator &(int left, ViewGroupMask right) => new(left & right.value);

        public static ViewGroupMask operator |(ViewGroupMask left, ViewGroupMask right) =>
            new(left.value | right.value);

        public static ViewGroupMask operator |(ViewGroupMask left, int right) => new(left.value | right);

        public static ViewGroupMask operator |(int left, ViewGroupMask right) => new(left | right.value);

        public static ViewGroupMask operator ^(ViewGroupMask left, ViewGroupMask right) =>
            new(left.value ^ right.value);

        public static ViewGroupMask operator ^(ViewGroupMask left, int right) => new(left.value & right);

        public static ViewGroupMask operator ^(int left, ViewGroupMask right) => new(left & right.value);

        public static ViewGroupMask operator ~(ViewGroupMask mask) => new(~mask.value);

        public override readonly bool Equals(object obj) => obj is ViewGroupMask other && Equals(other);

        public readonly bool Equals(ViewGroupMask other) => value == other.value;

        public override readonly int GetHashCode() => value.GetHashCode();

        public override readonly string ToString() => value.ToString();

        /// <summary>
        /// A mask representing all groups (value of -1).
        /// </summary>
        public static readonly ViewGroupMask Everything = new(-1);

        /// <summary>
        /// A mask representing no groups (value of 0).
        /// </summary>
        public static readonly ViewGroupMask Nothing = new(0);
    }
}
