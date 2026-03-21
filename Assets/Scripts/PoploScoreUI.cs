using TMPro;
using UnityEngine;

/// <summary>
/// Minimal on-screen score display. Requires TextMeshPro (Unity UI package).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class PoploScoreUI : MonoBehaviour
{
    [SerializeField] private PoploScoreManager scoreManager;

    [SerializeField] private TextMeshProUGUI scoreText;

    private void Awake()
    {
        if (scoreText == null)
            scoreText = GetComponent<TextMeshProUGUI>() ?? GetComponentInChildren<TextMeshProUGUI>(true);
    }

    [SerializeField] private string labelPrefix = "";

    [SerializeField] private string numberFormat = "N0";

    [Tooltip("If true, subscribe in OnEnable (handles manager created later if you reorder).")]
    [SerializeField] private bool findManagerIfNull = true;

    private void OnEnable()
    {
        if (scoreManager == null && findManagerIfNull)
            scoreManager = FindFirstObjectByType<PoploScoreManager>();

        if (scoreManager != null)
        {
            scoreManager.OnTotalScoreChanged += OnScoreChanged;
            OnScoreChanged(scoreManager.TotalScore);
        }
        else
        {
            if (scoreText != null)
                scoreText.text = string.IsNullOrEmpty(labelPrefix) ? "0" : $"{labelPrefix}0";
        }
    }

    private void OnDisable()
    {
        if (scoreManager != null)
            scoreManager.OnTotalScoreChanged -= OnScoreChanged;
    }

    private void OnScoreChanged(int total)
    {
        if (scoreText == null) return;
        string num = total.ToString(numberFormat);
        scoreText.text = string.IsNullOrEmpty(labelPrefix) ? num : $"{labelPrefix}{num}";
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (scoreText == null)
            scoreText = GetComponent<TextMeshProUGUI>();
    }
#endif
}
