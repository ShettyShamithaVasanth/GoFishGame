# Fix Plan: Online Multiplayer Card Overlap in Remote Player Hands

## Root Cause

Two bugs combine to cause card overlap in online mode:

### Bug 1: `SetSortingOrder` is SKIPPED for remote player cards (`UICard.cs:55-60`)

In online mode, remote player hands are filled with dummy cards `new Card(0, CardSuit.Spade)` (rank=0) by `ApplyPublicState()` (`GameManager.cs:1675`).

In `UICard.SetCard()` (line 55-60):
```csharp
if (rank <= 0)
{
    rankTop.text = "";
    rankBottom.text = "";
    suitRenderer.enabled = false;
    return;  // ← RETURNS BEFORE SetSortingOrder!
}
// ...
SetSortingOrder(sortingOrder);  // ← NEVER REACHED for rank=0 cards
```

All remote player cards get sorting order **0** (default). With identical Z-order, Unity renders them in arbitrary order — cards overlap each other instead of rendering in sequence.

In offline mode, AI players have real cards (rank > 0) so `SetSortingOrder` IS called — no overlap.

### Bug 2: `ShowFront` is commented out in `RefreshHand` (`UIPlayer.cs:202`)

```csharp
// uiCard.ShowFront(showFront);
```

Without this, ALL cards display in the prefab's default state (likely all front or all back). In online mode, this means remote player back-cards might show as face-up blank cards, contributing to visual confusion.

### Bug 3: `Destroy` is deferred — ghost cards during rapid refresh (`UIPlayer.cs:211`)

`ClearVisualCards()` uses `Destroy()` which is deferred to end-of-frame. If `RefreshAllHands()` runs twice in quick succession (e.g., `ApplyPublicState` + `ApplyPrivateHand` arriving same frame), new cards are created while old ones still exist visually.

## Fix 1 — Move `SetSortingOrder` before rank check (`UICard.cs`)

### File: `Assets/Scripts/UICard.cs`
### Location: `SetCard()` method, lines 45-71

BEFORE:
```csharp
public void SetCard(int rank, Sprite suitSprite, Color suitColor, ICardOwner ownerPlayer, int sortingOrder = 0)
{
    if (cardData == null)
    {
        Debug.LogError("CardData not assigned to UICard!.......", gameObject);
    }
    cardRank = rank;
    owner = ownerPlayer;
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
    SetSortingOrder(sortingOrder);
}
```

AFTER:
```csharp
public void SetCard(int rank, Sprite suitSprite, Color suitColor, ICardOwner ownerPlayer, int sortingOrder = 0)
{
    if (cardData == null)
    {
        Debug.LogError("CardData not assigned to UICard!.......", gameObject);
    }
    cardRank = rank;
    owner = ownerPlayer;
    SetSortingOrder(sortingOrder);

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
```

Change: `SetSortingOrder(sortingOrder)` moved BEFORE the `rank <= 0` check, and removed from the bottom (now only called once at the top). This ensures ALL cards — even dummy rank=0 cards for remote players — get proper sorting orders.

## Fix 2 — Uncomment `ShowFront` in `RefreshHand` (`UIPlayer.cs`)

### File: `Assets/Scripts/UIPlayer.cs`
### Location: `RefreshHand()` method, line 202

BEFORE:
```csharp
            );  // Higher sorting order for later cards

            // uiCard.ShowFront(showFront);

            spawnedCards.Add(newCard);
```

AFTER:
```csharp
            );  // Higher sorting order for later cards

            uiCard.ShowFront(showFront);

            spawnedCards.Add(newCard);
```

Change: Uncomment `ShowFront(showFront)`. This ensures:
- Human player cards → `showFront = true` → face up
- Remote/AI player cards → `showFront = false` → face down (card back visible)

This works correctly for both offline AND online modes.

## Fix 3 — Immediately deactivate cards in `ClearVisualCards` (`UIPlayer.cs`)

### File: `Assets/Scripts/UIPlayer.cs`
### Location: `ClearVisualCards()` method, lines 209-218

BEFORE:
```csharp
    private void ClearVisualCards()
    {
        foreach (GameObject card in spawnedCards)
        {
            Destroy(card);
        }

        spawnedCards.Clear();
    }
```

AFTER:
```csharp
    private void ClearVisualCards()
    {
        foreach (GameObject card in spawnedCards)
        {
            card.SetActive(false);
            Destroy(card);
        }

        spawnedCards.Clear();
    }
```

Change: Add `card.SetActive(false)` before `Destroy(card)`. `Destroy` is deferred to end-of-frame, but `SetActive(false)` takes effect immediately. This prevents ghost cards from being visible when `RefreshAllHands()` runs twice in quick succession (e.g., `ApplyPublicState` + `ApplyPrivateHand` arriving same frame in online mode).

## Summary

| File | Change | Lines |
|------|--------|-------|
| `UICard.cs` | Move `SetSortingOrder` before rank check | 2 lines moved |
| `UIPlayer.cs` | Uncomment `ShowFront(showFront)` | 1 line uncommented |
| `UIPlayer.cs` | Add `SetActive(false)` before `Destroy` | 1 line added |

## Impact

| Area | Affected? |
|------|-----------|
| Offline gameplay | No change (already worked, fixes are consistent) |
| Online remote player cards | **FIXED** — proper sorting, no overlap |
| Online local player cards | **FIXED** — proper front/back display |
| Lobby / Friends | No change |
| MVC structure | No change |
| Host vs Client | Same behavior for both |

## Why Offline Didn't Have This Issue

- Offline AI players have real cards (rank > 0) from `Deck.GetCard()` → `SetSortingOrder` IS called → proper Z-ordering
- Offline `RefreshHand` isn't called in rapid succession like online state sync RPCs
- `ShowFront` being commented out didn't matter because the prefab default happened to look acceptable
