using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public class PlayFabLogin : MonoBehaviour
{
    void Start()
    {
        Login();
    }

    void Login()
    {
        var request = new LoginWithCustomIDRequest
        {
            CustomId = SystemInfo.deviceUniqueIdentifier,
            CreateAccount = true
        };

        Debug.Log("Attempting PlayFab Login...");
        PlayFabClientAPI.LoginWithCustomID(request, OnSuccess, OnError);
    }

    void OnSuccess(LoginResult result)
    {
        Debug.Log("✅ PlayFab Login SUCCESS");

        // 🔥 LOAD CLOUD DATA AFTER LOGIN
        PlayFabProfileManager.Instance.LoadProfile();
    }

    void OnError(PlayFabError error)
    {
        Debug.LogError("❌ PlayFab Login FAILED: " + error.GenerateErrorReport());
    }
}