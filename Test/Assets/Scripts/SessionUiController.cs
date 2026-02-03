using System;
using System.Threading.Tasks;
using Blocks.Sessions.Common;
using TMPro;
using Unity.Netcode;
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
        joinSessionButton.interactable = IsSessionCodeValid(code);
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

            await ServicesInitialization.EnsureInitializedAsync();

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

            await ServicesInitialization.EnsureInitializedAsync();

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
}
