using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public class ServicesInitialization : MonoBehaviour
{
    async void Awake()
    {
        await InitializeServicesAsync();
    }

    static async Task InitializeServicesAsync()
    {
        if (UnityServices.State == ServicesInitializationState.Initialized)
        {
            return;
        }

        try
        {
            await UnityServices.InitializeAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to initialize Unity Services: {ex}");
            return;
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to sign in anonymously: {ex}");
            }
        }
    }
}
