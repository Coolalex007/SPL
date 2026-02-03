using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public class ServicesInitialization : MonoBehaviour
{
    static readonly SemaphoreSlim s_InitLock = new SemaphoreSlim(1, 1);
    static Task s_InitTask;

    async void Awake()
    {
        await EnsureInitializedAsync();
    }

    public static async Task EnsureInitializedAsync()
    {
        if (s_InitTask != null)
            await s_InitTask;

        await s_InitLock.WaitAsync();
        try
        {
            if (s_InitTask == null)
            {
                s_InitTask = InitializeServicesAsync();
            }
        }
        finally
        {
            s_InitLock.Release();
        }

        await s_InitTask;
    }

    static async Task InitializeServicesAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            try
            {
                await UnityServices.InitializeAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize Unity Services: {ex}");
                return;
            }
        }

        if (AuthenticationService.Instance.IsSignedIn)
            return;

        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        catch (AuthenticationException ex)
        {
            Debug.LogError($"Failed to sign in anonymously: {ex}");
        }
        catch (RequestFailedException ex)
        {
            Debug.LogError($"Failed to sign in anonymously: {ex}");
        }
    }
}
