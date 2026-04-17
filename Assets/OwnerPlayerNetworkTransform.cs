using Unity.Netcode.Components;

public class OwnerPlayerNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
