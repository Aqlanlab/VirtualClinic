using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Vivox;

public class RelayNetworkStartupUI : MonoBehaviour
{
    [Header("UI References")]
    // Main menu buttons for hosting, joining, and browsing lobbies
    public Button hostButton;
    public Button refreshButton;
    public Button joinSelectedButton;
    public Button joinByCodeButton;
    public Button prevLobbyButton;
    public Button nextLobbyButton;

    // UI text fields for selected lobby, status messages, and host join code
    public TMP_Text selectedLobbyText;
    public TMP_Text statusText;
    public TMP_Text joinCodeCreatedText;

    // Input field for entering a Relay join code manually
    public TMP_InputField joinCodeInputField;

    [Header("Relay Settings")]
    // Number of clients allowed to join, not counting the host
    [Tooltip("Number of CLIENT connections allowed (host not counted). Example: 3 means host + 3 clients = 4 players.")]
    public int maxClientConnections = 3;

    // Relay transport type: udp, dtls, or wss
    [Tooltip("udp / dtls / wss. Use 'dtls' for encrypted, 'wss' for WebGL.")]
    public string connectionType = "dtls";

    [Header("Lobby Settings")]
    // Name shown in the public/private lobby list
    [Tooltip("Name shown in the lobby list.")]
    public string lobbyName = "Clinic Lobby";

    // If true, the lobby will not appear in public searches
    [Tooltip("If true, the lobby won't show up in public queries.")]
    public bool lobbyPrivate = false;

    // How often the host sends heartbeat pings to keep the lobby alive
    [Tooltip("Heartbeat interval (seconds). Must be < 30s to keep lobby active/visible.")]
    public float lobbyHeartbeatSeconds = 15f;

    // Stores the current host join code and UI state
    private string joinCodeCreated = "";
    private bool busy;
    private string status = "";

    // Host lobby reference and its heartbeat coroutine
    private Lobby hostLobby;
    private Coroutine heartbeatRoutine;

    // Current queried lobby list and selected index
    private List<Lobby> lobbyList = new List<Lobby>();
    private int selectedLobbyIndex = -1;

    // Shared initialization state for Unity Services
    private static bool ugsReady;
    private static System.Threading.Tasks.Task initTask;

    // Shared initialization state for Vivox
    private static bool vivoxReady;
    private static System.Threading.Tasks.Task vivoxInitTask;

    private Lobby joinedLobby; // The lobby a client joined, used later for voice join

    private async void Awake()
{
    if (hostButton != null) hostButton.onClick.AddListener(StartHostWithRelayAndLobbyClicked);
    if (refreshButton != null) refreshButton.onClick.AddListener(RefreshLobbiesClicked);
    if (joinSelectedButton != null) joinSelectedButton.onClick.AddListener(JoinSelectedLobbyClicked);
    if (joinByCodeButton != null) joinByCodeButton.onClick.AddListener(StartClientWithRelayClicked);
    if (prevLobbyButton != null) prevLobbyButton.onClick.AddListener(SelectPreviousLobby);
    if (nextLobbyButton != null) nextLobbyButton.onClick.AddListener(SelectNextLobby);

    try
    {
        await EnsureUGSReady();
    }
    catch (Exception e)
    {
        Debug.LogException(e);
        status = $"UGS init failed: {e.Message}";
    }

    RefreshVisuals();
}

    private void RefreshVisuals()
    {
        if (hostButton != null) hostButton.interactable = !busy;
        if (refreshButton != null) refreshButton.interactable = !busy;
        if (joinSelectedButton != null) joinSelectedButton.interactable = !busy;
        if (joinByCodeButton != null) joinByCodeButton.interactable = !busy;
        if (prevLobbyButton != null) prevLobbyButton.interactable = !busy && lobbyList != null && lobbyList.Count > 0;
        if (nextLobbyButton != null) nextLobbyButton.interactable = !busy && lobbyList != null && lobbyList.Count > 0;
        if (joinCodeInputField != null) joinCodeInputField.interactable = !busy;

        if (statusText != null)
            statusText.text = status;

        if (joinCodeCreatedText != null)
        {
            joinCodeCreatedText.text = string.IsNullOrEmpty(joinCodeCreated)
                ? ""
                : $"Host Join Code: {joinCodeCreated}";
        }

        if (selectedLobbyText != null)
        {
            if (lobbyList == null || lobbyList.Count == 0 || selectedLobbyIndex < 0 || selectedLobbyIndex >= lobbyList.Count)
            {
                selectedLobbyText.text = "(No lobbies found)";
            }
            else
            {
                var l = lobbyList[selectedLobbyIndex];
                int currentPlayers = l.Players != null ? l.Players.Count : 0;
                selectedLobbyText.text = $"{l.Name} ({currentPlayers}/{l.MaxPlayers})";
            }
        }
    }


    private async void StartHostWithRelayAndLobbyClicked()
    {
        busy = true;
        status = "Creating Relay allocation...";
        joinCodeCreated = "";
        RefreshVisuals();

        try
        {
            await EnsureUGSReady();
            await EnsureVivoxReady();

            if (VivoxService.Instance == null)
                throw new Exception("VivoxService.Instance is null.");

            if (!VivoxService.Instance.IsLoggedIn)
                throw new Exception("Vivox is not logged in.");

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxClientConnections);

            if (NetworkManager.Singleton == null)
                throw new Exception("NetworkManager.Singleton is null. Make sure there is an active NetworkManager in the scene.");

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
                throw new Exception("UnityTransport not found on NetworkManager.");

            if (string.IsNullOrWhiteSpace(connectionType))
                connectionType = "dtls";

            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));
            transport.UseWebSockets = string.Equals(connectionType, "wss", StringComparison.OrdinalIgnoreCase);

            joinCodeCreated = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log("Relay Join Code: " + joinCodeCreated);

            status = "Creating Lobby (publishing join code)...";
            RefreshVisuals();

            int maxPlayersTotal = maxClientConnections + 1;

            Lobby createdLobby = await LobbyService.Instance.CreateLobbyAsync(
                lobbyName,
                maxPlayersTotal,
                new CreateLobbyOptions
                {
                    IsPrivate = lobbyPrivate,
                    Data = new Dictionary<string, DataObject>
                    {
                    { "relayJoinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCodeCreated) }
                    }
                }
            );

            if (createdLobby == null)
                throw new Exception("CreateLobbyAsync returned null.");

            hostLobby = createdLobby;

            Debug.Log($"Created lobby: {hostLobby.Name}");
            Debug.Log($"Created lobby id: {hostLobby.Id}");

            if (heartbeatRoutine != null)
                StopCoroutine(heartbeatRoutine);

            heartbeatRoutine = StartCoroutine(HeartbeatLobby(hostLobby.Id, lobbyHeartbeatSeconds));

            status = "Starting Host...";
            RefreshVisuals();

            bool ok = NetworkManager.Singleton.StartHost();

            if (!ok)
            {
                status = "StartHost() failed. Cleaning up lobby...";
                RefreshVisuals();

                await SafeDeleteHostLobby();
                status = "StartHost() failed.";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(createdLobby.Id))
                    throw new Exception("createdLobby.Id is null or empty before Vivox join.");

                Debug.Log($"Joining Vivox with lobby id: {createdLobby.Id}");
                await VivoxService.Instance.JoinGroupChannelAsync(createdLobby.Id, ChatCapability.AudioOnly);
                Debug.Log("Joined Vivox host channel");

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
            RefreshVisuals();
        }
    }

    private async void StartClientWithRelayClicked()
    {
        busy = true;
        status = "Joining Relay allocation...";
        RefreshVisuals();

        try
        {
            await EnsureUGSReady();
            await EnsureVivoxReady();

            string joinCodeInput = joinCodeInputField != null ? joinCodeInputField.text.Trim() : "";

            if (string.IsNullOrWhiteSpace(joinCodeInput))
                throw new Exception("Enter a join code first.");

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCodeInput);

            if (NetworkManager.Singleton == null)
                throw new Exception("NetworkManager.Singleton is null.");

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
                throw new Exception("UnityTransport not found on NetworkManager.");

            if (string.IsNullOrWhiteSpace(connectionType))
                connectionType = "dtls";

            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, connectionType));
            transport.UseWebSockets = string.Equals(connectionType, "wss", StringComparison.OrdinalIgnoreCase);

            status = "Starting Client...";
            RefreshVisuals();

            bool ok = NetworkManager.Singleton.StartClient();

            if (ok && joinedLobby != null)
            {
                Debug.Log($"Vivox initialized/logged in: {vivoxReady} / {VivoxService.Instance.IsLoggedIn}");
                await VivoxService.Instance.JoinGroupChannelAsync(joinedLobby.Id, ChatCapability.AudioOnly);
                status = "Client started and joined voice.";
            }
            else
            {
                status = ok ? "Client started, but no joined lobby was available for Vivox." : "StartClient() failed.";
            }
        }
        catch (Exception e)
        {
            status = $"Client failed: {e.Message}";
            Debug.LogException(e);
        }
        finally
        {
            busy = false;
            RefreshVisuals();
        }
    }

    private async void RefreshLobbiesClicked()
    {
        busy = true;
        status = "Querying lobbies...";
        RefreshVisuals();

        try
        {
            await EnsureUGSReady();

            QueryResponse qr = await LobbyService.Instance.QueryLobbiesAsync();
            lobbyList = qr.Results ?? new List<Lobby>();

            selectedLobbyIndex = lobbyList.Count > 0 ? 0 : -1;
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
            RefreshVisuals();
        }
    }

    private async void JoinSelectedLobbyClicked()
    {
        if (lobbyList == null || lobbyList.Count == 0 || selectedLobbyIndex < 0 || selectedLobbyIndex >= lobbyList.Count)
        {
            status = "No lobby selected. Click Refresh first.";
            RefreshVisuals();
            return;
        }

        try
        {
            await EnsureUGSReady();

            joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyList[selectedLobbyIndex].Id);

            if (joinedLobby.Data != null && joinedLobby.Data.TryGetValue("relayJoinCode", out var codeObj))
            {
                if (joinCodeInputField != null)
                    joinCodeInputField.text = codeObj.Value;

                StartClientWithRelayClicked();
            }
            else
            {
                status = "Joined lobby, but relayJoinCode is missing.";
                RefreshVisuals();
            }
        }
        catch (Exception e)
        {
            status = $"Lobby join failed: {e.Message}";
            Debug.LogException(e);
            RefreshVisuals();
        }
    }

    public void SelectPreviousLobby()
    {
        if (lobbyList == null || lobbyList.Count == 0) return;
        selectedLobbyIndex = Wrap(selectedLobbyIndex - 1, lobbyList.Count);
        RefreshVisuals();
    }

    public void SelectNextLobby()
    {
        if (lobbyList == null || lobbyList.Count == 0) return;
        selectedLobbyIndex = Wrap(selectedLobbyIndex + 1, lobbyList.Count);
        RefreshVisuals();
    }

    private IEnumerator HeartbeatLobby(string lobbyId, float intervalSeconds)
    {
        if (intervalSeconds <= 0f)
            intervalSeconds = 15f;

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

        try
        {
            if (VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn)
            {
                await VivoxService.Instance.LeaveAllChannelsAsync();
                await VivoxService.Instance.LogoutAsync();
            }
        }
        catch { }

        if (hostLobby != null)
        {
            try
            {
                await LobbyService.Instance.DeleteLobbyAsync(hostLobby.Id);
            }
            catch { }

            hostLobby = null;
        }

        joinedLobby = null;
    }

    private void OnDestroy()
    {
        _ = SafeDeleteHostLobby();
    }
    private static System.Threading.Tasks.Task EnsureUGSReady()
    {
        if (ugsReady)
            return System.Threading.Tasks.Task.CompletedTask;

        if (initTask != null)
            return initTask;

        initTask = EnsureUGSReadyInternal();
        return initTask;
    }


    private static async System.Threading.Tasks.Task EnsureUGSReadyInternal()
    {
        try
        {
            await UnityServices.InitializeAsync();
            Debug.Log("UGS initialized");

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Auth signed in");

            ugsReady = true;
        }
        finally
        {
            if (!ugsReady)
                initTask = null;
        }
    }

    private static int Wrap(int i, int len)
    {
        if (len <= 0) return 0;
        i %= len;
        if (i < 0) i += len;
        return i;
    }
    private static System.Threading.Tasks.Task EnsureVivoxReady()
    {
        if (VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn)
        {
            vivoxReady = true;
            return System.Threading.Tasks.Task.CompletedTask;
        }

        if (vivoxInitTask != null)
            return vivoxInitTask;

        vivoxInitTask = EnsureVivoxReadyInternal();
        return vivoxInitTask;
    }

    private static async System.Threading.Tasks.Task EnsureVivoxReadyInternal()
    {
        try
        {
            await VivoxService.Instance.InitializeAsync();
            Debug.Log("Vivox initialized");

            await VivoxService.Instance.LoginAsync();
            Debug.Log("Vivox logged in");

            vivoxReady = true;
        }
        finally
        {
            if (!(VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn))
                vivoxInitTask = null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ugsReady = false;
        initTask = null;
        vivoxReady = false;
        vivoxInitTask = null;
    }
}