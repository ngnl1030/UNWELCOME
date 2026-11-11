using Unity.Netcode.Components;
namespace MakeTrap.Multiplayer
{
    public class OwnerNetworkAnimator : NetworkAnimator
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
