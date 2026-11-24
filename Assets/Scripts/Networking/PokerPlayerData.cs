
using System;
using Unity.Netcode;
using Unity.Collections;

public struct PokerPlayerData :
    INetworkSerializable, IEquatable<PokerPlayerData>
{
    public ulong ClientId;
    public int SeatIndex;
    public int Chips;
    public bool IsConnected;

    public int CurrentBet;
    public int TotalContribution;
    public bool IsFolded;
    public bool IsAllIn;
    public bool HasActed;
    public bool HasDealtCards;
    // Empty until the server authorizes a contested showdown reveal.
    public FixedString64Bytes RevealedCard1;
    public FixedString64Bytes RevealedCard2;

    // -1 means no action yet.
    public int LastAction;

    public PokerPlayerData(ulong clientId, int seatIndex)
    {
        ClientId = clientId;
        SeatIndex = seatIndex;
        Chips = 1000;
        IsConnected = true;

        CurrentBet = 0;
        TotalContribution = 0;
        IsFolded = false;
        IsAllIn = false;
        HasActed = false;
        HasDealtCards = false;
        RevealedCard1 = default;
        RevealedCard2 = default;
        LastAction = -1;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref SeatIndex);
        serializer.SerializeValue(ref Chips);
        serializer.SerializeValue(ref IsConnected);
        serializer.SerializeValue(ref CurrentBet);
        serializer.SerializeValue(ref TotalContribution);
        serializer.SerializeValue(ref IsFolded);
        serializer.SerializeValue(ref IsAllIn);
        serializer.SerializeValue(ref HasActed);
        serializer.SerializeValue(ref HasDealtCards);
        serializer.SerializeValue(ref RevealedCard1);
        serializer.SerializeValue(ref RevealedCard2);
        serializer.SerializeValue(ref LastAction);
    }

    public bool Equals(PokerPlayerData other)
    {
        return ClientId == other.ClientId
            && SeatIndex == other.SeatIndex
            && Chips == other.Chips
            && IsConnected == other.IsConnected
            && CurrentBet == other.CurrentBet
            && TotalContribution == other.TotalContribution
            && IsFolded == other.IsFolded
            && IsAllIn == other.IsAllIn
            && HasActed == other.HasActed
            && HasDealtCards == other.HasDealtCards
            && RevealedCard1.Equals(other.RevealedCard1)
            && RevealedCard2.Equals(other.RevealedCard2)
            && LastAction == other.LastAction;
    }
}
