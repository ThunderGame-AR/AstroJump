using UnityEngine;

public static class GlobalStats
{
    public static float TotalAccumulatedTimeInSeconds = 0f;

    public static void ResetTotalTime()
    {
        TotalAccumulatedTimeInSeconds = 0f;
    }

    public static void AddLevelTime(float levelTime)
    {
        TotalAccumulatedTimeInSeconds += levelTime;
    }
}