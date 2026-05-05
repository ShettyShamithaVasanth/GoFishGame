using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using DG.Tweening;
using System.Collections;
using Unity.Netcode;

public struct AIProfile
{
    public int avatarIndex;
    public string name;
}
public class GameManager : MonoBehaviour, ICardOwner
{
    [Header("Card Data")]
    public CardData cardData;
    [Header("UI Players")]
    public UIPlayer[] uiPlayers; // Size = 4
    private AIMemory aiMemory = new AIMemory();
    [Header("Avatar Sprites")]
    public Sprite[] avatarSprites;


    [Header("Deck Visual")]
    public Transform deckPosition;      // empty object in center
    private int maxVisibleDeckCards = 8; // 🔥 LIMIT visual cards
    public GameObject cardBackPrefab;   // card prefab for deck stack
    private List<GameObject> deckVisualCards = new List<GameObject>();
    private Deck deck;
    private UIPlayer currentTargetUI;

    // ⭐ Mode-based player index arrays
    [SerializeField]
    UIPlayer[] mode2Players, mode3Players, mode4Players;

    // ⭐ Active players for current game
    int[] activePlayers;

    // private int[] turnOrder = { 0, 1, 2, 3 };
    // private int turnIndex = 0;
    [HideInInspector]
    public Player[] players; // size 4
    private int currentPlayer = 0;   // Tracks whose turn
    private int selectedRank = -1;
    // ⭐ remembers the rank that was asked before Go Fish
    private int lastAskedRank = -1;
    private int bookOrderCounter = 0;

    private bool waitingForTarget = false;
    private bool waitingForDeckClick = false;
    private bool turnActionRunning = false;
    private bool bookJustFormed = false; // 🔥 detects recent book
    // private bool waitingForNextTurnButton = false;

    // ⭐ tracks asked rank per target in current turn
    private HashSet<string> askedRankTargetThisTurn = new HashSet<string>();
    private HashSet<int> completedRanks = new HashSet<int>();
    // ⭐ Maps local playerID → real network clientId
    private Dictionary<int, ulong> playerIdToClientId = new Dictionary<int, ulong>();

    public GameOverUI gameOverUI;
    private bool gameOver = false;
    private bool lockTargetSelection = false;
    public ToastUI toastUI;
    // 🔥 Player names pool (10 names)
    [SerializeField]
    private AIProfile[] aiProfiles = new AIProfile[]
{
    new AIProfile { name = "Lob", avatarIndex = 0 },
    new AIProfile { name = "Alex", avatarIndex = 1 },
    new AIProfile { name = "Johna", avatarIndex = 2 },
    new AIProfile { name = "Emma", avatarIndex = 3 },
    new AIProfile { name = "Ava", avatarIndex = 4 },
    new AIProfile { name = "Soph", avatarIndex = 5 },
    new AIProfile { name = "Liam", avatarIndex = 6 },
    new AIProfile { name = "Olivi", avatarIndex = 7 },
    new AIProfile { name = "Noah", avatarIndex = 8 },
    new AIProfile { name = "David", avatarIndex = 9 }
};
    // ⭐ tracks if the last deck card was drawn
    // private bool lastDeckCardDrawn = false;

    void Start()
    {
        Debug.Log("GameManager START");

        // 🔥 RESET EVERYTHING
        gameOver = false;
        waitingForDeckClick = false;
        waitingForTarget = false;
        turnActionRunning = false;

        // 🔥 IMPORTANT: hide game over UI if still active
        if (gameOverUI != null && gameOverUI.gameOverPanel != null)
        {
            gameOverUI.gameOverPanel.SetActive(false);
        }
        if (toastUI != null)
        {
            toastUI.HideToast();
        }

        if (!GameModeManager.isOnlineMode)
        {
            // SetupGame();
        }
    }

    public void OnCardSelected(int rank, UICard card)
    {
        if (!waitingForDeckClick)
            return;

        Debug.Log("Top deck card clicked");
        // RemoveTopDeckVisual();
        OnDeckClicked();
    }

    int GetUIIndex(int playerID)
    {
        for (int i = 0; i < activePlayers.Length; i++)
        {
            if (activePlayers[i] == playerID)
                return i;
        }

        Debug.LogError("UI index not found for playerID: " + playerID);
        return 0;
    }
    public void SetupGame()
    {
        Debug.Log("SetupGame CALLED");

        aiMemory = new AIMemory();

        // 1️⃣ Create Deck
        deck = new Deck(this);
        deck.Shuffle();

        // 2️⃣ Create ALL 4 players (fixed positions)
        players = new Player[4];

        // shuffle names (simple random pick)
        List<AIProfile> shuffledProfiles = new List<AIProfile>(aiProfiles);

        for (int i = 0; i < shuffledProfiles.Count; i++)
        {
            AIProfile temp = shuffledProfiles[i];
            int rand = Random.Range(i, shuffledProfiles.Count);
            shuffledProfiles[i] = shuffledProfiles[rand];
            shuffledProfiles[rand] = temp;
        }

        int savedAvatarIndex = ProfileSaveSystem.LoadAvatarIndex();

        players[0] = new Player(0, ProfileData.PlayerName, true, savedAvatarIndex);
        players[1] = new Player(1, shuffledProfiles[1].name, false, shuffledProfiles[1].avatarIndex);
        players[2] = new Player(2, shuffledProfiles[2].name, false, shuffledProfiles[2].avatarIndex);
        players[3] = new Player(3, shuffledProfiles[3].name, false, shuffledProfiles[3].avatarIndex);
        int humanAvatar = savedAvatarIndex;

        foreach (var p in players)
        {
            if (!p.IsHuman && p.AvatarIndex == humanAvatar)
            {
                p.AvatarIndex = (p.AvatarIndex + 1) % avatarSprites.Length;
            }
        }
        // ⭐ DEBUG PRINT — PLAYER AVATAR MAPPING (START)
        Debug.Log("=== GAME START PLAYER AVATAR DATA ===");

        foreach (Player p in players)
        {
            Debug.Log("PlayerID: " + p.PlayerID +
                      " | Name: " + p.PlayerName +
                      " | AvatarIndex: " + p.AvatarIndex +
                      " | IsHuman: " + p.IsHuman);
        }

        Debug.Log("====================================");

        Debug.Log("AI1: " + players[1].PlayerName + " avatar: " + players[1].AvatarIndex);
        Debug.Log("AI2: " + players[2].PlayerName + " avatar: " + players[2].AvatarIndex);
        Debug.Log("AI3: " + players[3].PlayerName + " avatar: " + players[3].AvatarIndex);
        bookOrderCounter = 0;

        foreach (var p in players)
        {
            p.LastBookTurn = int.MaxValue;
        }

        // 3️⃣ Select mode
        int count = ModeSelectionController.selectedPlayers;

        switch (count)
        {
            case 2:
                uiPlayers = mode2Players;
                activePlayers = new int[] { 0, 2 };   // ⭐ ADD THIS
                // turnOrder = new int[] { 0, 2 };
                break;

            case 3:
                uiPlayers = mode3Players;
                activePlayers = new int[] { 0, 1, 3 }; // ⭐ ADD THIS
                // turnOrder = new int[] { 0, 1, 3 };
                break;

            default:
                uiPlayers = mode4Players;
                activePlayers = new int[] { 0, 1, 2, 3 }; // ⭐ ADD THIS
                // turnOrder = new int[] { 0, 1, 2, 3 };
                break;
        }

        // 4️⃣ Deal 7 cards ONLY to active players
        for (int i = 0; i < 7; i++)
        {
            foreach (int id in activePlayers)
            {
                players[id].AddCard(deck.GetCard());
            }
        }

        // 5️⃣ Initialize UI ONLY for active players
        for (int i = 0; i < activePlayers.Length; i++)
        {
            int id = activePlayers[i];
            uiPlayers[i].Initialize(players[id]);
            BindUIPlayer(uiPlayers[i]);
        }

        // 6️⃣ Hide unused UI players
        // for (int i = 0; i < uiPlayers.Length; i++)
        // {
        //     if (!System.Array.Exists(activePlayers, x => x == i))
        //     {
        //         uiPlayers[i].gameObject.SetActive(false);
        //     }
        // }

        // 7️⃣ Refresh hands
        for (int i = 0; i < activePlayers.Length; i++)
        {
            int id = activePlayers[i];
            bool showFront = players[id].IsHuman;
            uiPlayers[i].RefreshHand(showFront);
        }

        // 8️⃣ Deck visual
        if (cardBackPrefab == null || deckPosition == null)
        {
            Debug.LogError("Deck Visual references missing");
        }
        else
        {
            CreateDeckVisual();
        }

        // 9️⃣ Start first turn
        currentPlayer = activePlayers[0];
        StartCurrentTurn();

        // 🔟 Enable interaction
        foreach (UIPlayer ui in uiPlayers)
        {
            ui.canInteract = true;
        }
    }

    private void HandleUITargetClicked(UIPlayer uiPlayer, int targetID)
    {
        if (players == null || activePlayers == null)
            return;

        HumanSelectTarget(targetID);
    }
    private void HandleUIRankSelected(UIPlayer uiPlayer, int rank)
    {
        if (players == null || activePlayers == null)
            return;

        if (!players[currentPlayer].IsHuman)
            return;

        if (uiPlayer.GetPlayerID() != currentPlayer)
            return;

        SetSelectedRank(rank);
    }

    private void BindUIPlayer(UIPlayer uiPlayer)
    {
        uiPlayer.cardData = cardData;
        uiPlayer.OnTargetClicked -= HandleUITargetClicked;
        uiPlayer.OnRankSelected -= HandleUIRankSelected;
        uiPlayer.OnTargetClicked += HandleUITargetClicked;
        uiPlayer.OnRankSelected += HandleUIRankSelected;
    }

    void CreateDeckVisual()
    {
        deckVisualCards.Clear();

        int totalDeckCards = 0;

        if (GameModeManager.isOnlineMode)
        {
            if (NetworkDeckManager.Instance == null)
            {
                Debug.LogError("NetworkDeckManager missing");
                return;
            }
            totalDeckCards = NetworkDeckManager.Instance.deck.Count;
        }
        else
        {
            totalDeckCards = deck.CardCount();
        }

        //only show max 8 cards visually
        int visualCount = Mathf.Min(totalDeckCards, maxVisibleDeckCards);

        for (int i = 0; i < visualCount; i++)
        {
            GameObject cardObj = Instantiate(cardBackPrefab, deckPosition);
            DOVirtual.DelayedCall(0.5f, () =>
            {
                if (cardObj != null) cardObj.SetActive(true);
            }).SetLink(cardObj); // 🔥 VERY IMPORTANT

            cardObj.transform.localPosition =
                new Vector3(0, i * 0.08f, 0); // slightly spaced better
            UICard uiCard = cardObj.GetComponent<UICard>();

            uiCard.cardData = cardData;

            Card deckCard;

            if (GameModeManager.isOnlineMode)
            {
                int value = NetworkDeckManager.Instance.deck[i];
                deckCard = ConvertToCard(value);
            }
            else
            {
                deckCard = deck.GetAllCards()[i];
            }

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
                col.enabled = false; // 🔥 disable ALL deck card colliders
            }
            deckVisualCards.Add(cardObj);
        }
    }

    void StartCurrentTurn()
    {
        if (gameOver)
            return;
        // ⭐ ALWAYS CLEAR PREVIOUS TOAST WHEN NEW TURN STARTS
        if (toastUI != null)
        {
            toastUI.HideToast();
        }

        // ⭐ clear all target popups from previous turn
        foreach (UIPlayer ui in uiPlayers)
        {
            ui.HideTargetPopup();
        }
        players[currentPlayer].StartTurn();
        if (players[currentPlayer].IsHuman)
        {
            selectedRank = -1;
            waitingForTarget = false;

            // ⭐ TOAST MESSAGE
            toastUI.ShowToast("Your turn! Select a rank card");
        }
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
        int currentIndex = System.Array.IndexOf(activePlayers, currentPlayer);

        currentIndex++;

        if (currentIndex >= activePlayers.Length)
            currentIndex = 0;

        currentPlayer = activePlayers[currentIndex];
        // Start next
        // waitingForNextTurnButton = true;
        // Debug.Log("Turn ended. Waiting for NextTurn button.");
        // 🔥 DELAY NEXT TURN IF BOOK JUST FORMED
        if (bookJustFormed)
        {
            Debug.Log("Book formed → delaying next turn");

            bookJustFormed = false; // reset

            Invoke(nameof(StartCurrentTurn), 5f); // 👈 delay (you can tune 1.5–2.0)
        }
        else
        {
            Invoke(nameof(StartCurrentTurn), 0.5f); // normal flow
        }
    }

    // public void StartNextTurnFromButton()
    // {
    //     if (!waitingForNextTurnButton)
    //     {
    //         Debug.Log("NextTurn button pressed but turn not finished yet.");
    //         return;
    //     }

    //     waitingForNextTurnButton = false;

    //     Debug.Log("NextTurn button confirmed. Starting next turn.");

    //     StartCurrentTurn();
    // }
    public Player GetCurrentPlayer()
    {
        return players[currentPlayer];
    }

    void AISelectRandomTarget()
    {
        if (GameModeManager.isOnlineMode)
        {
            Debug.Log("ONLINE MODE → AI handled by server");
            return;
        }
        if (gameOver)
            return;

        if (deck.CardCount() == 0)
        {
            TriggerGameOver();
            return;
        }

        if (!lockTargetSelection)
        {
            foreach (UIPlayer ui in uiPlayers)
            {
                ui.HideTargetPopup();
            }
        }

        if (turnActionRunning)
            return;

        Player aiPlayer = players[currentPlayer];
        List<Card> aiCards = aiPlayer.PlayerHand.Cards;

        if (aiCards.Count == 0)
        {
            Debug.Log("AI has no cards, skipping turn.");
            EndTurn();
            return;
        }

        // ✅ STEP 1 — CREATE possibleTargets FIRST (FIX)
        List<int> possibleTargets = new List<int>();
        int count = ModeSelectionController.selectedPlayers;

        for (int i = 0; i < activePlayers.Length; i++)
        {
            int id = activePlayers[i];

            if (id != currentPlayer)
            {
                possibleTargets.Add(id);
            }
        }

        // ✅ STEP 2 — GET SORTED RANKS
        List<int> sortedRanks = GetSortedRanks(aiCards);

        bool foundValidMove = false;

        foreach (int rank in sortedRanks)
        {
            selectedRank = rank;

            if (!PlayerHasRankInHand(currentPlayer, selectedRank))
                continue;

            int tempTarget = SelectBestTarget(possibleTargets, selectedRank);

            if (!AlreadyAskedThisTurn(tempTarget, selectedRank))
            {
                foundValidMove = true;
                break;
            }
        }

        if (!foundValidMove)
        {
            Debug.Log("No valid moves left. Ending turn.");
            EndTurn();
            return;
        }

        // ✅ FINAL SAFETY CHECKS (UNCHANGED)
        if (completedRanks.Contains(selectedRank))
        {
            Debug.Log("Selected rank is already completed. Retrying...");
            Invoke(nameof(AISelectRandomTarget), 1f);
            return;
        }

        if (!PlayerHasRankInHand(currentPlayer, selectedRank))
        {
            Invoke(nameof(AISelectRandomTarget), 2.2f);
            return;
        }

        Debug.Log(aiPlayer.PlayerName + " asking for rank " + selectedRank);

        // ✅ FINAL TARGET SELECT
        int randomTarget = SelectBestTarget(possibleTargets, selectedRank);

        int targetUIIndex = GetUIIndex(randomTarget);
        currentTargetUI = uiPlayers[targetUIIndex];

        int currentUIIndex = GetUIIndex(currentPlayer);
        string displayName = players[randomTarget].IsHuman
     ? ProfileData.PlayerName
     : players[randomTarget].PlayerName;

        uiPlayers[currentUIIndex].ShowAskPopup(displayName, selectedRank);

        Debug.Log("AI asking " + players[randomTarget].PlayerName +
                  " for rank " + selectedRank);

        turnActionRunning = true;

        Invoke(nameof(ResolveAsk), 2f);
    }
    void ResolveAsk()
    {
        if (GameModeManager.isOnlineMode)
        {
            Debug.Log("ONLINE MODE → ResolveAsk skipped (server handles)");
            return;
        }
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
            UpdateMemoryOnSuccess(currentTargetUI.GetPlayerID(), selectedRank);
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
            UpdateMemoryOnFailure(currentTargetUI.GetPlayerID(), selectedRank);
            Debug.Log("GO FISH!");
            // ⭐ remember the asked rank before drawing
            lastAskedRank = selectedRank;
            currentTargetUI.ShowReplyPopup(false, 0, selectedRank);
            // If target is human → wait for GoFish button
            if (targetPlayer.IsHuman)
            {
                Debug.Log("Waiting for human to click GO FISH button");

                // ⭐ SHOW TOAST
                if (toastUI != null)
                {
                    toastUI.ShowToast("Please click  Fish  icon button");
                }

                waitingForDeckClick = true;

                return;
            }

            // AI target → auto draw
            Card drawn;

            if (GameModeManager.isOnlineMode)
            {
                int value = NetworkDeckManager.Instance.DrawCard();

                if (value == -1)
                    drawn = null;
                else
                    drawn = ConvertToCard(value);
            }
            else
            {
                drawn = deck.GetCard();
            }

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
        toastUI.HideToast();
        // 🚫 BLOCK if GO FISH pending
        if (lockTargetSelection)
        {
            Debug.Log("Target selection locked until card draw.");
            return;
        }
        // Only clear if NOT locked
        if (!lockTargetSelection)
        {
            foreach (UIPlayer ui in uiPlayers)
            {
                ui.HideTargetPopup();
            }
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

        int uiIndex = GetUIIndex(targetID);
        currentTargetUI = uiPlayers[uiIndex];
        askedRankTargetThisTurn.Add(key);

        int currentUIIndex = GetUIIndex(currentPlayer);
        string displayName = players[targetID].IsHuman
    ? ProfileData.PlayerName
    : players[targetID].PlayerName;

        uiPlayers[currentUIIndex].ShowAskPopup(displayName, selectedRank);
        currentTargetUI.ShowTargetPopup();
        turnActionRunning = true;
        Debug.Log("Human asking for rank: " + selectedRank);

        if (GameModeManager.isOnlineMode)
        {
            if (!playerIdToClientId.ContainsKey(targetID))
            {
                Debug.LogError("Target ClientID not found");
                return;
            }

            ulong targetClientId = playerIdToClientId[targetID];
            Debug.Log("Sending RPC → Rank: " + selectedRank + " TargetClientId: " + targetClientId);
            NetworkGameManager.Instance.RequestCardRpc(selectedRank, targetClientId);
            //STOP LOCAL LOGIC
            return;
        }


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
            toastUI.ShowToast("You got cards! Select any rank card");
            return; // DO NOT END TURN
        }

        // CASE 2 — GO FISH
        Debug.Log("GO FISH!");
        toastUI.ShowToast("Go Fish! Draw a card from deck");
        lockTargetSelection = true; // 🔥 LOCK UI
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
        toastUI.HideToast();
        toastUI.ShowToast("Now select a player");

        Debug.Log("Selected Rank: " + rank);
        Debug.Log("Now select a target player");

    }

    void RefreshAllHands()
    {
        for (int i = 0; i < activePlayers.Length; i++)
        {
            int id = activePlayers[i];
            bool showFront = players[id].IsHuman;

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
            Card drawn;

            if (GameModeManager.isOnlineMode)
            {
                Debug.Log("ONLINE MODE → refill handled by server");
                yield break;
            }
            else
            {
                drawn = deck.GetCard();
            }

            if (drawn == null)
                yield break;

            RemoveTopDeckVisual();

            int uiIndex = GetUIIndex(playerID);

            yield return StartCoroutine(
                AnimateCardMove(deckPosition, uiPlayers[uiIndex].transform, drawn, playerID)
            );
            player.AddCard(drawn);
        }

        RefreshAllHands();
    }

    public void OnDeckClicked()
    {
        toastUI.HideToast();
        Debug.Log("Deck clicked.");
        if (gameOver)
        {
            Debug.Log("Game is already over. Deck click ignored.");
            return;
        }

        if (!waitingForDeckClick)
        {
            Debug.Log("not waiting for deck click. Ignoring.");
            return;
        }


        Card drawn = null;
        if (!GameModeManager.isOnlineMode)
        {
            drawn = deck.GetCard();
        }
        else
        {
            Debug.Log("ONLINE MODE → deck click ignored (server handles draw)");
            return;
        }
        if (drawn == null)
        {
            Debug.Log("Deck is empty. Ending game.");
            TriggerGameOver();
            return;
        }
        Debug.Log("Card drawn from deck: Rank " + drawn.Rank + ", Suit " + drawn.Suit);
        waitingForDeckClick = false;
        lockTargetSelection = false; // 🔥 UNLOCK after draw

        Debug.Log("removed top deck visual");
        RemoveTopDeckVisual();
        toastUI.ShowToast("Drawing a card...");
        StartCoroutine(DrawCardWithAnimation(drawn));
    }

    void RemoveTopDeckVisual()
    {
        int actualDeckCount = deck.CardCount(); // remaining real cards
        int currentVisualCount = deckVisualCards.Count;

        // 🔥 calculate how many SHOULD be visible
        int expectedVisualCount = Mathf.Min(actualDeckCount, maxVisibleDeckCards);

        // 🔥 remove ONLY if visuals exceed expected
        if (currentVisualCount > expectedVisualCount)
        {
            GameObject topCard = deckVisualCards[deckVisualCards.Count - 1];

            deckVisualCards.RemoveAt(deckVisualCards.Count - 1);

            Destroy(topCard);
        }

        // 👇 enable new top card collider
        if (deckVisualCards.Count > 0)
        {
            GameObject newTop = deckVisualCards[deckVisualCards.Count - 1];

            Collider2D col = newTop.GetComponent<Collider2D>();
            if (col != null)
                col.enabled = true;
        }
    }
    IEnumerator AnimateCardMove(Transform from, Transform to, Card card, int targetPlayerID)
    {
        int uiIndex = GetUIIndex(currentPlayer);

        GameObject flyingCard = Instantiate(uiPlayers[uiIndex].cardPrefab, from.position, Quaternion.identity);
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
        // ⭐ FORCE INITIAL STATE (VERY IMPORTANT)
        uiCard.ShowFront(false);

        bool comingFromDeck = (from == deckPosition);

        if (comingFromDeck)
        {
            if (players[targetPlayerID].IsHuman)
                uiCard.ShowFront(true);
            else
                uiCard.ShowFront(false);
        }
        else
        {
            // transfer between players → always visible
            uiCard.ShowFront(true);
        }

        // ⭐ STEP 4 — WAIT ONE FRAME (VERY IMPORTANT)
        yield return null;

        // ⭐ RE-APPLY AGAIN AFTER FRAME (FINAL GUARANTEE)
        if (comingFromDeck)
        {
            if (players[targetPlayerID].IsHuman)
                uiCard.ShowFront(true);
            else
                uiCard.ShowFront(false);
        }
        else
        {
            uiCard.ShowFront(true);
        }

        flyingCard.transform.localScale = Vector3.one * 0.7f;

        Vector3 midPoint = (from.position + to.position) / 2 + Vector3.up * 1.5f;

        Sequence seq = DOTween.Sequence().SetLink(flyingCard);
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

        DOTween.Kill(flyingCard); // 🔥 kill all tweens on this object
        Destroy(flyingCard);
    }

    IEnumerator TransferCardsWithAnimation(int fromID, int toID, List<Card> cards)
    {
        if (gameOver)
            yield break;
        int fromUI = GetUIIndex(fromID);
        int toUI = GetUIIndex(toID);

        Transform fromTransform = uiPlayers[fromUI].transform;
        Transform toTransform = uiPlayers[toUI].transform;
        foreach (Card c in cards)
        {
            yield return StartCoroutine(
               AnimateCardMove(fromTransform, toTransform, c, toID)
            );

            players[toID].AddCard(c);
            // ⭐ STEP 1: receiver gets HIGH confidence
            aiMemory.AddConfidence(toID, c.Rank, 6f);

            // ⭐ STEP 2: all other players LOW confidence (they probably don't have it)
            for (int i = 0; i < players.Length; i++)
            {
                if (i != toID)
                {
                    aiMemory.AddConfidence(i, c.Rank, -2f);
                }
            }
        }

        // wait small moment so user sees animation clearly
        // wait small moment so user sees animation clearly
        yield return new WaitForSeconds(0.25f);

        // ⭐ STEP 1: show updated hand first
        RefreshAllHands();

        // ⭐ STEP 2: WAIT so player can SEE the new cards
        yield return new WaitForSeconds(1.5f);  // 👉 you can tune (1.0 - 1.5)

        // ⭐ STEP 3: NOW check for book
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
        if (GameModeManager.isOnlineMode)
        {
            Debug.Log("ONLINE MODE → animation draw skipped");
            yield break;
        }
        if (gameOver)
            yield break;

        // ⭐ CHECK IF THIS IS LAST CARD DRAWN BY AI
        bool isLastCard = (deck.CardCount() == 0);

        if (!players[currentPlayer].IsHuman && isLastCard)
        {
            Debug.Log("Last card drawn by AI → NO animation");

            players[currentPlayer].AddCard(drawn);
            RefreshAllHands();

            // ⭐ ADD DELAY HERE ALSO
            yield return new WaitForSeconds(1.5f);

            CheckForBook(currentPlayer);

            // ⭐ NORMAL FLOW CONTINUES
            if (drawn.Rank == lastAskedRank)
            {
                if (!players[currentPlayer].IsHuman)
                    Invoke(nameof(AISelectRandomTarget), 2.1f);
            }
            else
            {
                selectedRank = -1;
                EndTurn();
            }

            lastAskedRank = -1;
            turnActionRunning = false;

            // ⭐ GAME END CHECK
            StartCoroutine(EndGameAfterDelay());

            yield break; // 🚨 VERY IMPORTANT
        }
        Transform fromTransform = deckPosition;
        int uiIndex = GetUIIndex(currentPlayer);
        Transform toTransform = uiPlayers[uiIndex].transform;
        yield return StartCoroutine(
    AnimateCardMove(fromTransform, toTransform, drawn, currentPlayer));
        // ✅ AFTER animation finishes
        players[currentPlayer].AddCard(drawn);

        // ⭐ STEP 1: show updated hand
        RefreshAllHands();

        // ⭐ STEP 2: WAIT so player can SEE the drawn card
        yield return new WaitForSeconds(1.5f);   // same delay as transfer

        // ⭐ STEP 3: NOW check for book
        CheckForBook(currentPlayer);

        // ⭐ check if deck is now empty AFTER draw
        // if (deck.CardCount() == 0)
        // {
        //     Debug.Log("Last deck card drawn.");

        //     lastDeckCardDrawn = true;

        //     // ⭐ check if book formed from this draw
        //     bool bookExists = false;

        //     Dictionary<int, int> rankCount = new Dictionary<int, int>();

        //     foreach (Card c in players[currentPlayer].PlayerHand.Cards)
        //     {
        //         if (!rankCount.ContainsKey(c.Rank))
        //             rankCount[c.Rank] = 0;

        //         rankCount[c.Rank]++;

        //         if (rankCount[c.Rank] == 4)
        //         {
        //             bookExists = true;
        //             break;
        //         }
        //     }

        //     // ⭐ if NO book → end game immediately
        //     if (!bookExists)
        //     {
        //         Debug.Log("Deck empty and no book possible. Ending game immediately.");

        //         StartCoroutine(EndGameAfterDelay());
        //         yield break;
        //     }
        // }

        Debug.Log("Drew card of rank: " + drawn.Rank);

        // ✅ NOW decide turn
        if (drawn.Rank == lastAskedRank)
        {
            Debug.Log("Drawn card matches! Continue turn.");

            // ⭐ show lucky popup message
            int uiIndex2 = GetUIIndex(currentPlayer);
            uiPlayers[uiIndex2].ShowLuckyDrawPopup(drawn.Rank);
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

        // ⭐ AFTER everything finishes → check deck empty
        if (deck.CardCount() == 0)
        {
            Debug.Log("Deck empty after draw. Waiting for book resolution.");

            StartCoroutine(EndGameAfterDelay());
        }
        // ⭐ SHOW NEXT INSTRUCTION ONLY IF HUMAN TURN CONTINUES
        if (players[currentPlayer].IsHuman && !gameOver)
        {
            toastUI.ShowToast("Your turn! Select a rank card");
        }

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
        // ⭐ STEP 1 — wait for animations / book message
        yield return new WaitForSeconds(1.2f);

        // ⭐ STEP 2 — HIDE TOAST BEFORE GAME OVER
        if (toastUI != null)
        {
            toastUI.HideToast();
        }

        // ⭐ STEP 3 — small delay for smooth UX
        yield return new WaitForSeconds(0.3f);

        // ⭐ STEP 4 — NOW trigger game over
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
                // ⭐ TOAST — BOOK STARTING
                if (toastUI != null)
                {
                    if (players[playerID].IsHuman)
                        toastUI.ShowToast("Book forming...");
                }
                completedRanks.Add(group.Key);
                // ⭐ prevent asking this rank again
                if (selectedRank == group.Key)
                    selectedRank = -1;

                aiMemory.ClearRank(group.Key);
                bookJustFormed = true; // 🔥 mark that book happened
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
        CancelInvoke(nameof(AISelectRandomTarget));
        turnActionRunning = true;
        Player player = players[playerID];

        int uiIndex = GetUIIndex(playerID);

        Transform fromTransform = uiPlayers[uiIndex].transform;

        // choose book display position (right side of player)
        Vector3 bookPosition = fromTransform.position + Vector3.right * 2f;

        foreach (Card c in cards)
        {
            yield return StartCoroutine(
                AnimateCardMove(fromTransform, fromTransform, c, playerID)
            );

            player.PlayerHand.RemoveCard(c);
        }

        player.AddPoint();
        // ⭐ TRACK BOOK ORDER
        player.LastBookTurn = bookOrderCounter;
        bookOrderCounter++;
        // ⭐ TOAST — BOOK COMPLETED
        if (toastUI != null)
        {
            if (players[playerID].IsHuman)
                toastUI.ShowToastWithAutoHide("You completed a book!", 3f);
            else
                toastUI.ShowToastWithAutoHide(players[playerID].PlayerName + " completed a book!", 3f);
        }
        uiPlayers[uiIndex].UpdateScore(player.Score);
        Debug.Log("Score of " + player.PlayerName + ": " + player.Score);

        RefreshAllHands();
        // ⭐ check if player has no cards after book
        yield return StartCoroutine(RefillHandIfEmpty(playerID));

        turnActionRunning = false;
        selectedRank = -1; // ⭐ reset asking rank when book forms
                           // ⭐ AFTER book fully completes → check deck empty
        if (deck.CardCount() == 0)
        {
            Debug.Log("Deck empty after book completion. Ending game.");

            StartCoroutine(EndGameAfterDelay());
        }
        // ⭐ if the last deck card caused this book, end the game now
        // if (lastDeckCardDrawn)
        // {
        //     Debug.Log("Last deck card completed a book. Ending game.");
        //     StartCoroutine(EndGameAfterDelay());
        //     lastDeckCardDrawn = false;
        // }
        bookJustFormed = false; // 🔥 safety reset

    }

    void TriggerGameOver()
    {
        DOTween.KillAll();
        // ⭐ EXTRA SAFETY — ensure toast is hidden
        if (toastUI != null)
        {
            toastUI.HideToast();
        }
        if (gameOver)
            return;

        gameOver = true;
        // ⭐ STEP 1 — INCREASE GAMES PLAYED
        ProfileData.GamesPlayed++;

        Debug.Log("GAME OVER TRIGGERED");
        // ⭐ DEBUG PRINT — PLAYER AVATAR MAPPING (END)
        Debug.Log("=== GAME END PLAYER AVATAR DATA ===");

        foreach (Player p in players)
        {
            Debug.Log("PlayerID: " + p.PlayerID +
                      " | Name: " + p.PlayerName +
                      " | AvatarIndex: " + p.AvatarIndex +
                      " | Score: " + p.Score +
                      " | IsHuman: " + p.IsHuman);
        }

        Debug.Log("==================================");

        CancelInvoke();       // stop AI invokes
        StopAllCoroutines();  // ⭐ stop animations immediately

        waitingForDeckClick = false;
        waitingForTarget = false;
        turnActionRunning = true;

        foreach (UIPlayer ui in uiPlayers)
        {
            ui.canInteract = false;
        }
        // ⭐ STEP 2 — FIND WINNER
        Player winner = players[0];

        foreach (Player p in players)
        {
            if (p.Score > winner.Score)
            {
                winner = p;
            }
        }

        // ⭐ STEP 3 — IF HUMAN WON → INCREASE
        if (winner.IsHuman)
        {
            Debug.Log("Human WON!");
            ProfileData.GamesWon++;
        }
        else
        {
            Debug.Log("AI WON!");
        }
        // ⭐ STEP 4 — SAVE TO CLOUD
        PlayFabProfileManager.Instance.SaveProfile(
            ProfileData.PlayerName,
            ProfileSaveSystem.LoadAvatarIndex()
        );
        gameOverUI.ShowGameOver(players);
    }

    public void OnGoFishButtonClicked()
    {
        if (toastUI != null)
        {
            toastUI.HideToast();
        }
        // ⭐ Go Fish button should only work when waiting for deck click
        if (!waitingForDeckClick)
        {
            Debug.Log("Go Fish button pressed but not allowed right now.");
            return;
        }

        if (gameOver)
            return;

        Debug.Log("Human pressed GO FISH");

        Card drawn = null;
        if (!GameModeManager.isOnlineMode)
        {
            drawn = deck.GetCard();
        }
        else
        {
            Debug.Log("ONLINE MODE → GoFish handled by server");
            return;
        }

        if (drawn == null)
        {
            TriggerGameOver();
            return;
        }

        waitingForDeckClick = false;

        RemoveTopDeckVisual();

        StartCoroutine(DrawCardWithAnimation(drawn));
    }

    public void RestartGame()
    {
        Debug.Log("RESTARTING GAME...");
        DOTween.KillAll(); // 🔥 kill all tweens safely

        // 🔥 STOP everything
        StopAllCoroutines();
        CancelInvoke();

        // 🔥 RESET FLAGS
        gameOver = false;
        waitingForDeckClick = false;
        waitingForTarget = false;
        turnActionRunning = false;

        selectedRank = -1;
        lastAskedRank = -1;

        askedRankTargetThisTurn.Clear();
        completedRanks.Clear();

        // 🔥 CLEAR DECK VISUALS
        foreach (GameObject card in deckVisualCards)
        {
            Destroy(card);
        }
        deckVisualCards.Clear();

        // 🔥 RESET PLAYERS UI
        foreach (UIPlayer ui in uiPlayers)
        {
            //  ui.canInteract = true;

            // clear cards visually
            ui.RefreshHand(false); // temporary clear
        }

        // 🔥 RESTART GAME COMPLETELY
        SetupGame();
    }

    public void ApplyPublicState(ulong[] ids, int[] scores, int[] cardCounts)
    {
        Debug.Log("Applying PUBLIC state");
        for (int i = 0; i < ids.Length; i++)
        {
            // STEP 1: convert clientId → local playerId
            int localId = GetLocalPlayerId(ids[i]);
            // STEP 2: safety check
            if (localId == -1)
            {
                Debug.LogWarning("Player mapping not found for clientId: " + ids[i]);
                continue;
            }
            // STEP 3: update score
            players[localId].SetScore(scores[i]);
            players[localId].SetPublicCardCount(cardCounts[i]);
        }
        // STEP 5: refresh UI
        RefreshAllHands();
    }

    public void ApplyPrivateHand(int[] hand)
    {
        Debug.Log("Applying PRIVATE hand");
        // STEP 1: find MY player id
        int myId = GetLocalPlayerId(NetworkManager.Singleton.LocalClientId);

        if (myId == -1)
        {
            Debug.LogError("My PlayerID not found ❌");
            return;
        }
        // STEP 2: CLEAR OLD CARDS
        players[myId].PlayerHand.Clear();
        // STEP 3: ADD NEW CARDS
        foreach (int value in hand)
        {
            players[myId].AddCard(ConvertToCard(value));
        }
        // STEP 4: refresh UI
        RefreshAllHands();
        // STEP 5: VERY IMPORTANT (fix stuck turn)
        turnActionRunning = false;
    }

    int GetLocalPlayerId(ulong clientId)
    {
        foreach (var kvp in playerIdToClientId)
        {
            if (kvp.Value == clientId)
                return kvp.Key;
        }
        return -1;
    }

    public void ApplyNetworkTurn(ulong clientId)
    {
        //convert clientId → local player index
        int localId = GetLocalPlayerId(clientId);
        //safety check
        if (localId == -1)
        {
            Debug.LogError("Turn player not found");
            return;
        }

        if(players !=null && currentPlayer<players.Length){
            players[currentPlayer].EndTurn();
        }
        //update current player
        currentPlayer = newPlayer;
        // start new player turn
        players[currentPlayer].StartTurn();

        Debug.Log("Turn applied to: " + players[currentPlayer].PlayerName);
        //OPTIONAL UI feedback
        if (players[currentPlayer].IsHuman)
        {
            if (toastUI != null)
            {
                toastUI.ShowToast("Your turn!");
            }
        }
    }

    public void InitializeMultiplayer()
    {
        Debug.Log("Initializing Multiplayer Game Setup...");

        // STEP 0 — Make sure players array exists
        players = new Player[4];

        // Safety
        if (NetworkPlayerManager.Instance == null)
        {
            Debug.LogError("NetworkPlayerManager missing ❌");
            return;
        }

        var netPlayers = NetworkPlayerManager.Instance.players;

        if (netPlayers == null || netPlayers.Count == 0)
        {
            Debug.LogError("No network players found ❌");
            return;
        }

        // STEP 1 — Assign UI players based on network players
        for (int i = 0; i < netPlayers.Count; i++)
        {
            if (i >= uiPlayers.Length) break;

            NetworkPlayer netPlayer = netPlayers[i];

            // Create local Player object using network data
            Player p = new Player(
                i,
                netPlayer.playerName.Value.ToString(),
                netPlayer.OwnerClientId == NetworkManager.Singleton.LocalClientId,
                netPlayer.avatarIndex.Value
            );

            players[i] = p;
            playerIdToClientId[i] = netPlayer.OwnerClientId;

            // Initialize UI
            uiPlayers[i].Initialize(p);
            BindUIPlayer(uiPlayers[i]);
            uiPlayers[i].RefreshHand(p.IsHuman);
        }

        activePlayers = new int[NetworkPlayerManager.Instance.players.Count];

        for (int i = 0; i < activePlayers.Length; i++)
        {
            activePlayers[i] = i;
        }
        // deck = new Deck(this);

        // for (int i = 0; i < netPlayers.Count; i++)
        // {
        //     StartCoroutine(SyncHands(netPlayers));
        // }
        // StartCoroutine(SyncHands(netPlayers));
        //Refresh all hands visually
        RefreshAllHands();

        if (cardBackPrefab == null || deckPosition == null)
        {
            Debug.LogError("Deck Visual references missing");
        }
        else
        {
            CreateDeckVisual();
        }
        currentPlayer = 0;
        // StartCurrentTurn();

        // STEP 3 — Enable interaction
        foreach (UIPlayer ui in uiPlayers)
        {
            ui.canInteract = true;
        }

        Debug.Log("Multiplayer Setup Complete ✔");
    }

    // System.Collections.IEnumerator SyncHands(List<NetworkPlayer> netPlayers)
    // {
    //     yield return new WaitForSeconds(0.5f); // wait for server sync
    //     for (int i = 0; i < netPlayers.Count; i++)
    //     {
    //         foreach (var card in netPlayers[i].hand)
    //         {
    //             players[i].AddCard(ConvertToCard(card));
    //         }
    //     }
    //     RefreshAllHands();
    //     Debug.Log("Hands synced correctly");
    // }


    void UpdateMemoryOnSuccess(int playerID, int rank)
    {
        // target had cards → strong confidence
        aiMemory.AddConfidence(playerID, rank, 5f);

        // ⭐ asking player now has those cards too
        aiMemory.AddConfidence(currentPlayer, rank, 3f);
    }
    void UpdateMemoryOnFailure(int playerID, int rank)
    {
        // target probably doesn't have it
        aiMemory.AddConfidence(playerID, rank, -3f);
        // ⭐ small uncertainty (not 100% sure)
        aiMemory.AddConfidence(playerID, rank, -1f);

        // ⭐ asking player likely has it (they asked!)
        aiMemory.AddConfidence(currentPlayer, rank, 2f);
    }

    int SelectBestTarget(List<int> possibleTargets, int rank)
    {
        int bestTarget = -1;
        float bestScore = float.MinValue;

        foreach (int target in possibleTargets)
        {
            if (AlreadyAskedThisTurn(target, rank))
                continue;

            float memoryScore = aiMemory.GetConfidence(target, rank);
            // ⭐ STEP 4 ADD HERE (VERY IMPORTANT)
            if (PlayerHasRankInHand(currentPlayer, rank))
            {
                memoryScore += 2f; // AI prefers ranks it owns
            }

            float randomFactor = Random.Range(0f, 1f);

            float totalScore = (memoryScore * 0.8f) + (randomFactor * 0.2f);

            if (totalScore > bestScore)
            {
                bestScore = totalScore;
                bestTarget = target;
            }
        }

        // fallback
        if (bestTarget == -1 && possibleTargets.Count > 0)
        {
            bestTarget = possibleTargets[Random.Range(0, possibleTargets.Count)];
        }

        return bestTarget;
    }

    List<int> GetSortedRanks(List<Card> aiCards)
    {
        Dictionary<int, float> rankScores = new Dictionary<int, float>();

        foreach (Card c in aiCards)
        {
            int rank = c.Rank;

            if (completedRanks.Contains(rank))
                continue;

            if (!rankScores.ContainsKey(rank))
                rankScores[rank] = 0f;

            // memory score
            for (int i = 0; i < players.Length; i++)
            {
                if (i == currentPlayer) continue;
                rankScores[rank] += aiMemory.GetConfidence(i, rank);
            }

            // count bonus
            int count = aiCards.FindAll(x => x.Rank == rank).Count;
            rankScores[rank] += count * 1.5f;
        }

        // sort ranks by score (highest first)
        List<int> sortedRanks = new List<int>(rankScores.Keys);
        sortedRanks.Sort((a, b) => rankScores[b].CompareTo(rankScores[a]));

        return sortedRanks;
    }

    Card ConvertToCard(int value)
    {
        int rank = value / 10;
        int suit = value % 10;
        return new Card(rank, (CardSuit)suit);
    }


}
