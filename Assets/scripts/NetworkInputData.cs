using Fusion;
using UnityEngine;

/// <summary>
/// Fusion input struct — sent from client to server every simulation tick.
/// The server feeds this back to FixedUpdateNetwork on all peers via GetInput<T>().
/// </summary>
public enum MyButtons
{
    Jump = 0,
}

public struct NetworkInputData : INetworkInput
{
    /// <summary>-1 (left), 0 (none), or 1 (right).</summary>
    public float HorizontalDirection;

    /// <summary>Button states. Use .IsDown(MyButtons.Jump) for edge detection.</summary>
    public NetworkButtons Buttons;
}
