#if ENABLE_INPUT_SYSTEM
using System;
using UnityEngine.InputSystem;

namespace Sentinal.InputSystem
{
    /// <summary>
    /// Game-owned primary-player policy. Raise Changed after session/roster ownership changes.
    /// UIAuthority also resolves again after input registration, control changes, and SetPrimaryPlayer.
    /// Return null when no eligible local player exists; never create an input to satisfy this contract.
    /// </summary>
    public interface IPrimaryPlayerResolver
    {
        event Action Changed;
        PlayerInput ResolvePrimaryPlayer(int preferredPlayerIndex, Predicate<PlayerInput> isEligible);
    }
}
#endif
