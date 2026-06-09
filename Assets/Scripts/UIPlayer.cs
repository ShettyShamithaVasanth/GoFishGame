using UnityEngine;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;
using System.Collections;
using UnityEngine.InputSystem;

public class UIPlayer : MonoBehaviour, ICardOwner
{
    [Header("Profile")]
    public SpriteRenderer profilePhoto;

    [Header("Name")]
    public SpriteRenderer nameBar;
    public TextMeshPro nameLabel;

    [Header("Cards")]
    public Transform cardHolder;
    public GameObject cardPrefab;
    public CardData cardData;

    [Header("Turn Animation")]
    public Transform profileAnim;
    [Header("Popups")]
    public GameObject popup1;  // Current Player popup
    public GameObject popup2;  // Target Player popup
    public TextMeshPro popup1Text;
    public TextMeshPro popup2Text;
    private UICard selectedCard;

    [Header("Score")]
    public TextMeshPro scoreText;
    public bool canInteract = false;
    public AvatarDatabase avatarDatabase;
    private Player playerInstance;
    private List<GameObject> spawnedCards = new List<GameObject>();
    public event System.Action<UIPlayer, int> OnTargetClicked;
    public event System.Action<UIPlayer, int> OnRankSelected;

    public void Initialize(Player player)
    {

        if (playerInstance != null)
            playerInstance.OnTurnChanged -= HandleTurnChanged;

        playerInstance = player;

        //HUMAN PLAYER USES PROFILE DATA
        nameLabel.text = player.PlayerName;

        if (profilePhoto != null)
        {
            int index = player.AvatarIndex;

            if (avatarDatabase != null &&
                avatarDatabase.avatarSprites != null &&
                index >= 0 &&
                index < avatarDatabase.avatarSprites.Length)
            {
                profilePhoto.sprite = avatarDatabase.avatarSprites[index];
            }
        }

        Debug.Log("UI PLAYER INITIALIZED: " + player.PlayerName);
        // Subscribe to turn event
        playerInstance.OnTurnChanged += HandleTurnChanged;
        StopTurnAnimation();
        if (popup1 != null)
            popup1.SetActive(false);

        if (popup2 != null)
            popup2.SetActive(false);
        if (scoreText != null)
            scoreText.text = "0";
    }

    void Start()
    {
        //  canInteract = false;
    }
    public void UpdateScore(int newScore)
    {
        if (scoreText != null)
            scoreText.text = newScore.ToString();
    }

    private Tween rotateTween;
    private void HandleTurnChanged(Player player, bool isMyTurn)
    {
        if (isMyTurn)
        {
            StartTurnAnimation();
            //ONLY AI should auto show popup
            if (!player.IsHuman && !GameModeManager.isOnlineMode)
            {
                ShowCurrentPlayerPopup();
            }
        }
        else
        {
            StopTurnAnimation();
            HideTurnPopupOnly();   // Only hide popup1

        }
    }
    void ShowCurrentPlayerPopup()
    {
        Debug.Log(playerInstance.PlayerName + " showing popup1");

        if (popup1Text != null)
            popup1Text.text = "";   // ⭐ CLEAR OLD TEXT

        if (popup1 != null)
            popup1.SetActive(true);

        if (popup2 != null)
            popup2.SetActive(false);
    }
    public void HideTurnPopupOnly()
    {
        if (popup1 != null)
            popup1.SetActive(false);
    }
    void StartTurnAnimation()
    {
        profileAnim.gameObject.SetActive(true);

        // Kill old tween if exists
        rotateTween?.Kill();

        // Reset rotation
        profileAnim.localRotation = Quaternion.identity;

        // Rotate continuously on Z axis
        rotateTween = profileAnim
            .DORotate(new Vector3(0, 0, 360), 2f, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1);
    }
    void StopTurnAnimation()
    {
        rotateTween?.Kill();

        profileAnim.localRotation = Quaternion.identity;
        profileAnim.gameObject.SetActive(false);
    }


    public void RefreshHand(bool showFront)
    {
        Debug.Log($"Refreshing {playerInstance.PlayerName}'s hand. Count: {playerInstance.PlayerHand.Cards.Count}, Show Front: {showFront}", gameObject);
        ClearVisualCards();

        if (cardData == null)
        {
            Debug.LogError("UIPlayer cardData is missing", gameObject);
            return;
        }

        int index = 0;

        foreach (Card card in playerInstance.PlayerHand.Cards)
        {
            GameObject newCard = Instantiate(cardPrefab, cardHolder);

            int cardCount = playerInstance.PlayerHand.Cards.Count;

            float maxWidth = 7f;          // total width allowed for hand
            float minSpacing = 0.35f;     // minimum spacing (never go below this)
            float defaultSpacing = 0.8f;  // normal spacing

            float spacing = defaultSpacing;

            if (cardCount > 1)
            {
                float calculatedSpacing = maxWidth / (cardCount - 1);
                spacing = Mathf.Clamp(calculatedSpacing, minSpacing, defaultSpacing);
            }

            float startOffset = -((cardCount - 1) * spacing) / 2f;

            newCard.transform.localPosition =
                new Vector3(startOffset + index * spacing, 0, 0);

            UICard uiCard = newCard.GetComponent<UICard>();

            uiCard.cardData = cardData;

            uiCard.SetCard(
                card.Rank,
                uiCard.cardData.GetSuitSprite(card.Suit),
                uiCard.cardData.SuitColors[(int)card.Suit],
                this,
                index
            );  // Higher sorting order for later cards

            // uiCard.ShowFront(showFront);

            spawnedCards.Add(newCard);
            index++;
        }
    }

    private void ClearVisualCards()
    {
        foreach (GameObject card in spawnedCards)
        {
            Destroy(card);
        }

        spawnedCards.Clear();
    }
    private void OnDestroy()
    {
        if (playerInstance != null)
            playerInstance.OnTurnChanged -= HandleTurnChanged;

        rotateTween?.Kill();
    }

    public void ShowTargetPopup()
    {
        Debug.Log(playerInstance.PlayerName + " showing TARGET popup");

        if (popup2Text != null)
            popup2Text.text = "";   // ⭐ clear old text

        if (popup2 != null)
            popup2.SetActive(true);
    }

    public void HideTargetPopup()
    {
        Debug.Log("Hiding TARGET popup for ------ ", gameObject);
        //  Debug.Log("Hiding TARGET popup for " + playerInstance.PlayerName);
        if (popup2 != null)
            popup2.SetActive(false);
    }

    public int GetPlayerID()
    {
        return playerInstance.PlayerID;
    }

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
        Debug.Log("UIPlayer canInteract  " + canInteract);
        if (!canInteract)
            return;

        Vector2 worldPos = Camera.main.ScreenToWorldPoint(screenPos);

        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

        if (hit.collider != null && hit.collider.gameObject == gameObject)
        {
            Debug.Log("Player Clicked");

            OnTargetClicked?.Invoke(this, playerInstance.PlayerID);
        }
    }

    public void OnCardSelected(int rank, UICard clickedCard)
    {
        // Remove previous highlight
        if (selectedCard != null)
            selectedCard.transform.localScale = Vector3.one;

        selectedCard = clickedCard;
        // Simple highlight effect
        selectedCard.transform.localScale = Vector3.one * 1.2f;
        OnRankSelected?.Invoke(this, rank);

    }
    public void ClearCardSelection()
    {
        selectedCard = null;
    }

    public void ShowAskPopup(string targetName, int rank)
    {
        if (popup1 != null)
            popup1.SetActive(true);

        if (popup1Text != null)
            popup1Text.text = targetName + ", do you have any " + rank + "s?";
    }
    public void ShowReplyPopup(bool hasCards, int count, int rank)
    {
        if (popup2Text != null)
        {
            if (hasCards)
                popup2Text.text = "Yes, I have " + count + " " + rank + "s.";
            else
                popup2Text.text = "Go Fish!";
        }

        if (popup2 != null)
            popup2.SetActive(true);
    }

    // ⭐ show lucky draw message when player draws the requested rank
    Coroutine luckyRoutine;

    // ⭐ show lucky draw message when player draws the requested rank
    public void ShowLuckyDrawPopup(int rank)
    {
        if (popup1 == null || popup1Text == null)
            return;

        popup1.SetActive(true);
        popup1Text.text = "Lucky! I drew a " + rank + "!";

        // ⭐ stop previous popup coroutine
        if (luckyRoutine != null)
            StopCoroutine(luckyRoutine);

        luckyRoutine = StartCoroutine(HideLuckyPopup());
    }

    IEnumerator HideLuckyPopup()
    {
        yield return new WaitForSeconds(2f);
        if (popup1 != null)
            popup1.SetActive(false);
        luckyRoutine = null;
    }
    public void ShowSeat(bool show)
    {
        gameObject.SetActive(show);
    }
}
