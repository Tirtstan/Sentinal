using System;

namespace Sentinal
{
    /// <summary>
    /// Directions that a <see cref="SelectionNavigator"/> can handle.
    /// </summary>
    [Flags]
    public enum SelectionNavigationDirection
    {
        None = 0,
        Up = 1 << 0,
        Down = 1 << 1,
        Left = 1 << 2,
        Right = 1 << 3,
        UpLeft = 1 << 4,
        UpRight = 1 << 5,
        DownLeft = 1 << 6,
        DownRight = 1 << 7,
        Cardinal = Up | Down | Left | Right,
        Diagonal = UpLeft | UpRight | DownLeft | DownRight,
        All = Cardinal | Diagonal,
    }
}
