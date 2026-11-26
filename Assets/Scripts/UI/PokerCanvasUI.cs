using System.Collections;
using System.Collections.Generic;

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class PokerCanvasUI : MonoBehaviour
{
    [Header("Status Bar")]
    [SerializeField] private TMP_Text handNumberText;
    [SerializeField] private TMP_Text stageText;
    [SerializeField] private TMP_Text potText;

    [Header("First Hand Waiting Panel")]
    [SerializeField] private GameObject waitingPanel;
    [SerializeField] private TMP_Text waitingTitleText;
    [SerializeField] private TMP_Text waitingPlayerCountText;
    [SerializeField] private TMP_Text waitingCountdownText;
    [SerializeField] private TMP_Text waitingMessageText;

    [Header("Player Seats")]
    [SerializeField]
    private PokerSeatUI[] seats =
        new PokerSeatUI[6];

    [Header("Community Cards")]
    [SerializeField]
    private PokerCardUI[] communityCards =
        new PokerCardUI[5];

    [Header("Seat Cards - Seat 1 to Seat 6")]
    [SerializeField] private PokerSeatCardsUI[] seatCards =
        new PokerSeatCardsUI[6];

    private int displayedHand = -1;
    [Header("Dealing Animation")]
    [SerializeField] private PokerCardDealAnimator dealAnimator;
    private Coroutine dealRoutine;
    private bool isDealing;
    private bool dealPending;
    private float dealDeadline;

    [Header("Betting Controls")]
    [SerializeField] private Button foldButton;
    [SerializeField] private Button checkButton;
    [SerializeField] private Button callButton;
    [SerializeField] private Button raiseButton;
    [SerializeField] private Button allInButton;

    [SerializeField] private TMP_InputField raiseInput;

    [Header("Optional Betting Labels")]
    [SerializeField] private TMP_Text callButtonText;
    [SerializeField] private TMP_Text minimumRaiseText;

    [Header("Raise Notification")]
    [SerializeField] private TMP_Text raiseNotificationText;
    private float raiseNotificationUntil;

    [Header("Winner Panel")]
    [SerializeField] private GameObject winnerPanel;
    [SerializeField] private TMP_Text winnerText;
    [SerializeField] private Button newHandButton;

    [Header("Quick Raises and Guidance")]
    [SerializeField] private Button halfPotButton, fullPotButton;
    [SerializeField] private TMP_Text guidanceText;
    private PokerTableManager table;
    private PokerPlayerData localPlayer;

    private bool hasLocalPlayer;
    private int localSeat = -1;

    private readonly bool[] occupiedSeats = new bool[6];

    private void Start()
    {
        if (halfPotButton != null) halfPotButton.onClick.AddListener(OnHalfPot);
        if (fullPotButton != null) fullPotButton.onClick.AddListener(OnFullPot);
        foldButton.onClick.AddListener(OnFold);
        checkButton.onClick.AddListener(OnCheck);
        callButton.onClick.AddListener(OnCall);
        raiseButton.onClick.AddListener(OnRaise);
        allInButton.onClick.AddListener(OnAllIn);
        newHandButton.onClick.AddListener(OnNewHand);

        raiseInput.contentType =
            TMP_InputField.ContentType.IntegerNumber;

        if (winnerPanel != null)
            winnerPanel.SetActive(false);

        HideWaitingPanel();
        ClearRaiseNotification();
        SetButtonsInteractable(false);
        ResetCards();
    }

    private void OnDestroy()
    {
        if (halfPotButton != null) halfPotButton.onClick.RemoveListener(OnHalfPot);
        if (fullPotButton != null) fullPotButton.onClick.RemoveListener(OnFullPot);
        foldButton.onClick.RemoveListener(OnFold);
        checkButton.onClick.RemoveListener(OnCheck);
        callButton.onClick.RemoveListener(OnCall);
        raiseButton.onClick.RemoveListener(OnRaise);
        allInButton.onClick.RemoveListener(OnAllIn);
        newHandButton.onClick.RemoveListener(OnNewHand);
    }

    // Poll lightweight value snapshots; rebuild UI only when state changes.
    private bool snapshotValid;
    private PokerTableManager snapshotTable;
    private ulong snapshotClient;
    private int snapshotHand, snapshotPot, snapshotTurn, snapshotBet, snapshotRaise, snapshotCountdown;
    private PokerStage snapshotStage;
    private double snapshotTurnDeadline;
    private bool snapshotHasCards;
    private int snapshotPrivateHand;
    private Card snapshotCard1, snapshotCard2;
    private Unity.Collections.FixedString512Bytes snapshotResult;
    private readonly PokerPlayerData[] snapshotPlayers = new PokerPlayerData[6];
    private readonly Unity.Collections.FixedString64Bytes[] snapshotBoard = new Unity.Collections.FixedString64Bytes[5];
    private int snapshotPlayerCount, snapshotBoardCount;

    private bool CaptureChangedState()
    {
        ulong client = NetworkManager.Singleton.LocalClientId;
        int playerCount = table.Players.Count;
        int boardCount = table.CommunityCards.Count;
        // Fall back to normal refresh if the table ever exceeds its expected capacity.
        if (playerCount > snapshotPlayers.Length || boardCount > snapshotBoard.Length)
        {
            snapshotValid = false;
            return true;
        }
        bool changed = !snapshotValid || snapshotTable != table || snapshotClient != client ||
            snapshotHand != table.HandNumber.Value || snapshotPot != table.TotalPot.Value ||
            snapshotTurn != table.CurrentTurnSeat.Value || snapshotBet != table.CurrentBet.Value ||
            snapshotRaise != table.MinimumRaise.Value || snapshotCountdown != table.FirstHandCountdown.Value ||
            snapshotStage != table.CurrentStage.Value || snapshotTurnDeadline != table.TurnDeadline.Value ||
            snapshotHasCards != table.HasPrivateCards ||
            snapshotPrivateHand != table.PrivateCardsHandNumber ||
            snapshotCard1.Rank != table.PrivateCard1.Rank || snapshotCard1.Suit != table.PrivateCard1.Suit ||
            snapshotCard2.Rank != table.PrivateCard2.Rank || snapshotCard2.Suit != table.PrivateCard2.Suit ||
            !snapshotResult.Equals(table.HandResultText.Value) ||
            snapshotPlayerCount != playerCount || snapshotBoardCount != boardCount;
        for (int i = 0; i < playerCount; i++)
        {
            var player = table.Players[i];
            if (!snapshotPlayers[i].Equals(player)) changed = true;
            snapshotPlayers[i] = player;
        }
        for (int i = 0; i < boardCount; i++)
        {
            var card = table.CommunityCards[i];
            if (!snapshotBoard[i].Equals(card)) changed = true;
            snapshotBoard[i] = card;
        }
        snapshotValid = true;
        snapshotTable = table;
        snapshotClient = client;
        snapshotHand = table.HandNumber.Value;
        snapshotPot = table.TotalPot.Value;
        snapshotTurn = table.CurrentTurnSeat.Value;
        snapshotBet = table.CurrentBet.Value;
        snapshotRaise = table.MinimumRaise.Value;
        snapshotCountdown = table.FirstHandCountdown.Value;
        snapshotStage = table.CurrentStage.Value;
        snapshotTurnDeadline = table.TurnDeadline.Value;
        snapshotHasCards = table.HasPrivateCards;
        snapshotPrivateHand = table.PrivateCardsHandNumber;
        snapshotCard1 = table.PrivateCard1;
        snapshotCard2 = table.PrivateCard2;
        snapshotResult = table.HandResultText.Value;
        snapshotPlayerCount = playerCount;
        snapshotBoardCount = boardCount;
        return changed;
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsClient ||
            !NetworkManager.Singleton.IsConnectedClient)
        {
            SetButtonsInteractable(false);
            ResetDisconnectedUI();
            return;
        }

        if (table == null || !table.IsSpawned)
            table = PokerTableManager.Instance;

        if (table == null || !table.IsSpawned)
        {
            SetButtonsInteractable(false);
            ResetDisconnectedUI();
            return;
        }

        bool changed = CaptureChangedState();
        if (changed || (dealPending && Time.realtimeSinceStartup >= dealDeadline))
        {
            FindLocalPlayer();
            RefreshUI();
        }
        // Notification expiry must work even when no network state changes.
        if (raiseNotificationUntil > 0f && Time.unscaledTime >= raiseNotificationUntil)
            ClearRaiseNotification();
    }

    private void ResetDisconnectedUI()
    {
        snapshotValid = false;
        snapshotTable = null;
        HideWaitingPanel();
        ClearRaiseNotification();
        if (displayedHand != -1 || table != null)
        {
            CancelDeal();
            ResetCards();
            foreach (var seat in seats)
                if (seat != null) seat.ClearSeat();
        }
        table = null;
        displayedHand = -1;
        hasLocalPlayer = false;
        localSeat = -1;
        if (winnerPanel != null) winnerPanel.SetActive(false);
        handNumberText.text = "HAND -";
        stageText.text = "Waiting";
        potText.text = "POT: 0";
        SetWaitingText(guidanceText, "Connect to join a table.");
    }

    private void FindLocalPlayer()
    {
        hasLocalPlayer = false;
        localSeat = -1;

        ulong clientId =
            NetworkManager.Singleton.LocalClientId;

        foreach (PokerPlayerData player in table.Players)
        {
            if (player.ClientId == clientId &&
                player.IsConnected)
            {
                localPlayer = player;
                localSeat = player.SeatIndex;
                hasLocalPlayer = true;
                break;
            }
        }
    }

    private void RefreshUI()
    {
        handNumberText.text =
            $"HAND {table.HandNumber.Value}";

        stageText.text = table.CurrentStage.Value.ToString();
        RefreshWaitingPanel();

        potText.text =
            $"POT: {table.TotalPot.Value}";

        if (displayedHand != table.HandNumber.Value)
        {
            ClearRaiseNotification();
            CancelDeal();
            ResetCards();
            displayedHand = table.HandNumber.Value;
            dealPending = displayedHand > 0 && dealAnimator != null &&
                table.CurrentStage.Value == PokerStage.PreFlop;
            dealDeadline = Time.realtimeSinceStartup + 2f;
        }
        RefreshSeats();
        RefreshCommunityCards();
        if (table.CurrentStage.Value != PokerStage.PreFlop &&
            (isDealing || dealPending)) CancelDeal();
        if (dealPending && !isDealing && hasLocalPlayer &&
            localPlayer.HasDealtCards && table.HasPrivateCards &&
            table.PrivateCardsHandNumber == displayedHand)
        {
            dealPending = false;
            isDealing = true;
            dealRoutine = StartCoroutine(DealPrivateCards());
        }
        if (dealPending && !isDealing &&
            ((hasLocalPlayer && !localPlayer.HasDealtCards) ||
             Time.realtimeSinceStartup >= dealDeadline))
            dealPending = false;
        if (!isDealing && !dealPending) RefreshSeatCards();
        RefreshBettingButtons();
        RefreshWinnerPanel();
        RefreshGuidance();
    }

    private void HideWaitingPanel()
    {
        if (waitingPanel != null && waitingPanel.activeSelf)
            waitingPanel.SetActive(false);
    }

    private void RefreshWaitingPanel()
    {
        if (waitingPanel == null) return;
        bool show = table.CurrentStage.Value == PokerStage.Waiting &&
                    table.HandNumber.Value == 0;
        if (!show)
        {
            HideWaitingPanel();
            return;
        }

        int count = 0;
        foreach (PokerPlayerData player in table.Players)
            if (player.IsConnected) count++;

        int seconds = table.FirstHandCountdown.Value;
        bool counting = count >= 2 && seconds >= 0;
        SetWaitingText(waitingTitleText, counting ? "GAME STARTING SOON" : "WAITING FOR PLAYERS");
        SetWaitingText(waitingPlayerCountText, $"{count} / 6 Players Connected");
        SetWaitingText(waitingCountdownText, counting ? $"{seconds:00}" : "--");
        SetWaitingText(waitingMessageText, counting ? "Game starting in seconds" : "Minimum 2 players required");
        if (!waitingPanel.activeSelf) waitingPanel.SetActive(true);
    }

    private static void SetWaitingText(TMP_Text label, string value)
    {
        if (label != null && label.text != value) label.text = value;
    }

    private void RefreshSeats()
    {
        System.Array.Clear(
            occupiedSeats, 0, occupiedSeats.Length);

        foreach (PokerPlayerData player in table.Players)
        {
            int index = player.SeatIndex;

            if (index < 0 ||
                index >= seats.Length ||
                index >= occupiedSeats.Length ||
                seats[index] == null)
                continue;

            occupiedSeats[index] = true;

            bool isLocalPlayer =
                player.ClientId ==
                NetworkManager.Singleton.LocalClientId;

            bool isCurrentTurn =
                table.CurrentTurnSeat.Value == index;

            seats[index].SetPlayer(
                player,
                isLocalPlayer,
                isCurrentTurn);
        }

        for (int i = 0; i < seats.Length; i++)
        {
            if (i < occupiedSeats.Length &&
                !occupiedSeats[i] &&
                seats[i] != null)
            {
                if (i < seatCards.Length && seatCards[i] != null)
                    seatCards[i].HideCards();
                seats[i].ClearSeat();
            }
        }
    }

    private void RefreshCommunityCards()
    {
        for (int i = 0; i < communityCards.Length; i++)
        {
            if (communityCards[i] == null)
                continue;

            if (i < table.CommunityCards.Count)
            {
                communityCards[i].ShowCard(
                    table.CommunityCards[i].ToString());
            }
            else
            {
                communityCards[i].HideCard();
            }
        }
    }

    private void ResetCards()
    {
        foreach (PokerSeatCardsUI pair in seatCards)
            if (pair != null) pair.HideCards();
        foreach (PokerCardUI card in communityCards)
            if (card != null) card.HideCard();
    }

    private void RefreshSeatCards()
    {
        bool showdown = table.CurrentStage.Value == PokerStage.Showdown;
        foreach (PokerPlayerData player in table.Players)
        {
            int index = player.SeatIndex;
            if (index < 0 || index >= seatCards.Length ||
                index >= seats.Length || seats[index] == null ||
                !seats[index].gameObject.activeInHierarchy ||
                seatCards[index] == null)
                continue;

            PokerSeatCardsUI pair = seatCards[index];
            if (!player.IsConnected || !player.HasDealtCards || player.IsFolded)
            {
                pair.HideCards();
                continue;
            }

            if (player.ClientId == NetworkManager.Singleton.LocalClientId &&
                table.HasPrivateCards &&
                table.PrivateCardsHandNumber == table.HandNumber.Value)
            {
                pair.ShowFaceUp(table.PrivateCard1.ToString(),
                    table.PrivateCard2.ToString());
            }
            else if (showdown && player.RevealedCard1.Length > 0 &&
                     player.RevealedCard2.Length > 0)
            {
                pair.ShowFaceUp(player.RevealedCard1.ToString(),
                    player.RevealedCard2.ToString());
            }
            else
            {
                pair.ShowFaceDown();
            }
        }
    }

    private void CancelDeal()
    {
        if (dealRoutine != null) StopCoroutine(dealRoutine);
        dealRoutine = null;
        isDealing = false;
        dealPending = false;
        if (dealAnimator != null) dealAnimator.ClearFlyingCards();
    }

    private IEnumerator DealPrivateCards()
    {
        // Allow synchronized player data and layout to settle.
        yield return null;
        var order = new List<int>();
        int dealer = table.DealerSeat.Value;
        for (int offset = 1; offset <= seats.Length; offset++)
        {
            int index = (dealer + offset + seats.Length) % seats.Length;
            foreach (PokerPlayerData player in table.Players)
            {
                if (player.SeatIndex == index && player.IsConnected &&
                    player.HasDealtCards && !player.IsFolded &&
                    index < seatCards.Length && seatCards[index] != null &&
                    seatCards[index].gameObject.activeInHierarchy)
                {
                    order.Add(index);
                    seatCards[index].PrepareDeal();
                    break;
                }
            }
        }
        Canvas.ForceUpdateCanvases();
        for (int round = 0; round < 2; round++)
        {
            foreach (int index in order)
            {
                PokerSeatCardsUI pair = seatCards[index];
                if (!pair.gameObject.activeInHierarchy) continue;
                yield return dealAnimator.DealCard(pair.GetTarget(round));
                pair.ShowDealtCard(round);
                yield return new WaitForSecondsRealtime(
                    Mathf.Max(0f, dealAnimator.CardInterval));
            }
        }
        isDealing = false;
        dealRoutine = null;
        snapshotValid = false;
        RefreshSeatCards();
    }

    private void OnDisable()
    {
        snapshotValid = false;
        snapshotTable = null;
        HideWaitingPanel();
        ClearRaiseNotification();
        CancelDeal();
        displayedHand = -1;
    }

    private bool CanAct()
    {
        if (isDealing || dealPending || table == null ||
            !table.IsSpawned ||
            !hasLocalPlayer)
            return false;

        PokerStage stage = table.CurrentStage.Value;

        bool activeStage =
            stage == PokerStage.PreFlop ||
            stage == PokerStage.Flop ||
            stage == PokerStage.Turn ||
            stage == PokerStage.River;

        return activeStage &&
               table.TurnDeadline.Value >= 0 &&
               NetworkManager.Singleton.ServerTime.Time < table.TurnDeadline.Value &&
               table.CurrentTurnSeat.Value == localSeat &&
               localPlayer.HasDealtCards &&
               !localPlayer.IsFolded &&
               !localPlayer.IsAllIn &&
               localPlayer.Chips > 0;
    }

    private void RefreshBettingButtons()
    {
        bool canAct = CanAct();
        if (!canAct || Time.unscaledTime >= raiseNotificationUntil)
            ClearRaiseNotification();

        int toCall = Mathf.Max(
            0,
            table.CurrentBet.Value -
            localPlayer.CurrentBet);

        int minimumRaiseTo =
            table.CurrentBet.Value +
            table.MinimumRaise.Value;

        int maximumRaiseTo =
            localPlayer.CurrentBet +
            localPlayer.Chips;

        foldButton.interactable = canAct;

        checkButton.interactable =
            canAct && toCall == 0;

        callButton.interactable =
            canAct && toCall > 0;

        raiseButton.interactable =
            canAct && maximumRaiseTo >= minimumRaiseTo;

        allInButton.interactable =
            canAct && localPlayer.Chips > 0;

        raiseInput.interactable =
            raiseButton.interactable;

        if (halfPotButton != null) halfPotButton.interactable = raiseButton.interactable;
        if (fullPotButton != null) fullPotButton.interactable = raiseButton.interactable;
        if (callButtonText != null)
        {
            int actualCall =
                Mathf.Min(toCall, localPlayer.Chips);

            callButtonText.text = toCall > 0
                ? (toCall >= localPlayer.Chips ? $"CALL {actualCall} (ALL-IN)" : $"CALL {actualCall}")
                : "CALL";
        }

        if (minimumRaiseText != null)
        {
            minimumRaiseText.text =
                $"Min Raise To: {minimumRaiseTo}";
        }
    }

    private void RefreshWinnerPanel()
    {
        bool isShowdown =
            table.CurrentStage.Value ==
            PokerStage.Showdown;

        string result =
            table.HandResultText.Value.ToString();

        bool resultReady =
            isShowdown &&
            !string.IsNullOrEmpty(result);

        if (winnerPanel != null)
        {
            if (winnerPanel.activeSelf != resultReady)
                winnerPanel.SetActive(resultReady);
        }

        if (resultReady)
            winnerText.text = result;

        newHandButton.interactable =
            resultReady && hasLocalPlayer;
    }

    private void SetButtonsInteractable(bool value)
    {
        if (halfPotButton != null) halfPotButton.interactable = value;
        if (fullPotButton != null) fullPotButton.interactable = value;
        foldButton.interactable = value;
        checkButton.interactable = value;
        callButton.interactable = value;
        raiseButton.interactable = value;
        allInButton.interactable = value;
        raiseInput.interactable = value;
        newHandButton.interactable = value;
    }

    private void OnHalfPot() => SetQuickRaise(0.5f);
    private void OnFullPot() => SetQuickRaise(1f);
    private void SetQuickRaise(float fraction)
    {
        if (!CanAct() || !raiseButton.interactable) return;
        int bet = table.CurrentBet.Value;
        int toCall = Mathf.Max(0, bet - localPlayer.CurrentBet);
        int min = bet + table.MinimumRaise.Value;
        int max = localPlayer.CurrentBet + localPlayer.Chips;
        if (max < min) return;
        int target = bet + Mathf.FloorToInt((table.TotalPot.Value + toCall) * fraction);
        raiseInput.text = Mathf.Clamp(target, min, max).ToString();
        ClearRaiseNotification();
    }

    private void RefreshGuidance()
    {
        if (guidanceText == null) return;
        string message;
        if (table.CurrentStage.Value == PokerStage.Waiting) message = "Waiting for at least two players.";
        else if (!hasLocalPlayer) message = "Waiting for a seat.";
        else if (table.CurrentStage.Value == PokerStage.Showdown) message = "Hand complete. Start the next hand when ready.";
        else if (!localPlayer.HasDealtCards) message = "You can join the next hand.";
        else if (localPlayer.IsFolded) message = "You folded. Wait for the next hand.";
        else if (localPlayer.IsAllIn) message = "You are all-in. Wait for the result.";
        else if (isDealing || dealPending || table.TurnDeadline.Value < 0) message = "Dealing cards...";
        else if (!CanAct()) message = "Waiting for another player.";
        else
        {
            int call = Mathf.Max(0, table.CurrentBet.Value - localPlayer.CurrentBet);
            message = call == 0 ? "Your turn: Check for free or bet/raise." :
                $"Your turn: Call {Mathf.Min(call, localPlayer.Chips)} chips or fold.";
            if (call > 0 && raiseButton.interactable) message += " You may also raise.";
        }
        SetWaitingText(guidanceText, message);
    }

    private void OnFold()
    {
        if (CanAct())
            table.SubmitAction(PokerAction.Fold);
    }

    private void OnCheck()
    {
        if (CanAct())
            table.SubmitAction(PokerAction.Check);
    }

    private void OnCall()
    {
        if (CanAct())
            table.SubmitAction(PokerAction.Call);
    }

    private void OnRaise()
    {
        if (!CanAct())
            return;

        int minimumRaiseTo = table.CurrentBet.Value + table.MinimumRaise.Value;
        int maximumRaiseTo = localPlayer.CurrentBet + localPlayer.Chips;

        if (string.IsNullOrWhiteSpace(raiseInput.text))
        {
            ShowRaiseNotification($"Enter a raise amount. Minimum: {minimumRaiseTo} chips.");
            return;
        }
        if (!int.TryParse(raiseInput.text, out int amount))
        {
            ShowRaiseNotification("Enter a valid whole number of chips.");
            return;
        }
        if (amount < minimumRaiseTo)
        {
            ShowRaiseNotification($"Minimum raise to: {minimumRaiseTo} chips.");
            return;
        }
        if (amount > maximumRaiseTo)
        {
            ShowRaiseNotification($"Maximum raise to: {maximumRaiseTo} chips.");
            return;
        }

        ClearRaiseNotification();
        table.SubmitAction(PokerAction.Raise, amount);
    }

    private void ShowRaiseNotification(string message)
    {
        if (raiseNotificationText == null)
        {
            Debug.LogWarning(message);
            return;
        }
        raiseNotificationText.text = message;
        raiseNotificationUntil = Time.unscaledTime + 3f;
    }

    private void ClearRaiseNotification()
    {
        if (raiseNotificationText != null && raiseNotificationText.text.Length > 0)
            raiseNotificationText.text = string.Empty;
        raiseNotificationUntil = 0f;
    }

    private void OnAllIn()
    {
        if (CanAct())
            table.SubmitAction(PokerAction.AllIn);
    }

    private void OnNewHand()
    {
        if (table != null &&
            table.IsSpawned &&
            hasLocalPlayer &&
            table.CurrentStage.Value ==
            PokerStage.Showdown)
        {
            table.RequestNewHand();
        }
    }
}
