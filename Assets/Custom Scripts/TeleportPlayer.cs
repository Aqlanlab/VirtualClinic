using UnityEngine;

public class TeleportPlayer : MonoBehaviour
{
    [SerializeField] private Transform player;

    public void Teleport()
    {
        player.position = new Vector3(
            19.0990009f,
            -3.420555e-07f,
            -8.72700024f
        );
    }
}
