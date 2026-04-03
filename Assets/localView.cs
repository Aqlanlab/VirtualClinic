using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.SocialPlatforms;

public class LocalAvatarView : NetworkBehaviour
{
    [SerializeField] private SkinnedMeshRenderer fullMesh;
    [SerializeField] private SkinnedMeshRenderer headlessMesh;

    public override void OnNetworkSpawn()
    {
        bool isLocal = IsOwner;

        if (fullMesh != null)
            fullMesh.enabled = !isLocal;

        if (headlessMesh != null)
            headlessMesh.enabled = isLocal;
    }
}