using UnityEngine;

public class DeckClickHandler : MonoBehaviour
{
    private void OnEnable()
    {
        InputHandler.OnClick += HandleClick;
    }

    private void OnDisable()
    {
        InputHandler.OnClick -= HandleClick;
    }

    void HandleClick(Vector2 screenPos)
    {
        Vector2 worldPos = Camera.main.ScreenToWorldPoint(screenPos);

        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

        if (hit.collider != null && hit.collider.gameObject == gameObject)
        {
            Debug.Log("Deck Clicked");

            GameManager gm = FindFirstObjectByType<GameManager>();
            gm.OnDeckClicked();
        }
    }
}