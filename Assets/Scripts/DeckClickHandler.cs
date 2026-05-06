using UnityEngine;

public class DeckClickHandler : MonoBehaviour
{
    [System.Obsolete]
    private void OnEnable()
    {
        InputHandler.OnClick += HandleClick;
    }

    [System.Obsolete]
    private void OnDisable()
    {
        InputHandler.OnClick -= HandleClick;
    }

    [System.Obsolete]
    void HandleClick(Vector2 screenPos)
    {
        if (Camera.main == null)
        {
            Debug.LogError("Main Camera not found! Make sure camera has 'MainCamera' tag.");
            return;
        }

        Vector2 worldPos = Camera.main.ScreenToWorldPoint(screenPos);

        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

        if (hit.collider != null && hit.collider.gameObject == gameObject)
        {
            Debug.Log("Deck Clicked");

            GameManager gm = FindAnyObjectByType<GameManager>();

            if (gm == null)
            {
                Debug.LogError("GameManager NOT FOUND in scene!");
                return;
            }

            gm.OnDeckClicked();
        }
    }
}