using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using System.Collections.Generic;

public class PokerAudioUI : MonoBehaviour
{
    public static PokerAudioUI Instance { get; private set; }
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource effectsSource;
    [SerializeField] private AudioClip buttonClick, timerTick, breakPop;
    [SerializeField] private Toggle musicToggle;
    private const string MusicKey = "PokerMusicEnabled";
    private readonly HashSet<Button> buttons = new HashSet<Button>();
    private ulong lastTurn = ulong.MaxValue;
    private int lastSecond = -1;
    private PokerTableManager lastTable;
    private float nextScan;
    private void Awake()
    {
        Instance = this;
        bool enabled = PlayerPrefs.GetInt(MusicKey, 1) == 1;
        if (musicSource != null) { musicSource.loop = true; musicSource.mute = !enabled; }
        if (musicToggle != null) { musicToggle.SetIsOnWithoutNotify(enabled); musicToggle.onValueChanged.AddListener(SetMusic); }
    }
    private void Start() { ScanButtons(); }
    private void ScanButtons()
    {
        foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (buttons.Add(b)) b.onClick.AddListener(PlayClick);
    }
    public void SetMusic(bool enabled)
    {
        if (musicSource != null) { musicSource.mute = !enabled; if (enabled && !musicSource.isPlaying) musicSource.Play(); }
        PlayerPrefs.SetInt(MusicKey, enabled ? 1 : 0); PlayerPrefs.Save();
    }
    private void Play(AudioClip clip) { if (effectsSource != null && clip != null) effectsSource.PlayOneShot(clip); }
    public void PlayClick() { Play(buttonClick); if (musicSource != null && !musicSource.mute && !musicSource.isPlaying) musicSource.Play(); }
    public void PlayBreak() { Play(breakPop); }
    private void Update()
    {
        if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 2f; ScanButtons(); }
        var t = PokerTableManager.Instance; var n = NetworkManager.Singleton;
        if (t == null || !t.IsSpawned || n == null || !n.IsConnectedClient || t.TurnDeadline.Value < 0) { lastTable = null; lastSecond = -1; return; }
        if (lastTable != t || lastTurn != t.TurnSequenceId.Value) { lastTable = t; lastTurn = t.TurnSequenceId.Value; lastSecond = -1; }
        bool localTurn = false;
        foreach (var p in t.Players) if (p.ClientId == n.LocalClientId && p.SeatIndex == t.CurrentTurnSeat.Value && p.IsConnected && p.HasDealtCards && !p.IsFolded && !p.IsAllIn) { localTurn = true; break; }
        if (!localTurn) return;
        int second = Mathf.CeilToInt((float)(t.TurnDeadline.Value - n.ServerTime.Time));
        if (second != lastSecond) { lastSecond = second; if (second >= 1 && second <= 5) Play(timerTick); }
    }
    private void OnDestroy()
    {
        if (musicToggle != null) musicToggle.onValueChanged.RemoveListener(SetMusic);
        foreach (Button b in buttons) if (b != null) b.onClick.RemoveListener(PlayClick);
        if (Instance == this) Instance = null;
    }
}
