using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using DG.Tweening;
using System.Collections;

public class GameManager : MonoBehaviour, ICardOwner
{
    [Header("Card Data")]
    public CardData cardData;
    [Header("UI Players")]
    public UIPlayer[] uiPlayers; // Size = 4

    [Header("Deck Visual")]
    public Transform deckPosition;      // empty object in center
    public GameObject cardBackPrefab;   // card prefab for deck stack
    private List<GameObject> deckVisualCards = new List<GameObject>();
    private Deck deck;
    private UIPlayer currentTargetUI;

    private int[] turnOrder = { 1, 3, 0, 2 };
    private int turnIndex = 0;
    public Player[] players; // size 4
    private int currentPlayer = 1;   // Tracks whose turn
    private int selectedRank = -1;
    // ⭐ remembers the rank that was asked before Go Fish
    private int lastAskedRank = -1;

    private bool waitingForTarget = false;
    private bool waitingForDeckClick = false;
    private bool turnActionRunning = false;
    private bool waitingForNextTurnButton = false;

    // ⭐ tracks asked rank per target in current turn
    private HashSet<string> askedRankTargetThisTurn = new HashSet<string>();

    public GameOverUI gameOverUI;
    private bool gameOver = false;
    // ⭐ tracks if the last deck card was drawn
    private bool lastDeckCardDrawn = false;

    void Start()
    {
        SetupGame();
    }

    public void OnCardSelected(int rank, UICard card)
    {
        if (!waitingForDeckClick)
            return;

        Debug.Log("Top deck card clicked");
        // RemoveTopDeckVisual();
        OnDeckClicked();
    }

    void SetupGame()
    {
        // 1️⃣ Create Deck
        deck = new Deck(this);
        deck.Shuffle();
        // 2️⃣ Create Players Array
        players = new Player[4];

        int count = ModeSelectionController.selectedPlayers;

        if (count == 2)
        {
            turnOrder = new int[] { 1, 0 }; // AI Top → Human
        }

        else if (count == 3)
        {
            turnOrder = new int[] { 2, 0, 3 }; // AI Left → Human → AI Right
        }

        else
        {
            turnOrder = new int[] { 1, 3, 0, 2 }; // original 4 player order
        }
        players[0] = new Player(0, "You", true);
        players[1] = new Player(1, "AI Top", false);
        players[2] = new Player(2, "AI Left", false);
        players[3] = new Player(3, "AI Right", false);


        // 3️⃣ Deal 7 Cards Each
        for (int i = 0; i < 7; i++)
        {
            for (int j = 0; j < players.Length; j++)
            {
                players[j].AddCard(deck.GetCard());
            }
        }
        // 4️⃣ Connect UI
        for (int i = 0; i < uiPlayers.Length; i++)
        {
            uiPlayers[i].Initialize(players[i]);
        }
        // Show hands based on IsHumans
        for (int i = 0; i < players.Length; i++)
        {
            bool showFront = players[i].IsHuman;
            uiPlayers[i].RefreshHand(showFront);
        }
        // 6️⃣ Show Deck Stack
        CreateDeckVisual();
        // 7️⃣ Start First Turn
        currentPlayer = turnOrder[turnIndex];
        StartCurrentTurn();
        foreach (UIPlayer ui in uiPlayers)
        {
            ui.canInteract = true;
        }
    }

    void CreateDeckVisual()
    {
        deckVisualCards.Clear();

        int totalDeckCards = deck.CardCount();

        for (int i = 0; i < totalDeckCards; i++)
        {
            GameObject cardObj = Instantiate(cardBackPrefab, deckPosition);
            DOVirtual.DelayedCall(0.5f, () =>
            {

                cardObj.SetActive(true);
            }); // Staggered delay for visual effect

            cardObj.transform.localPosition =
                new Vector3(0, i * 0.06f, 0);

            UICard uiCard = cardObj.GetComponent<UICard>();

            uiCard.cardData = cardData;

            Card deckCard = deck.GetAllCards()[i];

            uiCard.SetCard(
                deckCard.Rank,
                cardData.GetSuitSprite(deckCard.Suit),
                cardData.SuitColors[(int)deckCard.Suit],
                this,
                i
            );

            uiCard.ShowFront(false); // show back only
            Collider2D col = cardObj.GetComponent<Collider2D>();
            if (col != null)
            {
                col.enabled = (i == totalDeckCards - 1);
            }
            deckVisualCards.Add(cardObj);
        }
    }

    void StartCurrentTurn()
    {
        if (gameOver)
            return;

        // ⭐ clear all target popups from previous turn
        foreach (UIPlayer ui in uiPlayers)
        {
            ui.HideTargetPopup();
        }
        players[currentPlayer].StartTurn();

        Debug.Log("Turn: " + players[currentPlayer].PlayerName);

        if (!players[currentPlayer].IsHuman)
        {
            // AI Turn → select random target
            Invoke(nameof(AISelectRandomTarget), 2.2f);
        }
    }
    public void EndTurn()
    {
        if (gameOver)
            return;

        // ⭐ if deck empty → end game immediately
        if (deck.CardCount() == 0)
        {
            Debug.Log("Deck empty. Game ends before next turn.");
            TriggerGameOver();
            return;
        }
        // Hide any remaining target popup
        if (currentTargetUI != null)
        {
            currentTargetUI.HideTargetPopup();
            currentTargetUI = null;
        }
        // End current
        Debug.Log($"{players[currentPlayer] == null} {currentPlayer}");
        players[currentPlayer].EndTurn();
        askedRankTargetThisTurn.Clear();
        // Move to next in custom order
        turnIndex++;

        if (turnIndex >= turnOrder.Length)
            turnIndex = 0;

        currentPlayer = turnOrder[turnIndex];
        // Start next
        waitingForNextTurnButton = true;
        Debug.Log("Turn ended. Waiting for NextTurn button.");
    }

    public void StartNextTurnFromButton()
    {
        if (!waitingForNextTurnButton)
        {
            Debug.Log("NextTurn button pressed but turn not finished yet.");
            return;
        }

        waitingForNextTurnButton = false;

        Debug.Log("NextTurn button confirmed. Starting next turn.");

        StartCurrentTurn();
    }
    public Player GetCurrentPlayer()
    {
        return players[currentPlayer];
    }

    void AISelectRandomTarget()
    {
        if (gameOver)
            return;

        // ⭐ stop AI actions if deck empty
        if (deck.CardCount() == 0)
        {
            TriggerGameOver();
            return;
        }

        // ⭐ clear old target popups
        foreach (UIPlayer ui in uiPlayers)
        {
            ui.HideTargetPopup();
        }
        if (turnActionRunning)
            return;
        Player aiPlayer = players[currentPlayer];
        // pick random card from AI hand
        List<Card> aiCards = aiPlayer.PlayerHand.Cards;
        // ⭐ find a valid rank that still exists in hand
        int attempts = 0;
        do
        {
            int randomIndex = Random.Range(0, aiCards.Count);
            selectedRank = aiCards[randomIndex].Rank;
            attempts++;
        }
        while (!PlayerHasRankInHand(currentPlayer, selectedRank) && attempts < 10);
        // ⭐ safety check (AI should not ask rank it doesn't have)
        if (!PlayerHasRankInHand(currentPlayer, selectedRank))
        {
            Invoke(nameof(AISelectRandomTarget), 2.2f);
            return;
        }

        Debug.Log(aiPlayer.PlayerName + " asking for rank " + selectedRank);
        List<int> possibleTargets = new List<int>();
        int count = ModeSelectionController.selectedPlayers;

        for (int i = 0; i < players.Length; i++)
        {
            if (i != currentPlayer)
            {
                if (count == 2 && (i == 2 || i == 3)) continue;
                if (count == 3 && i == 1) continue;

                possibleTargets.Add(i);
            }
        }

        int randomTarget = -1;
        int attemptsTarget = 0;

        do
        {
            randomTarget = possibleTargets[Random.Range(0, possibleTargets.Count)];
            attemptsTarget++;

        }
        while (AlreadyAskedThisTurn(randomTarget, selectedRank) && attemptsTarget < 10);

        // ⭐ if all targets were already asked for this rank, pick another rank
        if (AlreadyAskedThisTurn(randomTarget, selectedRank))
        {
            Invoke(nameof(AISelectRandomTarget), 2.2f);
            return;
        }
        Debug.Log("AI asking " + players[randomTarget].PlayerName +
          " for rank " + selectedRank);
        currentTargetUI = uiPlayers[randomTarget];
        uiPlayers[currentPlayer].ShowAskPopup(players[randomTarget].PlayerName, selectedRank);

        Debug.Log(players[currentPlayer].PlayerName +
                  " is asking " + players[randomTarget].PlayerName);

        // currentTargetUI.ShowTargetPopup();
        turnActionRunning = true;
        // Simulate ask resolve after 2 sec
        Invoke(nameof(ResolveAsk), 2f);
    }

    void ResolveAsk()
    {
        if (gameOver)
            return;
        Player askingPlayer = players[currentPlayer];
        Player targetPlayer = players[currentTargetUI.GetPlayerID()];

        // ⭐ record that this rank was asked to this target
        string askKey = currentTargetUI.GetPlayerID() + "_" + selectedRank;
        askedRankTargetThisTurn.Add(askKey);

        Debug.Log(askingPlayer.PlayerName + " asking for rank " + selectedRank);

        if (targetPlayer.HasRank(selectedRank))
        {
            int count = targetPlayer.PlayerHand.Cards.FindAll(c => c.Rank == selectedRank).Count;
            currentTargetUI.ShowReplyPopup(true, count, selectedRank);
            Debug.Log("SUCCESS! Cards transferred.");

            List<Card> cardsToTransfer = targetPlayer.RemoveCardsByRank(selectedRank);

            StartCoroutine(DelayedTransfer(currentTargetUI.GetPlayerID(),
            currentPlayer, cardsToTransfer));
            // Invoke(nameof(AISelectRandomTarget), 2.2f);
            // Continue turn (do not end)
            Debug.Log("Player continues turn.");
        }
        else
        {
            Debug.Log("GO FISH!");
            // ⭐ remember the asked rank before drawing
            lastAskedRank = selectedRank;
            currentTargetUI.ShowReplyPopup(false, 0, selectedRank);
            // If target is human → wait for GoFish button
            if (targetPlayer.IsHuman)
            {
                Debug.Log("Waiting for human to click GO FISH button");

                // ⭐ enable Go Fish button
                waitingForDeckClick = true;

                return;
            }

            // AI target → auto draw
            Card drawn = deck.GetCard();

            if (drawn == null)
            {
                TriggerGameOver();
                return;
            }

            RemoveTopDeckVisual();
            StartCoroutine(DelayedDraw(drawn));
        }

        if (currentTargetUI != null)
        {
            StartCoroutine(HidePopupAfterDelay(currentTargetUI));
        }

        currentTargetUI = null;
        selectedRank = -1;
        turnActionRunning = false;
    }

    IEnumerator HidePopupAfterDelay(UIPlayer target)
    {
        yield return new WaitForSeconds(1.5f);

        if (target != null)
            target.HideTargetPopup();
    }

    public void HumanSelectTarget(int targetID)
    {
        // ⭐ clear old target popups
        foreach (UIPlayer ui in uiPlayers)
        {
            ui.HideTargetPopup();
        }
        if (turnActionRunning)
            return;
        if (!players[currentPlayer].IsHuman)
            return;

        if (!waitingForTarget)
        {
            Debug.Log("Select a card first!");
            return;
        }

        if (targetID == currentPlayer)
            return;

        Player askingPlayer = players[currentPlayer];
        Player targetPlayer = players[targetID];

        // ⭐ prevent asking rank if player no longer has it (book already formed)
        if (!PlayerHasRankInHand(currentPlayer, selectedRank))
        {
            Debug.Log("Cannot ask for this rank. You no longer have it in hand.");
            return;
        }

        // ⭐ prevent asking same rank to same player in same turn
        string key = targetID + "_" + selectedRank;

        if (AlreadyAskedThisTurn(targetID, selectedRank))
        {
            Debug.Log("You already asked this player for this rank this turn.");
            return;
        }

        currentTargetUI = uiPlayers[targetID];
        askedRankTargetThisTurn.Add(key);

        uiPlayers[currentPlayer].ShowAskPopup(players[targetID].PlayerName, selectedRank);
        currentTargetUI.ShowTargetPopup();
        turnActionRunning = true;

        Debug.Log("Human asking for rank: " + selectedRank);

        // CASE 1 — SUCCESS
        if (targetPlayer.HasRank(selectedRank))
        {
            Debug.Log("SUCCESS! Target has the rank.");

            List<Card> transferredCards = targetPlayer.RemoveCardsByRank(selectedRank);
            // ⭐ SHOW REPLY TEXT
            currentTargetUI.ShowReplyPopup(true, transferredCards.Count, selectedRank);

            StartCoroutine(DelayedTransfer(targetID, currentPlayer, transferredCards));
            RefreshAllHands();

            StartCoroutine(HidePopupAfterDelay(currentTargetUI));
            currentTargetUI = null;

            waitingForTarget = false;
            selectedRank = -1;

            Debug.Log("Human continues turn.");
            return; // DO NOT END TURN
        }

        // CASE 2 — GO FISH
        Debug.Log("GO FISH!");
        // ⭐ remember asked rank before deck draw
        lastAskedRank = selectedRank;

        currentTargetUI.ShowReplyPopup(false, 0, selectedRank);

        waitingForDeckClick = true;
        waitingForTarget = false;
    }

    public void SetSelectedRank(int rank)
    {
        selectedRank = rank;
        waitingForTarget = true;

        Debug.Log("Selected Rank: " + rank);
        Debug.Log("Now select a target player");

    }

    void RefreshAllHands()
    {
        for (int i = 0; i < players.Length; i++)
        {
            bool showFront = players[i].IsHuman;
            uiPlayers[i].RefreshHand(showFront);
        }
    }
    // ⭐ check if player still has the rank in hand
    bool PlayerHasRankInHand(int playerID, int rank)
    {
        foreach (Card c in players[playerID].PlayerHand.Cards)
        {
            if (c.Rank == rank)
                return true;
        }

        return false;
    }
    // ⭐ check if this player already asked this target for this rank this turn
    bool AlreadyAskedThisTurn(int targetID, int rank)
    {
        string key = targetID + "_" + rank;
        return askedRankTargetThisTurn.Contains(key);
    }

    // ⭐ refill player hand if empty after forming a book
    IEnumerator RefillHandIfEmpty(int playerID)
    {
        Player player = players[playerID];

        if (player.PlayerHand.Cards.Count > 0)
            yield break;

        Debug.Log(player.PlayerName + " has no cards. Drawing new cards.");

        int drawCount = Mathf.Min(5, deck.CardCount());

        for (int i = 0; i < drawCount; i++)
        {
            Card drawn = deck.GetCard();

            if (drawn == null)
                yield break;

            RemoveTopDeckVisual();

            yield return StartCoroutine(
                AnimateCardMove(deckPosition, uiPlayers[playerID].transform, drawn)
            );

            player.AddCard(drawn);
        }

        RefreshAllHands();
    }

    public void OnDeckClicked()
    {
        if (gameOver)
            return;
        if (!waitingForDeckClick)
            return;

        Card drawn = deck.GetCard();

        if (drawn == null)
        {
            TriggerGameOver();
            return;
        }

        waitingForDeckClick = false;

        RemoveTopDeckVisual();

        StartCoroutine(DrawCardWithAnimation(drawn));
    }

    void RemoveTopDeckVisual()
    {
        if (deckVisualCards.Count == 0)
            return;

        GameObject topCard = deckVisualCards[deckVisualCards.Count - 1];

        deckVisualCards.RemoveAt(deckVisualCards.Count - 1);

        Destroy(topCard);

        // 👇 ENABLE NEW TOP CARD
        if (deckVisualCards.Count > 0)
        {
            GameObject newTop = deckVisualCards[deckVisualCards.Count - 1];

            Collider2D col = newTop.GetComponent<Collider2D>();
            if (col != null)
                col.enabled = true;
        }
    }
    IEnumerator AnimateCardMove(Transform from, Transform to, Card card)
    {
        GameObject flyingCard = Instantiate(uiPlayers[currentPlayer].cardPrefab, from.position, Quaternion.identity);
        // ⭐ TEMPORARY: bring card above UI during animation
        SortingGroup sg = flyingCard.GetComponent<SortingGroup>();
        if (sg != null)
        {
            sg.sortingLayerName = "UI_Overlay";
            sg.sortingOrder = 500;
        }
        Collider2D col = flyingCard.GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;
        UICard uiCard = flyingCard.GetComponent<UICard>();
        uiCard.cardData = cardData;
        uiCard.SetCard(
            card.Rank,
            cardData.GetSuitSprite(card.Suit), cardData.SuitColors[(int)card.Suit],
            this, 999);

        bool comingFromDeck = (from == deckPosition);
        bool receivingHuman = players[currentPlayer].IsHuman;

        if (comingFromDeck)
        {
            // Deck → Player
            uiCard.ShowFront(receivingHuman);
        }
        else
        {
            // Target Player → Player
            uiCard.ShowFront(true);
        }
        flyingCard.transform.localScale = Vector3.one * 0.7f;

        Vector3 midPoint = (from.position + to.position) / 2 + Vector3.up * 1.5f;

        Sequence seq = DOTween.Sequence();

        seq.Append(flyingCard.transform.DOMove(midPoint, 0.3f).SetEase(Ease.OutQuad));
        seq.Append(flyingCard.transform.DOMove(to.position, 0.3f).SetEase(Ease.InQuad));

        seq.Join(flyingCard.transform.DOScale(1f, 0.6f));

        yield return seq.WaitForCompletion();
        // 🟢 Landing Bounce Effect
        yield return flyingCard.transform.DOPunchScale(Vector3.one * 0.15f, 0.25f, 6, 0.8f).WaitForCompletion();
        // restore sorting layer before destroy
        if (sg != null)
        {
            sg.sortingLayerName = "Cards";
            sg.sortingOrder = 0;
        }

        Destroy(flyingCard);
    }

    IEnumerator TransferCardsWithAnimation(int fromID, int toID, List<Card> cards)
    {
        if (gameOver)
            yield break;
        Transform fromTransform = uiPlayers[fromID].transform;
        Transform toTransform = uiPlayers[toID].transform;

        foreach (Card c in cards)
        {
            yield return StartCoroutine(
                AnimateCardMove(fromTransform, toTransform, c)
            );

            players[toID].AddCard(c);
        }

        // wait small moment so user sees animation clearly
        yield return new WaitForSeconds(0.25f);

        RefreshAllHands();
        CheckForBook(toID);

        turnActionRunning = false;

        // ⭐ continue asking only AFTER book logic finishes
        if (!players[toID].IsHuman && !gameOver)
        {
            Invoke(nameof(AISelectRandomTarget), 2.2f);
        }
    }

    IEnumerator DrawCardWithAnimation(Card drawn)
    {
        if (gameOver)
            yield break;
        Transform fromTransform = deckPosition;
        Transform toTransform = uiPlayers[currentPlayer].transform;
        yield return StartCoroutine(
            AnimateCardMove(fromTransform, toTransform, drawn)
        );

        // ✅ AFTER animation finishes
        players[currentPlayer].AddCard(drawn);
        RefreshAllHands();

        // ⭐ check if drawing this card created a BOOK
        CheckForBook(currentPlayer);

        // ⭐ check if deck is now empty AFTER draw
        // ⭐ check if deck is now empty AFTER draw
        if (deck.CardCount() == 0)
        {
            Debug.Log("Last deck card drawn.");

            lastDeckCardDrawn = true;

            // ⭐ check if book formed from this draw
            bool bookExists = false;

            Dictionary<int, int> rankCount = new Dictionary<int, int>();

            foreach (Card c in players[currentPlayer].PlayerHand.Cards)
            {
                if (!rankCount.ContainsKey(c.Rank))
                    rankCount[c.Rank] = 0;

                rankCount[c.Rank]++;

                if (rankCount[c.Rank] == 4)
                {
                    bookExists = true;
                    break;
                }
            }

            // ⭐ if NO book → end game immediately
            if (!bookExists)
            {
                Debug.Log("Deck empty and no book possible. Ending game immediately.");

                StartCoroutine(EndGameAfterDelay());
                yield break;
            }
        }

        Debug.Log("Drew card of rank: " + drawn.Rank);

        // ✅ NOW decide turn
        if (drawn.Rank == lastAskedRank)
        {
            Debug.Log("Drawn card matches! Continue turn.");

            // ⭐ show lucky popup message
            uiPlayers[currentPlayer].ShowLuckyDrawPopup(drawn.Rank);

            if (!players[currentPlayer].IsHuman)
                Invoke(nameof(AISelectRandomTarget), 2.1f);
        }
        else
        {
            Debug.Log("Drawn card does NOT match. Turn ends.");
            selectedRank = -1;
            EndTurn();
        }
        // ⭐ reset stored rank after draw
        lastAskedRank = -1;
        turnActionRunning = false;

    }

    IEnumerator DelayedTransfer(int fromID, int toID, List<Card> cards)
    {
        // wait so popup text can be read
        yield return new WaitForSeconds(1.3f);

        yield return StartCoroutine(
            TransferCardsWithAnimation(fromID, toID, cards)
        );
    }

    IEnumerator DelayedDraw(Card drawn)
    {
        yield return new WaitForSeconds(1.3f);

        yield return StartCoroutine(
            DrawCardWithAnimation(drawn)
        );
    }
    // ⭐ handles final game end after last deck card
    IEnumerator EndGameAfterDelay()
    {
        yield return new WaitForSeconds(1.2f);

        TriggerGameOver();
    }

    void CheckForBook(int playerID)
    {
        Player player = players[playerID];

        Dictionary<int, List<Card>> rankGroups = new Dictionary<int, List<Card>>();

        foreach (Card card in player.PlayerHand.Cards)
        {
            if (!rankGroups.ContainsKey(card.Rank))
                rankGroups[card.Rank] = new List<Card>();

            rankGroups[card.Rank].Add(card);
        }

        foreach (var group in rankGroups)
        {
            if (group.Value.Count == 4)
            {
                Debug.Log(player.PlayerName + " completed a BOOK of rank " + group.Key);

                // ⭐ prevent asking this rank again
                if (selectedRank == group.Key)
                    selectedRank = -1;

                StartCoroutine(HandleBook(playerID, group.Key, group.Value));

                // ⭐ if deck already empty when book formed → end game
                if (deck.CardCount() == 0)
                {
                    Debug.Log("Book formed with last deck card. Ending game.");
                    StartCoroutine(EndGameAfterDelay());
                }
                break;
            }
        }
    }

    IEnumerator HandleBook(int playerID, int rank, List<Card> cards)
    {
        turnActionRunning = true;
        Player player = players[playerID];

        Transform fromTransform = uiPlayers[playerID].transform;

        // choose book display position (right side of player)
        Vector3 bookPosition = fromTransform.position + Vector3.right * 2f;

        foreach (Card c in cards)
        {
            yield return StartCoroutine(
                AnimateCardMove(fromTransform, fromTransform, c)
            );

            player.PlayerHand.RemoveCard(c);
        }

        player.AddPoint();
        uiPlayers[playerID].UpdateScore(player.Score);
        Debug.Log("Score of " + player.PlayerName + ": " + player.Score);

        RefreshAllHands();
        // ⭐ check if player has no cards after book
        yield return StartCoroutine(RefillHandIfEmpty(playerID));

        turnActionRunning = false;
        selectedRank = -1; // ⭐ reset asking rank when book forms

        // ⭐ if the last deck card caused this book, end the game now
        if (lastDeckCardDrawn)
        {
            Debug.Log("Last deck card completed a book. Ending game.");
            StartCoroutine(EndGameAfterDelay());
            lastDeckCardDrawn = false;
        }
    }

    void TriggerGameOver()
    {
        if (gameOver)
            return;

        gameOver = true;

        Debug.Log("GAME OVER TRIGGERED");

        CancelInvoke();       // stop AI invokes
        StopAllCoroutines();  // ⭐ stop animations immediately

        waitingForDeckClick = false;
        waitingForTarget = false;
        turnActionRunning = true;

        foreach (UIPlayer ui in uiPlayers)
        {
            ui.canInteract = false;
        }
        gameOverUI.ShowGameOver(players);
    }

    public void OnGoFishButtonClicked()
    {
        // ⭐ Go Fish button should only work when waiting for deck click
        if (!waitingForDeckClick)
        {
            Debug.Log("Go Fish button pressed but not allowed right now.");
            return;
        }

        if (gameOver)
            return;

        Debug.Log("Human pressed GO FISH");

        Card drawn = deck.GetCard();

        if (drawn == null)
        {
            TriggerGameOver();
            return;
        }

        waitingForDeckClick = false;

        RemoveTopDeckVisual();

        StartCoroutine(DrawCardWithAnimation(drawn));
    }
}