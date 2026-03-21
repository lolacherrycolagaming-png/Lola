/// <summary>
/// LOL-11 scoring: cluster size → points.
/// - 2 bubbles = 10
/// - 3 bubbles = 30
/// - 4+ = 50 + 10 per bubble beyond 4 (4→50, 5→60, 6→70, …)
/// Single bubbles (size 1) award 0.
/// </summary>
public static class PoploScoreCalculator
{
    public static int PointsForCluster(int clusterSize)
    {
        if (clusterSize < 2)
            return 0;

        switch (clusterSize)
        {
            case 2:
                return 10;
            case 3:
                return 30;
            default:
                // 4+ : base 50 + 10 per extra bubble after the 4th
                return 50 + 10 * (clusterSize - 4);
        }
    }

    /// <summary>Optional: show breakdown in UI or debug.</summary>
    public static string DescribeRule(int clusterSize)
    {
        int p = PointsForCluster(clusterSize);
        return clusterSize < 2
            ? "No score (need 2+)"
            : $"{clusterSize} bubbles → {p} pts";
    }
}
