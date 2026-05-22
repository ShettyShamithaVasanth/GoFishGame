using UnityEngine;
using TMPro;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;
public enum CardSuit
{
    Spade,
    Heart,
    Diamond,
    Club
}

public class UICard : MonoBehaviour
{
    [Header("Sprite Renderers")]
    [SerializeField] private SpriteRenderer cardColor;
    [SerializeField] private SpriteRenderer suitRenderer;

    [Header("Rank Texts")]
    [SerializeField] private TextMeshPro rankTop;
    [SerializeField] private TextMeshPro rankBottom;

    [SerializeField] private GameObject cardFront;
    [SerializeField] private GameObject cardBack;
    [SerializeField] private SortingGroup cardSortingGroup;

    public CardData cardData;
    private int cardRank;
    private ICardOwner owner;

    void Awake()
    {
        cardSortingGroup = GetComponent<SortingGroup>();
    }

    public void SetSortingOrder(int order)
    {
        if (cardSortingGroup != null)
        {
            cardSortingGroup.sortingOrder = order;
        }
    }


    public void SetCard(int rank, Sprite suitSprite, Color suitColor, ICardOwner ownerPlayer, int sortingOrder = 0)
    {
        if (cardData == null)
        {
            Debug.LogError("CardData not assigned to UICard!.......", gameObject);
            // return;
        }
        cardRank = rank;
        owner = ownerPlayer;
        SetSortingOrder(sortingOrder);
        // Hidden opponent card
        if (rank <= 0)
        {
            rankTop.text = "";
            rankBottom.text = "";
            suitRenderer.enabled = false;
            return;
        }

        string rankString = cardData.GetRankString(rank);
        rankTop.text = rankString;
        rankBottom.text = rankString;
        suitRenderer.sprite = suitSprite;
        cardColor.color = suitColor;
    }

    public void ShowFront(bool show)
    {
        if (cardFront == null || cardBack == null)
        {
            Debug.LogError("CardFront or CardBack missing!");
            return;
        }

        // FRONT = visible for human
        cardFront.SetActive(show);

        // BACK = visible for AI/opponents
        cardBack.SetActive(!show);
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
        Vector2 worldPos = Camera.main.ScreenToWorldPoint(screenPos);

        Collider2D[] hits = Physics2D.OverlapPointAll(worldPos);

        UICard topCard = null;
        int highestOrder = int.MinValue;

        foreach (Collider2D col in hits)
        {
            UICard card = col.GetComponent<UICard>();

            if (card != null && card.cardSortingGroup != null)
            {
                int order = card.cardSortingGroup.sortingOrder;

                if (order > highestOrder)
                {
                    highestOrder = order;
                    topCard = card;
                }
            }
        }

        // ⭐ only trigger if THIS is the top card
        if (topCard == this)
        {
            Debug.Log("Card Clicked (Correct Top Card)");

            if (owner != null)
            {
                owner.OnCardSelected(cardRank, this);
            }
        }
    }
    // private void Start()
    // {
    //     SetCard(13,cardData.GetSuitSprite(CardSuit.Heart), cardData.SuitColors[(int)CardSuit.Heart]);
    //     ShowFront(true);
    // }
}
