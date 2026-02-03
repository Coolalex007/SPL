using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public class ServicesInitialization : MonoBehaviour
{
    static readonly SemaphoreSlim s_InitLock = new SemaphoreSlim(1, 1);
    static Task<bool> s_InitTask;

    async void Awake()
    {
        await EnsureInitializedAsync();
    }

    public static async Task<bool> EnsureInitializedAsync()
    {
        if (s_InitTask != null)
        {
            var existingResult = await s_InitTask;
            if (existingResult)
                return true;
        }

        await s_InitLock.WaitAsync();
        try
        {
            if (s_InitTask == null || (s_InitTask.IsCompleted && !s_InitTask.Result))
            {
                s_InitTask = InitializeServicesAsync();
            }
        }
        finally
        {
            s_InitLock.Release();
        }

        return await s_InitTask;
    }

    static async Task<bool> InitializeServicesAsync()
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
                return false;
            }
        }

        if (AuthenticationService.Instance.IsSignedIn)
            return true;

        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        catch (AuthenticationException ex)
        {
            Debug.LogError($"Failed to sign in anonymously: {ex}");
            return false;
        }
        catch (RequestFailedException ex)
        {
            Debug.LogError($"Failed to sign in anonymously: {ex}");
            return false;
        }

        return AuthenticationService.Instance.IsSignedIn;
    }
}
