using UnityEngine;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Threading.Tasks;
using System.Collections.Generic;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance;

    Lobby currentLobby;

    void Awake()
    {
        Instance = this;
    }

    // / CREATE ROOM (HOST)
    public async void CreateLobby()
    {
        Debug.Log("Creating Lobby...");
        var options = new CreateLobbyOptions();
        options.IsPrivate = false;
        currentLobby = await LobbyService.Instance.CreateLobbyAsync("GoFishRoom", 4, options);
        Debug.Log("Lobby Created → Code: " + currentLobby.LobbyCode);
        await CreateRelay();
    }

    //JOIN ROOM WITH CODE
    public async void JoinLobby(string code)
    {
        Debug.Log("Joining Lobby with code: " + code);
        currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code);
        Debug.Log("Joined Lobby");
        await JoinRelay();
    }

    //CREATE RELAY (HOST SIDE)
    async Task CreateRelay()
    {
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(4);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        Debug.Log("Relay Code: " + joinCode);
        //SAVE RELAY CODE IN LOBBY (IMPORTANT)
        await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, new UpdateLobbyOptions
        {
            Data = new Dictionary<string, DataObject>
            {
                { "relayCode", new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
            }
        });

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData
        );
        Debug.Log("Starting Host...");
        NetworkManager.Singleton.StartHost();
    }

    //JOIN RELAY (CLIENT SIDE)
    async Task JoinRelay()
    {
        string relayCode = currentLobby.Data["relayCode"].Value;
        Debug.Log("Joining Relay with code: " + relayCode);
        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(relayCode);
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        transport.SetRelayServerData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData,
            allocation.HostConnectionData
        );
        Debug.Log("Starting Client...");
        NetworkManager.Singleton.StartClient();
    }
}