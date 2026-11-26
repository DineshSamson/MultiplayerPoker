using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class PokerBreakPopup : MonoBehaviour
{
    [SerializeField] private float duration = 0.22f;
    private CanvasGroup group;
    private Vector3 normalScale;
    private void Awake() { group = GetComponent<CanvasGroup>(); normalScale = transform.localScale; }
    private void OnEnable() { if (PokerAudioUI.Instance != null) PokerAudioUI.Instance.PlayBreak(); StartCoroutine(Animate()); }
    private IEnumerator Animate()
    {
        group.alpha = 0; transform.localScale = normalScale * 0.88f;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float eased = 1 - Mathf.Pow(1-t, 3);
            group.alpha = eased; transform.localScale = normalScale * Mathf.Lerp(0.88f, 1f, eased);
            yield return null;
        }
        group.alpha = 1; transform.localScale = normalScale;
    }
    private void OnDisable() { StopAllCoroutines(); if (group != null) group.alpha = 1; transform.localScale = normalScale; }
}
