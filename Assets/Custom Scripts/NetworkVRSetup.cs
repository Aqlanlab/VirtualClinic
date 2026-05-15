using Unity.Netcode;
using UnityEngine;

public class NetworkVRSetup : NetworkBehaviour
{
    public Transform avatarHead;
    public Transform avatarLeftHand;
    public Transform avatarRightHand;

    private mainUserRef localRig;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        localRig = FindFirstObjectByType<mainUserRef>();

        if (localRig == null)
        {
            Debug.LogError("mainUserRef not found in scene.");
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (!IsOwner || localRig == null)
            return;

        avatarHead.position = localRig.head.position;
        avatarHead.rotation = localRig.head.rotation;

        avatarLeftHand.position = localRig.leftHand.position;
        avatarLeftHand.rotation = localRig.leftHand.rotation;

        avatarRightHand.position = localRig.rightHand.position;
        avatarRightHand.rotation = localRig.rightHand.rotation;
    }
}