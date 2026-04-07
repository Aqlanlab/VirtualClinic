using Unity.Netcode;
using UnityEngine;

public class VRPlayerSetup : NetworkBehaviour
{
    [Header("Objects only the local owner should use")]
    [SerializeField] private GameObject xrRigRoot;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener playerAudioListener;

    [Header("Scripts that should only run for the local owner")]
    [SerializeField] private MonoBehaviour[] localOnlyScripts;

    [Header("Optional objects to hide on remote players")]
    [SerializeField] private GameObject[] localOnlyObjects;

    public override void OnNetworkSpawn()
    {
        ApplyOwnerState();
    }

    private void ApplyOwnerState()
    {
        bool isLocalOwner = IsOwner;

        if (xrRigRoot != null)
            xrRigRoot.SetActive(isLocalOwner);

        if (playerCamera != null)
            playerCamera.enabled = isLocalOwner;

        if (playerAudioListener != null)
            playerAudioListener.enabled = isLocalOwner;

        if (localOnlyScripts != null)
        {
            foreach (MonoBehaviour script in localOnlyScripts)
            {
                if (script != null)
                    script.enabled = isLocalOwner;
            }
        }

        if (localOnlyObjects != null)
        {
            foreach (GameObject obj in localOnlyObjects)
            {
                if (obj != null)
                    obj.SetActive(isLocalOwner);
            }
        }
    }
}