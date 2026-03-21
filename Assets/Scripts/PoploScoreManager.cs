using System;
using UnityEngine;

/// <summary>
/// Tracks total score and notifies listeners. Place one instance in the scene (e.g. on a GameSystems object).
/// </summary>
public sealed class PoploScoreManager : MonoBehaviour
{
    public static PoploScoreManager Instance { get; private set; }

    [SerializeField]
    [Tooltip("If true, this object survives scene loads (optional for multi-scene games).")]
    private bool dontDestroyOnLoad;

    public int TotalScore { get; private set; }

    /// <summary>Fired with new total after each cluster pop that awards points.</summary>
    public event Action<int> OnTotalScoreChanged;

    /// <summary>Fired with points earned this pop and new total.</summary>
    public event Action<int, int> OnClusterScored;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Call when a cluster is popped (after you know the final count).</summary>
    public void AddScoreForCluster(int clusterSize)
    {
        int gained = PoploScoreCalculator.PointsForCluster(clusterSize);
        if (gained <= 0)
            return;

        TotalScore += gained;
        OnClusterScored?.Invoke(gained, TotalScore);
        OnTotalScoreChanged?.Invoke(TotalScore);
    }

    public void ResetScore()
    {
        TotalScore = 0;
        OnTotalScoreChanged?.Invoke(TotalScore);
    }
}
