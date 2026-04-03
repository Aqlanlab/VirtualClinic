using UnityEngine;
using Unity.Netcode;

public class DeleteOnStart : NetworkBehaviour
{
    [SerializeField] private GameObject objectToDelete;

    public override void OnNetworkSpawn()
    {
        if (objectToDelete != null)
        {
            Destroy(objectToDelete);
        }
    }
}