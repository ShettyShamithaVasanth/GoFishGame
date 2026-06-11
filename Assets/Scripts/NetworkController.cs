using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;

public class NetworkController : MonoBehaviour
{
    public static NetworkController Instance;
    public Button StartGameButton;
    public Button nextTurnButton;
    public Button askButton;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }

    }
    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    void OnClientConnected(ulong clientId)
    {
        Debug.Log("Client Connected: " + clientId);
    }
    public void StartHost()
    {
        if (NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        NetworkManager.Singleton.StartHost();
        Debug.Log("Host Started");
        StartGameButton.gameObject.SetActive(true);
        nextTurnButton.gameObject.SetActive(true);
        // askButton.gameObject.SetActive(true);
    }

    public void StartClient()
    {
        if (NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        NetworkManager.Singleton.StartClient();
        Debug.Log("Client Started");
        StartGameButton.gameObject.SetActive(false);
        nextTurnButton.gameObject.SetActive(false);
        // askButton.gameObject.SetActive(false);
    }

    void OnApplicationQuit()
    {
        ShutdownNetwork();
    }
    void OnDisable()
    {
        ShutdownNetwork();
    }

    void ShutdownNetwork()
    {
        if (NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsHost ||
             NetworkManager.Singleton.IsClient ||
             NetworkManager.Singleton.IsServer))
        {
            Debug.Log("Shutting down NetworkManager...");
            NetworkManager.Singleton.Shutdown();
        }
    }
}