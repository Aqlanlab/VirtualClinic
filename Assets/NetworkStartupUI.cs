using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

using Unity.Services.Core;
using Unity.Services.Authentication;

using Unity.Services.Relay;
using Unity.Services.Relay.Models;

// Lobby
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

// Needed for RelayServerData type used by UnityTransport
using Unity.Networking.Transport.Relay;
//
public class RelayNetworkStartupUI : MonoBehaviour
{
    [Header("Relay Settings")]
    [Tooltip("Number of CLIENT connections allowed (host not counted). Example: 3 means host + 3 clients = 4 players.")]
    public int maxClientConnections = 3;

    [Tooltip("udp / dtls / wss. Use 'dtls' for encrypted, 'wss' for WebGL.")]
    public string connectionType = "dtls";

    [Header("Lobby Settings")]
    [Tooltip("Name shown in the lobby list.")]
    public string lobbyName = "Clinic Lobby";

    [Tooltip("If true, the lobby won't show up in public queries.")]
    public bool lobbyPrivate = false;

    [Tooltip("Heartbeat interval (seconds). Must be < 30s to keep lobby active/visible.")]
    public float lobbyHeartbeatSeconds = 15f;

    private string joinCodeCreated = "";
    private string joinCodeInput = "";

    private bool busy;
    private string status = "";

    // Lobby state
    private Lobby hostLobby;
    private Coroutine heartbeatRoutine;

    private List<Lobby> lobbyList = new List<Lobby>();
    private int selectedLobbyIndex = -1;

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

        float w = 320f, h = 40f;
        float x = 10f, y = 10f;

        GUI.Label(new Rect(x, y, 800f, 25f), "Relay + Lobby (No typing join code needed)");
        y += 30f;

        GUI.enabled = !busy;

        // HOST
        if (GUI.Button(new Rect(x, y, w, h), "Host (Create Relay + Create Lobby + StartHost)"))
            StartHostWithRelayAndLobbyClicked();
        y += h + 10f;

        // LOBBY DISCOVERY
        if (GUI.Button(new Rect(x, y, w, h), "Refresh Lobby List"))
            RefreshLobbiesClicked();
        y += h + 10f;

        DrawLobbySelector(x, y, w, h, out float usedHeight);
        y += usedHeight + 10f;

        if (GUI.Button(new Rect(x, y, w, h), "Join Selected Lobby"))
            JoinSelectedLobbyClicked();
        y += h + 10f;

        // Manual join code (fallback)
        GUI.Label(new Rect(x, y, 500f, 25f), "Or join by code (manual):");
        y += 25f;

        joinCodeInput = GUI.TextField(new Rect(x, y, w, h), joinCodeInput);
        y += h + 10f;

        if (GUI.Button(new Rect(x, y, w, h), "Client (Join Relay + StartClient)"))
            StartClientWithRelayClicked();
        y += h + 10f;

        GUI.enabled = true;

        if (!string.IsNullOrEmpty(joinCodeCreated))
        {
            GUI.Label(new Rect(x, y, 900f, 25f), $"Host Join Code: {joinCodeCreated}");
            y += 25f;
        }

        if (!string.IsNullOrEmpty(status))
            GUI.Label(new Rect(x, y, 900f, 60f), status);
    }

    private void DrawLobbySelector(float x, float y, float w, float h, out float usedHeight)
    {
        usedHeight = 0f;

        GUI.Label(new Rect(x, y, 500f, 25f), "Select Lobby:");
        y += 25f;
        usedHeight += 25f;

        if (lobbyList == null || lobbyList.Count == 0)
        {
            GUI.Label(new Rect(x, y, 900f, 25f), "(No lobbies found. Click Refresh.)");
            y += 25f;
            usedHeight += 25f;
            return;
        }

        if (selectedLobbyIndex < 0 || selectedLobbyIndex >= lobbyList.Count)
            selectedLobbyIndex = 0;

        // Arrow selector: <  [LobbyName (players/max)]  >
        float arrowW = 40f;
        float valueW = w - arrowW - arrowW;

        if (GUI.Button(new Rect(x, y, arrowW, h), "<"))
            selectedLobbyIndex = Wrap(selectedLobbyIndex - 1, lobbyList.Count);

        var l = lobbyList[selectedLobbyIndex];
        string label = $"{l.Name} ({l.Players.Count}/{l.MaxPlayers})";
        GUI.Box(new Rect(x + arrowW, y, valueW, h), label);

        if (GUI.Button(new Rect(x + arrowW + valueW, y, arrowW, h), ">"))
            selectedLobbyIndex = Wrap(selectedLobbyIndex + 1, lobbyList.Count);

        y += h;
        usedHeight += h;
    }

    private async void StartHostWithRelayAndLobbyClicked()
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
            transport.UseWebSockets = connectionType.Equals("wss", StringComparison.OrdinalIgnoreCase);

            joinCodeCreated = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log("Relay Join Code: " + joinCodeCreated);

            status = "Creating Lobby (publishing join code)...";

            // Lobby max players includes host
            int maxPlayersTotal = maxClientConnections + 1;

            hostLobby = await LobbyService.Instance.CreateLobbyAsync(
                lobbyName,
                maxPlayersTotal,
                new CreateLobbyOptions
                {
                    IsPrivate = lobbyPrivate,
                    Data = new Dictionary<string, DataObject>
                    {
                        // Public so clients can see it in query results
                        { "relayJoinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCodeCreated) }
                    }
                }
            );

            // Keep lobby active/visible
            if (heartbeatRoutine != null) StopCoroutine(heartbeatRoutine);
            heartbeatRoutine = StartCoroutine(HeartbeatLobby(hostLobby.Id, lobbyHeartbeatSeconds));

            status = "Starting Host...";
            bool ok = NetworkManager.Singleton.StartHost();

            if (!ok)
            {
                status = "StartHost() failed. Cleaning up lobby...";
                await SafeDeleteHostLobby();
                status = "StartHost() failed.";
            }
            else
            {
                status = lobbyPrivate
                    ? $"Host started. Lobby is PRIVATE. Share join code: {joinCodeCreated}"
                    : $"Host started. Lobby is PUBLIC. Clients can select it from the list.";
            }
        }
        catch (Exception e)
        {
            status = $"Host failed: {e.Message}";
            Debug.LogException(e);
            await SafeDeleteHostLobby();
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
            transport.UseWebSockets = connectionType.Equals("wss", StringComparison.OrdinalIgnoreCase);

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

    private async void RefreshLobbiesClicked()
    {
        busy = true;
        status = "Querying lobbies...";

        try
        {
            await EnsureUGSReady();

            QueryResponse qr = await LobbyService.Instance.QueryLobbiesAsync();
            lobbyList = qr.Results ?? new List<Lobby>();

            selectedLobbyIndex = (lobbyList.Count > 0) ? 0 : -1;
            status = $"Found {lobbyList.Count} lobbies.";
        }
        catch (Exception e)
        {
            status = $"Query failed: {e.Message}";
            Debug.LogException(e);
        }
        finally
        {
            busy = false;
        }
    }

    private void JoinSelectedLobbyClicked()
    {
        if (lobbyList == null || lobbyList.Count == 0 || selectedLobbyIndex < 0 || selectedLobbyIndex >= lobbyList.Count)
        {
            status = "No lobby selected. Click Refresh first.";
            return;
        }

        var l = lobbyList[selectedLobbyIndex];

        if (l.Data != null && l.Data.TryGetValue("relayJoinCode", out var codeObj))
        {
            joinCodeInput = codeObj.Value;
            StartClientWithRelayClicked(); // reuse existing relay join flow
        }
        else
        {
            status = "Selected lobby is missing relayJoinCode.";
        }
    }

    private IEnumerator HeartbeatLobby(string lobbyId, float intervalSeconds)
    {
        if (intervalSeconds <= 0f) intervalSeconds = 15f;

        var wait = new WaitForSecondsRealtime(intervalSeconds);
        while (true)
        {
            _ = LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return wait;
        }
    }

    private async System.Threading.Tasks.Task SafeDeleteHostLobby()
    {
        if (heartbeatRoutine != null)
        {
            StopCoroutine(heartbeatRoutine);
            heartbeatRoutine = null;
        }

        if (hostLobby != null)
        {
            try { await LobbyService.Instance.DeleteLobbyAsync(hostLobby.Id); }
            catch { /* ignore */ }
            hostLobby = null;
        }
    }

    private void OnDestroy()
    {
        // Best-effort cleanup when leaving play mode / destroying object
        _ = SafeDeleteHostLobby();
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

    private static int Wrap(int i, int len)
    {
        if (len <= 0) return 0;
        i %= len;
        if (i < 0) i += len;
        return i;
    }
}