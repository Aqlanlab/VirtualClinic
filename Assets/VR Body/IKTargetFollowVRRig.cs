using Unity.Netcode;
using UnityEngine;

[System.Serializable]
public class VRMap
{
    public Transform vrTarget;
    public Transform ikTarget;
    public Vector3 trackingPositionOffset;
    public Vector3 trackingRotationOffset;
}

public class IKTargetFollowVRRig : NetworkBehaviour
{
    [Range(0, 1)]
    public float turnSmoothness = 0.1f;

    public VRMap head;
    public VRMap leftHand;
    public VRMap rightHand;

    public Vector3 headBodyPositionOffset;
    public float headBodyYawOffset;

    private void LateUpdate()
    {
        if (!IsOwner)
            return;

        if (head == null || head.ikTarget == null)
            return;

        transform.position = head.ikTarget.position + headBodyPositionOffset;

        float yaw = head.ikTarget.eulerAngles.y + headBodyYawOffset;
        Quaternion targetRotation = Quaternion.Euler(0f, yaw, 0f);

        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            targetRotation,
            turnSmoothness
        );
    }
}