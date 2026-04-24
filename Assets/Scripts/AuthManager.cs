using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

public class AuthManager : MonoBehaviour
{
    public static bool IsSignedIn = false;

    async void Awake()
    {
        await Initialize();
    }

    async Task Initialize()
    {
        await UnityServices.InitializeAsync();
        //FIX: check before signing in
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Signed in NEW: " + AuthenticationService.Instance.PlayerId);
        }
        else
        {
            Debug.Log("Already signed in: " + AuthenticationService.Instance.PlayerId);
        }
        IsSignedIn = true;
    }
}