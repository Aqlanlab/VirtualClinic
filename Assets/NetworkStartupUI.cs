using UnityEngine;
using Unity.Netcode;

public class NetworkStartupUI : MonoBehaviour
{
    private bool started;

    void OnGUI()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // Once started or running, don't draw UI
        if (started || nm.IsClient || nm.IsServer) return;

        float w = 200f, h = 40f;
        float x = 10f, y = 10f;

        if (GUI.Button(new Rect(x, y, w, h), "Host")) started = nm.StartHost();
        if (GUI.Button(new Rect(x, y + h + 10, w, h), "Client")) started = nm.StartClient();
        if (GUI.Button(new Rect(x, y + 2 * (h + 10), w, h), "Server")) started = nm.StartServer();
    }
}
