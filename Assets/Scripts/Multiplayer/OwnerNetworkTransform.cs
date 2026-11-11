using Unity.Netcode.Components;
namespace MakeTrap.Multiplayer
{
    // Starter Assets simulates movement only on the owning player's machine.
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
