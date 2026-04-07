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
    public Button hostButton;
    public Button refreshButton;
    public Button joinSelectedButton;
    public Button joinByCodeButton;
    public Button prevLobbyButton;
    public Button nextLobbyButton;

    public TMP_Text selectedLobbyText;
    public TMP_Text statusText;
    public TMP_Text joinCodeCreatedText;

    public TMP_InputField joinCodeInputField;

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
    private bool busy;
    private string status = "";

    private Lobby hostLobby;
    private Coroutine heartbeatRoutine;

    private List<Lobby> lobbyList = new List<Lobby>();
    private int selectedLobbyIndex = -1;

    private static bool ugsReady;
    private static System.Threading.Tasks.Task initTask;

    private static bool vivoxReady;
    private static System.Threading.Tasks.Task vivoxInitTask;

    private Lobby joinedLobby;   // for clients

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

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxClientConnections);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
                throw new Exception("UnityTransport not found on NetworkManager.");

            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));
            transport.UseWebSockets = connectionType.Equals("wss", StringComparison.OrdinalIgnoreCase);

            joinCodeCreated = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log("Relay Join Code: " + joinCodeCreated);

            status = "Creating Lobby (publishing join code)...";
            RefreshVisuals();

            int maxPlayersTotal = maxClientConnections + 1;

            hostLobby = await LobbyService.Instance.CreateLobbyAsync(
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
                await EnsureUGSReady();
                await EnsureVivoxReady();
                await VivoxService.Instance.JoinGroupChannelAsync(hostLobby.Id, ChatCapability.AudioOnly);


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

            string joinCodeInput = joinCodeInputField != null ? joinCodeInputField.text.Trim() : "";

            if (string.IsNullOrWhiteSpace(joinCodeInput))
                throw new Exception("Enter a join code first.");

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCodeInput);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
                throw new Exception("UnityTransport not found on NetworkManager.");

            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, connectionType));
            transport.UseWebSockets = connectionType.Equals("wss", StringComparison.OrdinalIgnoreCase);

            status = "Starting Client...";
            RefreshVisuals();

            bool ok = NetworkManager.Singleton.StartClient();

            if (ok && joinedLobby != null)
            {
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

        if (hostLobby != null)
        {
            try
            {
                await VivoxService.Instance.LeaveChannelAsync(hostLobby.Id);

            }
            catch { }

            try
            {
                await LobbyService.Instance.DeleteLobbyAsync(hostLobby.Id);
            }
            catch { }

            hostLobby = null;
        }

        if (joinedLobby != null)
        {
            try
            {
                await VivoxService.Instance.LeaveChannelAsync(joinedLobby.Id);  
            }
            catch { }

            joinedLobby = null;
        }
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
        if (vivoxReady)
            return System.Threading.Tasks.Task.CompletedTask;

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
            if (!vivoxReady)
                vivoxInitTask = null;
        }
    }
}