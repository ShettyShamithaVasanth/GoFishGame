using UnityEngine;
using TMPro;
using UnityEngine.Rendering;

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

        string rankString = cardData.GetRankString(rank);

        rankTop.text = rankString;
        rankBottom.text = rankString;

        suitRenderer.sprite = suitSprite;
        cardColor.color = suitColor;
        SetSortingOrder(sortingOrder);
    }

    public void ShowFront(bool show)
    {
        if (cardFront == null || cardBack == null)
        {
            Debug.LogError("CardFront or CardBack not assigned!");
            return;
        }

        cardFront.SetActive(show);
        cardBack.SetActive(!show);
    }

    void OnMouseDown()
    {
        Debug.Log("Card Clicked,owner: " + (owner != null ? owner.ToString() : "None"));
        if (!gameObject.activeInHierarchy)
            return;
        if (owner != null)
        {
            Debug.Log("Owner found");
            owner.OnCardSelected(cardRank, this);
        }
        else
        {
            Debug.Log("Owner is NULL");
        }
    }
    // private void Start()
    // {
    //     SetCard(13,cardData.GetSuitSprite(CardSuit.Heart), cardData.SuitColors[(int)CardSuit.Heart]);
    //     ShowFront(true);
    // }
}
