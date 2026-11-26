using UnityEngine;
using Unity.Netcode;

[DisallowMultipleComponent]
public class PokerRelativeSeatLayout : MonoBehaviour
{
    [SerializeField] private RectTransform[] seats = new RectTransform[6];
    [SerializeField] private RectTransform[] positions = new RectTransform[6];
    private static readonly int[][] Layouts = {
        new int[0], new[] {0}, new[] {0,3}, new[] {0,1,3},
        new[] {0,1,3,5}, new[] {0,1,2,4,5}, new[] {0,1,2,3,4,5}
    };
    private int lastHero = -2;
    private int lastMask = -1;
    private readonly bool[] used = new bool[6];
    private void OnEnable() { lastHero = -2; lastMask = -1; }

    private void LateUpdate()
    {
        if (seats == null || positions == null || seats.Length != 6 || positions.Length != 6) return;
        for (int i = 0; i < 6; i++)
            if (seats[i] == null || positions[i] == null || seats[i].parent != positions[i].parent) return;

        var manager = NetworkManager.Singleton;
        var table = PokerTableManager.Instance;
        int hero = -1, mask = 0, count = 0;
        if (manager != null && manager.IsConnectedClient && table != null && table.IsSpawned)
        {
            foreach (var player in table.Players)
            {
                int index = player.SeatIndex;
                if (!player.IsConnected || index < 0 || index >= 6) continue;
                if ((mask & (1 << index)) == 0) { mask |= 1 << index; count++; }
                if (player.ClientId == manager.LocalClientId) hero = index;
            }
        }
        if (hero == lastHero && mask == lastMask) return;
        lastHero = hero; lastMask = mask;
        if (hero < 0)
        {
            for (int i = 0; i < 6; i++) Place(i, i);
            return;
        }
        System.Array.Clear(used, 0, used.Length);
        int rank = 0;
        for (int offset = 0; offset < 6; offset++)
        {
            int index = (hero + offset) % 6;
            if ((mask & (1 << index)) == 0) continue;
            int position = Layouts[count][rank++];
            Place(index, position);
            used[position] = true;
        }
        // Keep unused seat objects in unused positions; existing UI owns visibility.
        int next = 0;
        for (int i = 0; i < 6; i++)
        {
            if ((mask & (1 << i)) != 0) continue;
            while (next < 6 && used[next]) next++;
            if (next < 6) { Place(i, next); used[next++] = true; }
        }
    }
    private void Place(int index, int position)
    {
        var seat = seats[index];
        var marker = positions[position];
        seat.anchorMin = marker.anchorMin;
        seat.anchorMax = marker.anchorMax;
        seat.anchoredPosition = marker.anchoredPosition;
    }
}
