
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

public class PokerTableManager : NetworkBehaviour
{
    public static PokerTableManager Instance { get; private set; }

    private const int MaxPlayers = 6;

    [SerializeField, Min(1f)] private float firstHandCountdownSeconds = 15f;
    public NetworkVariable<int> FirstHandCountdown = new NetworkVariable<int>(-1);
    private double firstHandDeadline = -1;
    [SerializeField, Min(5f)] private float turnDurationSeconds = 30f;
    public NetworkVariable<double> TurnDeadline = new NetworkVariable<double>(-1);
    public NetworkVariable<float> TurnDuration = new NetworkVariable<float>(30f);
    private ulong timedTurn = ulong.MaxValue;
    private int timedSeat = -1;

    public NetworkList<PokerPlayerData> Players;
    public NetworkList<FixedString64Bytes> CommunityCards;

    public NetworkVariable<PokerStage> CurrentStage =
        new NetworkVariable<PokerStage>(PokerStage.Waiting);

    public NetworkVariable<int> DealerSeat =
        new NetworkVariable<int>(-1);

    public NetworkVariable<int> SmallBlindSeat =
        new NetworkVariable<int>(-1);

    public NetworkVariable<int> BigBlindSeat =
        new NetworkVariable<int>(-1);

    public NetworkVariable<int> CurrentTurnSeat =
        new NetworkVariable<int>(-1);

    public NetworkVariable<int> TotalPot =
        new NetworkVariable<int>(0);

    public NetworkVariable<int> CurrentBet =
        new NetworkVariable<int>(0);

    public NetworkVariable<int> MinimumRaise =
        new NetworkVariable<int>(20);

    public NetworkVariable<int> HandNumber =
        new NetworkVariable<int>(0);

    // Server-owned revision of the current betting opportunity.
    public NetworkVariable<ulong> TurnSequenceId =
        new NetworkVariable<ulong>(0);

    public NetworkVariable<FixedString512Bytes> HandResultText =
        new NetworkVariable<FixedString512Bytes>(
            new FixedString512Bytes(""));

    private PokerGameEngine gameEngine;
    private readonly Dictionary<int, ulong> dealtClients =
        new Dictionary<int, ulong>();
    private bool revealShowdownCards;
    public int PrivateCardsHandNumber { get; private set; } = -1;

    public bool HasPrivateCards { get; private set; }
    public Card PrivateCard1 { get; private set; }
    public Card PrivateCard2 { get; private set; }

    private void Awake()
    {
        Instance = this;
        Players = new NetworkList<PokerPlayerData>();
        CommunityCards = new NetworkList<FixedString64Bytes>();
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
        ClearLocalCards();

        Players.OnListChanged += OnPlayersChanged;
        CurrentStage.OnValueChanged += OnStageChanged;
        TotalPot.OnValueChanged += OnPotChanged;
        CurrentTurnSeat.OnValueChanged += OnTurnChanged;
        CommunityCards.OnListChanged += OnCommunityCardsChanged;

        if (IsServer)
        {
            // A new server session must not retain the previous session's
            // replicated state when the scene is reused in Play Mode.
            timedTurn = ulong.MaxValue;
            timedSeat = -1;
            TurnDeadline.Value = -1;
            TurnDuration.Value = Mathf.Max(5f, turnDurationSeconds);
            gameEngine = new PokerGameEngine();
            dealtClients.Clear();
            revealShowdownCards = false;
            Players.Clear();
            CommunityCards.Clear();
            HandNumber.Value = 0;
            TurnSequenceId.Value = 0;
            HandResultText.Value = new FixedString512Bytes("");
            firstHandDeadline = -1;
            FirstHandCountdown.Value = -1;
            SyncGameState();

            NetworkManager.OnClientConnectedCallback +=
                OnClientConnected;

            NetworkManager.OnClientDisconnectCallback +=
                OnClientDisconnected;

            foreach (ulong id in NetworkManager.ConnectedClientsIds)
                AddPlayer(id);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[SERVER] Poker engine initialized.");
#endif
            SyncGameState();
        }

        PrintGameState();
    }

    public override void OnNetworkDespawn()
    {
        Players.OnListChanged -= OnPlayersChanged;
        CurrentStage.OnValueChanged -= OnStageChanged;
        TotalPot.OnValueChanged -= OnPotChanged;
        CurrentTurnSeat.OnValueChanged -= OnTurnChanged;
        CommunityCards.OnListChanged -= OnCommunityCardsChanged;

        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -=
                OnClientConnected;

            NetworkManager.OnClientDisconnectCallback -=
                OnClientDisconnected;
        }

        ClearLocalCards();
        dealtClients.Clear();
        revealShowdownCards = false;
        gameEngine = null;
        firstHandDeadline = -1;

        if (Instance == this)
            Instance = null;
    }

    private void OnClientConnected(ulong clientId)
    {
        AddPlayer(clientId);
    }

    private void AddPlayer(ulong clientId)
    {
        if (!IsServer)
            return;

        foreach (var player in Players)
        {
            if (player.ClientId == clientId)
                return;
        }

        if (Players.Count >= MaxPlayers)
            return;

        int seat = FindAvailableSeat();

        if (seat < 0)
            return;

        Players.Add(new PokerPlayerData(clientId, seat));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[SERVER] Client {clientId} joined Seat {seat + 1}");
#endif

        if (gameEngine != null)
        {
            if (gameEngine.Stage == PokerStage.Waiting ||
                (gameEngine.Stage == PokerStage.Showdown &&
                 gameEngine.IsPotSettled))
            {
                gameEngine.Players.RemoveAll(p => p.SeatIndex == seat);
                gameEngine.AddPlayer(seat);
            }
            else
            {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log(
                    $"[SERVER] Seat {seat + 1} waiting for next hand.");
#endif
            }
        }

        TryStartFirstHand();
    }

    private int FindAvailableSeat()
    {
        for (int seat = 0; seat < MaxPlayers; seat++)
        {
            bool occupied = false;

            foreach (var player in Players)
            {
                if (player.SeatIndex == seat)
                {
                    occupied = true;
                    break;
                }
            }

            if (!occupied && (gameEngine.Stage == PokerStage.Waiting ||
                gameEngine.IsPotSettled || !gameEngine.Players.Exists(p => p.SeatIndex == seat)))
                return seat;
        }

        return -1;
    }

    private void Update()
    {
        if (IsSpawned && IsServer && gameEngine != null &&
            gameEngine.Stage == PokerStage.Waiting)
            TryStartFirstHand();
        if (IsSpawned && IsServer && gameEngine != null) TryExpireTurn();
    }

    private void TryExpireTurn()
    {
        if (TurnDeadline.Value < 0 || NetworkManager.ServerTime.Time < TurnDeadline.Value ||
            gameEngine.Betting == null || gameEngine.IsPotSettled) return;
        int seat = CurrentTurnSeat.Value;
        if (seat < 0 || gameEngine.Betting.CurrentTurnSeat != seat) return;
        if (gameEngine.Betting.TryAction(seat, PokerAction.Fold, 0, out string error))
        {
            TurnSequenceId.Value++;
            AdvanceAndSettle();
            SyncGameState();
        }
        else
        {
            Debug.LogWarning("[SERVER] Timeout fold rejected: " + error);
            TurnDeadline.Value = -1;
        }
    }

    private void SyncTurnTimer()
    {
        int seat = CurrentTurnSeat.Value;
        if (seat < 0 || gameEngine.Stage == PokerStage.Waiting || gameEngine.Stage == PokerStage.Showdown)
        {
            TurnDeadline.Value = -1;
            timedSeat = -1;
            return;
        }
        if (timedSeat != seat || timedTurn != TurnSequenceId.Value)
        {
            timedSeat = seat;
            timedTurn = TurnSequenceId.Value;
            TurnDeadline.Value = NetworkManager.ServerTime.Time + TurnDuration.Value;
        }
    }

    private void TryStartFirstHand()
    {
        if (!IsSpawned || !IsServer || gameEngine == null ||
            gameEngine.Stage != PokerStage.Waiting)
            return;

        int count = gameEngine.Players.Count;
        if (count < 2)
        {
            firstHandDeadline = -1;
            FirstHandCountdown.Value = -1;
            return;
        }

        double now = Time.realtimeSinceStartupAsDouble;
        if (firstHandDeadline < 0)
            firstHandDeadline = now + Mathf.Max(1f, firstHandCountdownSeconds);

        if (count >= MaxPlayers || now >= firstHandDeadline)
        {
            firstHandDeadline = -1;
            FirstHandCountdown.Value = -1;
            StartNewHandInternal();
            return;
        }

        int remaining = Mathf.CeilToInt((float)(firstHandDeadline - now));
        if (FirstHandCountdown.Value != remaining)
            FirstHandCountdown.Value = remaining;
    }

    public void RequestNewHand()
    {
        if (!IsSpawned || !IsClient)
            return;

        RequestNewHandServerRpc(HandNumber.Value);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestNewHandServerRpc(
        int requestedHand,
        ServerRpcParams rpcParams = default)
    {
        if (!IsServer || gameEngine == null)
            return;

        if (requestedHand != HandNumber.Value)
            return;

        if (FindSeatForClient(
            rpcParams.Receive.SenderClientId) < 0)
            return;

        if (gameEngine.Stage != PokerStage.Showdown ||
            !gameEngine.IsPotSettled)
            return;

        if (!CanStartNextHand())
        {
            Debug.LogWarning(
                "[SERVER] Cannot start next hand.");
            return;
        }

        StartNewHandInternal();
    }

    private bool CanStartNextHand()
    {
        if (Players.Count < 2)
            return false;

        // Register late arrivals after the previous hand is settled.
        int eligible = 0;
        foreach (PokerPlayerData networkPlayer in Players)
        {
            if (!networkPlayer.IsConnected)
                continue;

            PokerPlayer enginePlayer = gameEngine.Players.Find(
                p => p.SeatIndex == networkPlayer.SeatIndex);

            if (enginePlayer == null)
            {
                gameEngine.AddPlayer(networkPlayer.SeatIndex, networkPlayer.Chips);
                enginePlayer = gameEngine.Players.Find(
                    p => p.SeatIndex == networkPlayer.SeatIndex);
            }

            if (enginePlayer != null && enginePlayer.Chips > 0)
                eligible++;
        }

        return eligible >= 2;
    }

    private void StartNewHandInternal()
    {
        if (!IsServer || gameEngine == null)
            return;

        gameEngine.Players.RemoveAll(p => !HasConnectedSeat(p.SeatIndex));
        ClearPrivateCardsClientRpc();

        HandResultText.Value = new FixedString512Bytes("");

        revealShowdownCards = false;
        dealtClients.Clear();
        gameEngine.StartHand();

        foreach (PokerPlayerData networkPlayer in Players)
        {
            PokerPlayer player = gameEngine.Players.Find(
                p => p.SeatIndex == networkPlayer.SeatIndex);
            if (networkPlayer.IsConnected && HasValidHoleCards(player))
                dealtClients[networkPlayer.SeatIndex] = networkPlayer.ClientId;
        }

        HandNumber.Value++;
        TurnSequenceId.Value++;

        AdvanceAndSettle();
        SyncGameState();
        SendPrivateCardsToPlayers();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[SERVER] ===== HAND {HandNumber.Value} STARTED =====");
#endif

        PrintGameState();
    }

    [ClientRpc]
    private void ClearPrivateCardsClientRpc()
    {
        ClearLocalCards();
    }

    private void ClearLocalCards()
    {
        HasPrivateCards = false;
        PrivateCardsHandNumber = -1;
        PrivateCard1 = default;
        PrivateCard2 = default;
    }

    private static bool HasValidHoleCards(PokerPlayer player)
    {
        if (player == null || player.HoleCards == null ||
            player.HoleCards.Length < 2)
            return false;
        for (int i = 0; i < 2; i++)
        {
            Card card = player.HoleCards[i];
            if ((int)card.Rank < 2 || (int)card.Rank > 14 ||
                (int)card.Suit < 0 || (int)card.Suit > 3)
                return false;
        }
        return true;
    }

    private bool WasDealt(PokerPlayerData player)
    {
        return dealtClients.TryGetValue(player.SeatIndex, out ulong id)
            && id == player.ClientId;
    }

    private void SendPrivateCardsToPlayers()
    {
        if (!IsServer || gameEngine == null)
            return;

        foreach (PokerPlayerData networkPlayer in Players)
        {
            PokerPlayer player = gameEngine.Players.Find(
                p => p.SeatIndex == networkPlayer.SeatIndex);

            if (!WasDealt(networkPlayer) || !HasValidHoleCards(player))
                continue;

            Card first = player.HoleCards[0];
            Card second = player.HoleCards[1];

            if ((int)first.Rank < 2 ||
                (int)second.Rank < 2)
                continue;

            ClientRpcParams target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[]
                    {
                        networkPlayer.ClientId
                    }
                }
            };

            ReceivePrivateCardsClientRpc(
                HandNumber.Value,
                (int)first.Suit,
                (int)first.Rank,
                (int)second.Suit,
                (int)second.Rank,
                target);
        }
    }

    [ClientRpc]
    private void ReceivePrivateCardsClientRpc(
        int handNumber,
        int suit1,
        int rank1,
        int suit2,
        int rank2,
        ClientRpcParams rpcParams = default)
    {
        if (suit1 < 0 || suit1 > 3 ||
            suit2 < 0 || suit2 > 3 ||
            rank1 < 2 || rank1 > 14 ||
            rank2 < 2 || rank2 > 14)
        {
            Debug.LogError("[CLIENT] Invalid private cards.");
            return;
        }

        PrivateCard1 = new Card(
            (CardSuit)suit1, (CardRank)rank1);

        PrivateCard2 = new Card(
            (CardSuit)suit2, (CardRank)rank2);

        PrivateCardsHandNumber = handNumber;
        HasPrivateCards = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[CLIENT] Private cards received.");
#endif
    }

    public void SubmitAction(PokerAction action, int raiseTo = 0)
    {
        if (!IsSpawned || !IsClient)
            return;

        SubmitActionServerRpc(action, raiseTo,
            HandNumber.Value, TurnSequenceId.Value);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitActionServerRpc(
        PokerAction action,
        int raiseTo,
        int requestedHand,
        ulong requestedTurn,
        ServerRpcParams rpcParams = default)
    {
        if (!IsServer || gameEngine == null)
            return;

        if (requestedHand != HandNumber.Value ||
            requestedTurn != TurnSequenceId.Value)
        {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[SERVER] Stale or duplicate action rejected.");
#endif
            return;
        }

        int seat = FindSeatForClient(
            rpcParams.Receive.SenderClientId);

        if (seat < 0 || !dealtClients.TryGetValue(seat, out ulong dealtClient) ||
            dealtClient != rpcParams.Receive.SenderClientId)
            return;

        if (gameEngine.Stage != PokerStage.PreFlop &&
            gameEngine.Stage != PokerStage.Flop &&
            gameEngine.Stage != PokerStage.Turn &&
            gameEngine.Stage != PokerStage.River)
            return;

        if (gameEngine.Betting == null ||
            gameEngine.Betting.CurrentTurnSeat != seat)
        {
            Debug.LogWarning(
                $"[SERVER] Out-of-turn action: Seat {seat + 1}");
            return;
        }

        if (TurnDeadline.Value >= 0 && NetworkManager.ServerTime.Time >= TurnDeadline.Value)
        {
            TryExpireTurn();
            return;
        }

        bool success = gameEngine.Betting.TryAction(
            seat, action, raiseTo, out string error);

        if (!success)
        {
            Debug.LogWarning(
                $"[SERVER] Action rejected: {error}");
            return;
        }

        // Consume the token only after a valid action.
        TurnSequenceId.Value++;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[SERVER] Seat {seat + 1} performed {action}");
#endif

        AdvanceAndSettle();
        SyncGameState();
        PrintGameState();
    }

    private void AdvanceAndSettle()
    {
        int safety = 0;

        while (gameEngine.Stage != PokerStage.Showdown &&
               safety < 5)
        {
            safety++;

            int potBeforeAdvance = gameEngine.Pot;

            if (!gameEngine.TryAdvanceBettingRound())
                break;

            if (gameEngine.Stage == PokerStage.Showdown &&
                gameEngine.IsPotSettled)
            {
                PublishFoldWinner(potBeforeAdvance);
                break;
            }
        }

        if (gameEngine.Stage == PokerStage.Showdown &&
            !gameEngine.IsPotSettled)
        {
            PotSettlement settlement = gameEngine.SettlePot();

            // Only a contested, completed board may reveal hole cards.
            int contenders = 0;
            foreach (PokerPlayer player in gameEngine.Players)
                if (!player.IsFolded && HasValidHoleCards(player))
                    contenders++;
            revealShowdownCards = gameEngine.CommunityCards.Count == 5
                && contenders >= 2;

            PublishShowdownResult(settlement);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[SERVER] Showdown settled.");
#endif
        }

    }

    private void PublishFoldWinner(int amount)
    {
        PokerPlayer winner = null;

        foreach (PokerPlayer player in gameEngine.Players)
        {
            if (!player.IsFolded && HasValidHoleCards(player))
            {
                winner = player;
                break;
            }
        }

        if (winner == null)
            return;

        string message =
            $"Winner: Seat {winner.SeatIndex + 1}\n" +
            "Result: Other players folded\n" +
            $"Pot Awarded: {amount} chips";

        HandResultText.Value =
            new FixedString512Bytes(message);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[SERVER] " + message);
#endif
    }

    private void PublishShowdownResult(PotSettlement settlement)
    {
        StringBuilder result = new StringBuilder();

        if (settlement.Payouts.Count == 0)
        {
            result.Append("No pot payouts recorded.");
        }
        else
        {
            result.AppendLine(
                settlement.Payouts.Count == 1
                    ? "SHOWDOWN WINNER"
                    : "SHOWDOWN PAYOUTS");

            List<int> seats =
                new List<int>(settlement.Payouts.Keys);

            seats.Sort();

            foreach (int seat in seats)
            {
                int payout = settlement.Payouts[seat];

                if (payout <= 0)
                    continue;

                PokerPlayer player = gameEngine.Players.Find(
                    p => p.SeatIndex == seat);

                if (player == null)
                    continue;

                List<Card> cards =
                    new List<Card>(gameEngine.CommunityCards);

                cards.AddRange(player.HoleCards);

                HandResult hand = HandEvaluator.Evaluate(cards);

                result.AppendLine(
                    $"Seat {seat + 1}: {FormatHandRank(hand.Rank)}");

                result.AppendLine(
                    $"Pot Awarded: {payout} chips");
            }
        }

        if (settlement.Refunds.Count > 0)
        {
            List<int> refundSeats =
                new List<int>(settlement.Refunds.Keys);

            refundSeats.Sort();

            foreach (int seat in refundSeats)
            {
                int refund = settlement.Refunds[seat];

                if (refund > 0)
                {
                    result.AppendLine(
                        $"Seat {seat + 1} Refund: {refund}");
                }
            }
        }

        string message = result.ToString().TrimEnd();

        HandResultText.Value =
            new FixedString512Bytes(message);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[SERVER] " + message);
#endif
    }

    private string FormatHandRank(HandRank rank)
    {
        switch (rank)
        {
            case HandRank.HighCard: return "High Card";
            case HandRank.OnePair: return "One Pair";
            case HandRank.TwoPair: return "Two Pair";
            case HandRank.ThreeOfAKind: return "Three of a Kind";
            case HandRank.Straight: return "Straight";
            case HandRank.Flush: return "Flush";
            case HandRank.FullHouse: return "Full House";
            case HandRank.FourOfAKind: return "Four of a Kind";
            case HandRank.StraightFlush: return "Straight Flush";
            case HandRank.RoyalFlush: return "Royal Flush";
            default: return rank.ToString();
        }
    }

    private int FindSeatForClient(ulong clientId)
    {
        foreach (PokerPlayerData player in Players)
        {
            if (player.ClientId == clientId &&
                player.IsConnected)
                return player.SeatIndex;
        }

        return -1;
    }

    private void SyncGameState()
    {
        if (!IsServer || gameEngine == null)
            return;

        CurrentStage.Value = gameEngine.Stage;
        DealerSeat.Value = gameEngine.DealerSeat;
        SmallBlindSeat.Value = gameEngine.SmallBlindSeat;
        BigBlindSeat.Value = gameEngine.BigBlindSeat;
        TotalPot.Value = gameEngine.Pot;

        CurrentTurnSeat.Value =
            gameEngine.IsPotSettled ||
            gameEngine.Stage == PokerStage.Showdown ||
            gameEngine.Betting == null
                ? -1
                : gameEngine.Betting.CurrentTurnSeat;

        SyncTurnTimer();

        CurrentBet.Value =
            gameEngine.Betting == null ||
            gameEngine.Stage == PokerStage.Showdown
                ? 0
                : gameEngine.Betting.CurrentBet;

        MinimumRaise.Value =
            gameEngine.Betting == null
                ? PokerGameEngine.BigBlind
                : gameEngine.Betting.MinimumRaise;

        // Synchronize ALL public player betting information.
        for (int i = 0; i < Players.Count; i++)
        {
            PokerPlayerData data = Players[i];

            PokerPlayer player = gameEngine.Players.Find(
                p => p.SeatIndex == data.SeatIndex);

            data.HasDealtCards = WasDealt(data) && HasValidHoleCards(player);
            data.RevealedCard1 = default;
            data.RevealedCard2 = default;

            if (!data.HasDealtCards || player == null)
            {
                if (player != null && data.IsConnected)
                    data.Chips = player.Chips;
                data.CurrentBet = 0;
                data.TotalContribution = 0;
                data.IsFolded = false;
                data.IsAllIn = false;
                data.HasActed = false;
                data.LastAction = -1;
                if (!data.Equals(Players[i])) Players[i] = data;
                continue;
            }

            if (revealShowdownCards && gameEngine.Stage == PokerStage.Showdown
                && !player.IsFolded)
            {
                data.RevealedCard1 = new FixedString64Bytes(player.HoleCards[0].ToString());
                data.RevealedCard2 = new FixedString64Bytes(player.HoleCards[1].ToString());
            }

            data.Chips = player.Chips;
            data.CurrentBet = player.CurrentBet;
            data.TotalContribution = player.TotalContribution;
            data.IsFolded = player.IsFolded;
            data.IsAllIn = player.IsAllIn;
            data.HasActed = player.HasActed;
            data.LastAction = player.LastAction.HasValue
                ? (int)player.LastAction.Value
                : -1;

            // NetworkList only notifies clients when
            // the struct actually changes.
            if (!data.Equals(Players[i]))
                Players[i] = data;
        }

        bool changed =
            CommunityCards.Count != gameEngine.CommunityCards.Count;

        if (!changed)
        {
            for (int i = 0; i < CommunityCards.Count; i++)
            {
                if (CommunityCards[i].ToString() !=
                    gameEngine.CommunityCards[i].ToString())
                {
                    changed = true;
                    break;
                }
            }
        }

        if (changed)
        {
            CommunityCards.Clear();

            foreach (Card card in gameEngine.CommunityCards)
            {
                CommunityCards.Add(
                    new FixedString64Bytes(card.ToString()));
            }
        }
    }

    private bool HasConnectedSeat(int seat)
    {
        foreach (PokerPlayerData player in Players)
            if (player.SeatIndex == seat && player.IsConnected)
                return true;
        return false;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer || gameEngine == null)
            return;

        int seat = FindSeatForClient(clientId);
        if (seat < 0) return;

        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].ClientId != clientId) continue;
            Players.RemoveAt(i);
            break;
        }

        // No seated players remain: discard the previous table session.
        if (Players.Count == 0)
        {
            ResetEmptyTable();
            return;
        }

        if (gameEngine.Stage == PokerStage.Waiting)
        {
            gameEngine.Players.RemoveAll(p => p.SeatIndex == seat);
            TryStartFirstHand();
        }
        else if (!gameEngine.IsPotSettled && gameEngine.Betting != null)
        {
            // Keep contributions and all-in eligibility until settlement.
            int previousTurn = gameEngine.Betting.CurrentTurnSeat;
            PokerStage previousStage = gameEngine.Stage;
            gameEngine.Betting.FoldDisconnectedPlayer(seat);
            AdvanceAndSettle();
            if (previousStage != gameEngine.Stage ||
                previousTurn != gameEngine.Betting.CurrentTurnSeat)
                TurnSequenceId.Value++;
        }

        SyncGameState();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SERVER] Seat {seat + 1} disconnected.");
#endif
    }

    private void ResetEmptyTable()
    {
        gameEngine = new PokerGameEngine();
        dealtClients.Clear();
        revealShowdownCards = false;
        ClearLocalCards();
        HandNumber.Value = 0;
        // Keep the action revision moving forward across table resets.
        TurnSequenceId.Value++;
        HandResultText.Value = new FixedString512Bytes("");
        firstHandDeadline = -1;
        FirstHandCountdown.Value = -1;
        SyncGameState();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[SERVER] Empty poker table reset.");
#endif
    }

    private void OnPlayersChanged(
        NetworkListEvent<PokerPlayerData> changeEvent)
    {
        PrintPlayers();
    }

    private void OnStageChanged(
        PokerStage previous, PokerStage current)
    {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NETWORK] Stage: {current}");
#endif
    }

    private void OnPotChanged(int previous, int current)
    {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NETWORK] Pot: {current}");
#endif
    }

    private void OnTurnChanged(int previous, int current)
    {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            current < 0
                ? "[NETWORK] Current Turn: None"
                : $"[NETWORK] Current Turn: Seat {current + 1}");
#endif
    }

    private void OnCommunityCardsChanged(
        NetworkListEvent<FixedString64Bytes> changeEvent)
    {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[NETWORK] Community Cards: {CommunityCards.Count}");
#endif
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void PrintPlayers()
    {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"Poker Table: {Players.Count} players");
#endif

        foreach (var player in Players)
        {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"Seat {player.SeatIndex + 1} | " +
                $"Client {player.ClientId} | " +
                $"Chips {player.Chips} | " +
                $"Bet {player.CurrentBet} | " +
                $"Folded {player.IsFolded} | " +
                $"AllIn {player.IsAllIn}");
#endif
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void PrintGameState()
    {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("===== SYNCHRONIZED GAME STATE =====");
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"Hand: {HandNumber.Value}");
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"Stage: {CurrentStage.Value}");
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"Dealer: {DealerSeat.Value + 1}");
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"Small Blind: {SmallBlindSeat.Value + 1}");
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"Big Blind: {BigBlindSeat.Value + 1}");
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"Pot: {TotalPot.Value}");
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"Current Bet: {CurrentBet.Value}");
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"Minimum Raise: {MinimumRaise.Value}");
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            CurrentTurnSeat.Value < 0
                ? "Current Turn: None"
                : $"Current Turn: {CurrentTurnSeat.Value + 1}");
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"Community Cards: {CommunityCards.Count}");
#endif

        PrintPlayers();
    }
}
