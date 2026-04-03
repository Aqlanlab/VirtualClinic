using UnityEngine;
using Unity.Netcode;

public class NetworkVRSetup : NetworkBehaviour
{
    [SerializeField] private IKTargetFollowVRRig ikFollower;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            if (ikFollower != null)
                ikFollower.enabled = false;
            return;
        }

        mainUserRef refs = FindFirstObjectByType<mainUserRef>();

        if (refs == null)
        {
            Debug.LogError("MainUserReferences not found.");
            return;
        }

        ikFollower.head.vrTarget = refs.head;
        ikFollower.leftHand.vrTarget = refs.leftHand;
        ikFollower.rightHand.vrTarget = refs.rightHand;
        ikFollower.enabled = true;
    }
}
