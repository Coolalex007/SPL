using System;
using System.Net;
using System.Threading.Tasks;
using Blocks.Sessions.Common;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

public class SessionUiController : MonoBehaviour
{
    const string k_ValidSessionCodeCharacters = "6789BCDFGHJKLMNPQRTWbcdfghjklmnpqrtw";

    [SerializeField] SessionSettings sessionSettings;
    [SerializeField] TMP_InputField createSessionNameInput;
    [SerializeField] Button createSessionButton;
    [SerializeField] TMP_Text sessionCodeText;
    [SerializeField] TMP_InputField joinSessionCodeInput;
    [SerializeField] Button joinSessionButton;

    GameObject createRoot;
    GameObject joinRoot;
    ISession activeSession;
    bool uiHidden;

    void Awake()
    {
        EnsureReferences();
        WireUi();
        UpdateCreateButtonState();
        UpdateJoinButtonState();
    }

    void EnsureReferences()
    {
        if (createRoot == null)
            createRoot = GameObject.Find("Create Session");

        if (createSessionNameInput == null || createSessionButton == null)
        {
            if (createRoot != null)
            {
                if (createSessionNameInput == null)
                    createSessionNameInput = createRoot.GetComponentInChildren<TMP_InputField>(true);
                if (createSessionButton == null)
                    createSessionButton = createRoot.GetComponentInChildren<Button>(true);
            }
        }

        if (joinRoot == null)
            joinRoot = GameObject.Find("Join Session By Code");

        if (joinSessionCodeInput == null || joinSessionButton == null)
        {
            if (joinRoot != null)
            {
                if (joinSessionCodeInput == null)
                    joinSessionCodeInput = joinRoot.GetComponentInChildren<TMP_InputField>(true);
                if (joinSessionButton == null)
                    joinSessionButton = joinRoot.GetComponentInChildren<Button>(true);
            }
        }

        if (sessionCodeText == null)
        {
            var sessionCodeObject = GameObject.Find("Session Code Text");
            if (sessionCodeObject != null)
                sessionCodeText = sessionCodeObject.GetComponent<TMP_Text>();
        }

        if (sessionSettings == null)
        {
            Debug.LogWarning("SessionSettings is not assigned. Create/Join will use defaults from the asset if assigned.");
        }
    }

    void WireUi()
    {
        if (createSessionButton != null)
            createSessionButton.onClick.AddListener(CreateSessionAsync);
        if (joinSessionButton != null)
            joinSessionButton.onClick.AddListener(JoinSessionAsync);
        if (createSessionNameInput != null)
            createSessionNameInput.onValueChanged.AddListener(_ => UpdateCreateButtonState());
        if (joinSessionCodeInput != null)
            joinSessionCodeInput.onValueChanged.AddListener(_ => UpdateJoinButtonState());
    }

    void UpdateCreateButtonState()
    {
        if (createSessionButton == null || createSessionNameInput == null)
            return;

        createSessionButton.interactable = !string.IsNullOrWhiteSpace(createSessionNameInput.text);
    }

    void UpdateJoinButtonState()
    {
        if (joinSessionButton == null || joinSessionCodeInput == null)
            return;

        var code = joinSessionCodeInput.text ?? string.Empty;
        if (sessionSettings != null && sessionSettings.networkType == NetworkType.Direct)
        {
            joinSessionButton.interactable = TryParseDirectAddress(code, out _, out _);
            return;
        }

        joinSessionButton.interactable = IsSessionCodeValid(code);
    }

    void ReportStatus(string message)
    {
        Debug.LogError(message);
        if (sessionCodeText != null)
            sessionCodeText.text = message;
    }

    static bool IsSessionCodeValid(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length is < 6 or > 8)
            return false;

        foreach (var c in value)
        {
            if (!k_ValidSessionCodeCharacters.Contains(c))
                return false;
        }

        return true;
    }

    async void CreateSessionAsync()
    {
        if (createSessionButton != null)
            createSessionButton.interactable = false;

        try
        {
            if (sessionSettings == null)
            {
                Debug.LogError("SessionSettings is not assigned.");
                return;
            }

            if (string.IsNullOrWhiteSpace(createSessionNameInput?.text))
            {
                Debug.LogWarning("Create Session: name is empty.");
                return;
            }

            if (sessionSettings.networkType == NetworkType.Direct)
            {
                StartDirectHost(sessionSettings.ipAddress, sessionSettings.port);
                return;
            }

            if (!await ServicesInitialization.EnsureInitializedAsync())
            {
                ReportStatus("Unity Services failed to initialize or sign in.");
                return;
            }

            if (MultiplayerService.Instance == null)
            {
                Debug.LogError("Multiplayer Services are not initialized.");
                return;
            }

            var options = sessionSettings.ToSessionOptions();
            options.Name = createSessionNameInput.text;

            var hostSession = await MultiplayerService.Instance.CreateSessionAsync(options);
            if (sessionCodeText != null)
                sessionCodeText.text = hostSession?.Code ?? sessionCodeText.text;

            activeSession = hostSession;
            SubscribeSessionEvents();
            StartNetworkHost();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to create session: {ex}");
        }
        finally
        {
            UpdateCreateButtonState();
        }
    }

    async void JoinSessionAsync()
    {
        if (joinSessionButton != null)
            joinSessionButton.interactable = false;

        try
        {
            if (sessionSettings == null)
            {
                Debug.LogError("SessionSettings is not assigned.");
                return;
            }

            if (string.IsNullOrWhiteSpace(joinSessionCodeInput?.text))
            {
                Debug.LogWarning("Join Session: code is empty.");
                return;
            }

            if (sessionSettings.networkType == NetworkType.Direct)
            {
                if (!TryParseDirectAddress(joinSessionCodeInput.text, out var address, out var port))
                {
                    ReportStatus("Invalid LAN address. Use IP or IP:port.");
                    return;
                }

                StartDirectClient(address, port);
                HideSessionUi();
                return;
            }

            if (!await ServicesInitialization.EnsureInitializedAsync())
            {
                ReportStatus("Unity Services failed to initialize or sign in.");
                return;
            }

            if (MultiplayerService.Instance == null)
            {
                Debug.LogError("Multiplayer Services are not initialized.");
                return;
            }

            var options = sessionSettings.ToJoinSessionOptions();
            var session = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinSessionCodeInput.text, options);
            activeSession = session;
            SubscribeSessionEvents();
            StartNetworkClient();
            HideSessionUi();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to join session: {ex}");
        }
        finally
        {
            UpdateJoinButtonState();
        }
    }

    void SubscribeSessionEvents()
    {
        if (activeSession == null)
            return;

        activeSession.PlayerJoined += OnPlayerJoined;
        activeSession.RemovedFromSession += OnSessionEnded;
        activeSession.Deleted += OnSessionEnded;
    }

    void OnPlayerJoined(string playerId)
    {
        if (activeSession?.Players != null && activeSession.Players.Count >= 2)
            HideSessionUi();
    }

    void OnSessionEnded()
    {
        if (activeSession == null)
            return;

        activeSession.PlayerJoined -= OnPlayerJoined;
        activeSession.RemovedFromSession -= OnSessionEnded;
        activeSession.Deleted -= OnSessionEnded;
        activeSession = null;
    }

    void HideSessionUi()
    {
        if (uiHidden)
            return;

        uiHidden = true;
        if (createRoot != null)
            createRoot.SetActive(false);
        if (joinRoot != null)
            joinRoot.SetActive(false);
        if (sessionCodeText != null)
            sessionCodeText.transform.parent?.gameObject.SetActive(false);
    }

    void StartNetworkHost()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager is missing in the scene.");
            return;
        }

        if (!NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.StartHost();
    }

    void StartNetworkClient()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager is missing in the scene.");
            return;
        }

        if (!NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.StartClient();
    }

    void StartDirectHost(string address, ushort port)
    {
        if (!ConfigureUnityTransport(address, port, isHost: true))
            return;

        if (NetworkManager.Singleton.IsListening)
            return;

        if (NetworkManager.Singleton.StartHost())
        {
            if (sessionCodeText != null)
                sessionCodeText.text = $"{address}:{port}";
        }
    }

    void StartDirectClient(string address, ushort port)
    {
        if (!ConfigureUnityTransport(address, port, isHost: false))
            return;

        if (NetworkManager.Singleton.IsListening)
            return;

        NetworkManager.Singleton.StartClient();
    }

    bool ConfigureUnityTransport(string address, ushort port, bool isHost)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager is missing in the scene.");
            return false;
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("UnityTransport is missing on the NetworkManager.");
            return false;
        }

        transport.ConnectionData.Address = address;
        transport.ConnectionData.Port = port;
        if (isHost)
        {
            transport.ConnectionData.ServerListenAddress = address;
        }

        return true;
    }

    static bool TryParseDirectAddress(string input, out string address, out ushort port)
    {
        address = string.Empty;
        port = 0;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();
        var parts = trimmed.Split(':');
        if (parts.Length == 1)
        {
            if (!IPAddress.TryParse(parts[0], out _))
                return false;

            address = parts[0];
            port = 7777;
            return true;
        }

        if (parts.Length == 2)
        {
            if (!IPAddress.TryParse(parts[0], out _))
                return false;

            if (!ushort.TryParse(parts[1], out port))
                return false;

            address = parts[0];
            return true;
        }

        return false;
    }
}
