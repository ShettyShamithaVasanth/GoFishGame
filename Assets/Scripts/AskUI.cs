using UnityEngine;
using Unity.Netcode;

public class AskUI : MonoBehaviour
{
    [System.Obsolete]
    public void AskTest()
    {
        ulong myId= NetworkManager.Singleton.LocalClientId;
        // Temporary :choose opponent
        ulong targetId = myId==0?1UL:0UL;
        int testRank = Random.Range(1, 13);
        NetworkGameManager.Instance.RequestCardRpc(testRank, targetId);
    }
}
