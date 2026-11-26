using UnityEngine;
using UnityEngine.UI;

public class PokerMusicButton : MonoBehaviour
{
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private Image musicIcon;
    [SerializeField] private Sprite musicOnSprite;
    [SerializeField] private Sprite musicOffSprite;

    private bool musicEnabled;

    private void Start()
    {
        musicEnabled = PlayerPrefs.GetInt(
            "PokerMusicEnabled", 1) == 1;

        ApplyMusicState();
    }

    public void ToggleMusic()
    {
        musicEnabled = !musicEnabled;

        PlayerPrefs.SetInt(
            "PokerMusicEnabled", musicEnabled ? 1 : 0);

        PlayerPrefs.Save();

        ApplyMusicState();
    }

    private void ApplyMusicState()
    {
        musicSource.mute = !musicEnabled;

        musicIcon.sprite = musicEnabled
            ? musicOnSprite
            : musicOffSprite;
    }
}