
public class PokerPlayer
{
    public int SeatIndex { get; private set; }
    public int Chips { get; set; }

    public Card[] HoleCards { get; private set; }

    public bool IsFolded { get; set; }
    public bool IsAllIn { get; set; }

    public int CurrentBet { get; set; }
    public int TotalContribution { get; set; }

    public bool HasActed { get; set; }
    public int LastFacedBet { get; set; }

    public PokerAction? LastAction { get; set; }

    public PokerPlayer(int seatIndex, int startingChips = 1000)
    {
        SeatIndex = seatIndex;
        Chips = startingChips;
        HoleCards = new Card[2];
    }

    public void SetHoleCards(Card first, Card second)
    {
        HoleCards[0] = first;
        HoleCards[1] = second;
    }

    public void ResetForNewHand()
    {
        IsFolded = false;
        IsAllIn = false;
        CurrentBet = 0;
        TotalContribution = 0;
        HasActed = false;
        LastFacedBet = 0;
        LastAction = null;
        HoleCards = new Card[2];
    }
}
