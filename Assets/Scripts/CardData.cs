using UnityEngine;

[CreateAssetMenu(fileName = "CardData", menuName = "GoFish/CardData", order = 1)]
public class CardData : ScriptableObject
{

    [Header("Suit Sprites (Spade, Heart, Diamond, Club)")]
    [SerializeField] private Sprite[] suitSprites;

    [Header("Suit Colors (Spade, Heart, Diamond, Club)")]
    [SerializeField] private Color[] suitColors;

    private static readonly string[] rankStrings =
    {
        "A","2","3","4","5","6","7","8","9","10","J","Q","K"
    };

    public Sprite[] SuitSprites => suitSprites;
    public Color[] SuitColors => suitColors;

    public string GetRankString(int rank)
    {
        switch (rank)
        {
            case 1: return "A";
            case 11: return "J";
            case 12: return "Q";
            case 13: return "K";
            default: return rank.ToString();
        }
    }


    public Sprite GetSuitSprite(CardSuit suit)
    {
        int suitIndex = (int)suit;
        if (suitSprites.Length < 4)
        {
            Debug.LogError("Suit sprites not assigned properly!");
            return null;
        }
        return suitSprites[suitIndex];
    }



}
