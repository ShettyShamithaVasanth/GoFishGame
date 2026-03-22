using UnityEngine;

public class DeckClickHandler : MonoBehaviour
{
    void OnMouseDown()
    {
        Debug.Log("Deck Clicked");
        GameManager gm = FindFirstObjectByType<GameManager>();
        gm.OnDeckClicked();
    }
}