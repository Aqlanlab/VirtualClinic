using System;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

// Needed for RelayServerData type used by UnityTransport
using Unity.Networking.Transport.Relay;

public class RelayNetworkStartupUI : MonoBehaviour
{
    [Header("Relay Settings")]
    [Tooltip("Number of CLIENT connections allowed (host not counted). Example: 3 means host + 3 clients = 4 players.")]
    public int maxClientConnections = 3;

    [Tooltip("udp / dtls / wss. Use 'dtls' for encrypted, 'wss' for WebGL.")]
    public string connectionType = "dtls";

    private string joinCodeCreated = "";
    private string joinCodeInput = "";

    private bool busy;
    private string status = "";

    private async void Awake()
    {
        // Optional: initialize early so UI is snappy
        await EnsureUGSReady();
    }

    void OnGUI()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // Once started, don't draw the startup UI
        if (nm.IsClient || nm.IsServer) return;

        float w = 260f, h = 40f;
        float x = 10f, y = 10f;

        GUI.Label(new Rect(x, y, 500f, 25f), "Unity Relay (Join Code) Demo");
        y += 30f;

        GUI.enabled = !busy;

        if (GUI.Button(new Rect(x, y, w, h), "Host (Create Relay + StartHost)"))
            StartHostWithRelayClicked();

        y += h + 10f;

        GUI.Label(new Rect(x, y, 500f, 25f), "Join Code:");
        y += 25f;

        joinCodeInput = GUI.TextField(new Rect(x, y, w, h), joinCodeInput);
        y += h + 10f;

        if (GUI.Button(new Rect(x, y, w, h), "Client (Join Relay + StartClient)"))
            StartClientWithRelayClicked();

        GUI.enabled = true;

        y += h + 10f;

        if (!string.IsNullOrEmpty(joinCodeCreated))
            GUI.Label(new Rect(x, y, 600f, 25f), $"Host Join Code: {joinCodeCreated}");

        y += 25f;

        if (!string.IsNullOrEmpty(status))
            GUI.Label(new Rect(x, y, 900f, 60f), status);
    }

    private async void StartHostWithRelayClicked()
    {
        busy = true;
        status = "Creating Relay allocation...";
        joinCodeCreated = "";

        try
        {
            await EnsureUGSReady();

            // Create allocation for N clients (host not counted)
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxClientConnections);

            // Apply relay settings to UnityTransport
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null) throw new Exception("UnityTransport not found on NetworkManager.");

            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));

            // WebGL needs websockets (wss)
            if (connectionType.Equals("wss", StringComparison.OrdinalIgnoreCase))
                transport.UseWebSockets = true;

            joinCodeCreated = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log("Relay Join Code: " + joinCodeCreated);

            status = "Starting Host...";
            bool ok = NetworkManager.Singleton.StartHost();

            status = ok ? $"Host started. Share this join code: {joinCodeCreated}" : "StartHost() failed.";
        }
        catch (Exception e)
        {
            status = $"Host failed: {e.Message}";
            Debug.LogException(e);
        }
        finally
        {
            busy = false;
        }
    }

    private async void StartClientWithRelayClicked()
    {
        busy = true;
        status = "Joining Relay allocation...";

        try
        {
            await EnsureUGSReady();

            if (string.IsNullOrWhiteSpace(joinCodeInput))
                throw new Exception("Enter a join code first.");

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCodeInput.Trim());

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null) throw new Exception("UnityTransport not found on NetworkManager.");

            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, connectionType));

            if (connectionType.Equals("wss", StringComparison.OrdinalIgnoreCase))
                transport.UseWebSockets = true;

            status = "Starting Client...";
            bool ok = NetworkManager.Singleton.StartClient();

            status = ok ? "Client started (connecting...)" : "StartClient() failed.";
        }
        catch (Exception e)
        {
            status = $"Client failed: {e.Message}";
            Debug.LogException(e);
        }
        finally
        {
            busy = false;
        }
    }

    private static bool ugsReady;

    private static async System.Threading.Tasks.Task EnsureUGSReady()
    {
        if (ugsReady) return;

        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        ugsReady = true;
    }
}