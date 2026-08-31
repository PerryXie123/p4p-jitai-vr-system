using Assets.Scripts.SignalProcessing;
using UnityEngine;

public static class RuntimeBaselineState
{
    public static bool IsValid { get; private set; }
    public static float HeartRate { get; private set; }
    public static float HeartRateStandardDeviation { get; private set; }
    public static float Rmssd { get; private set; }
    public static float LnRmssd { get; private set; }
    public static float LnRmssdStandardDeviation { get; private set; }
    public static float SignalQuality { get; private set; }
    public static int ValidWindowCount { get; private set; }

    public static bool TryApply(CalibrationResultPayload result)
    {
        if (result == null || !result.Success
            || !IsFinitePositive(result.BaselineHeartRate)
            || !IsFinitePositive(result.BaselineHeartRateStandardDeviation)
            || !IsFinitePositive(result.BaselineRmssd)
            || float.IsNaN(result.BaselineLnRmssd)
            || float.IsInfinity(result.BaselineLnRmssd)
            || !IsFinitePositive(result.BaselineLnRmssdStandardDeviation)
            || float.IsNaN(result.SignalQuality)
            || float.IsInfinity(result.SignalQuality)
            || result.ValidWindowCount <= 0)
        {
            return false;
        }

        HeartRate = result.BaselineHeartRate;
        HeartRateStandardDeviation = result.BaselineHeartRateStandardDeviation;
        Rmssd = result.BaselineRmssd;
        LnRmssd = result.BaselineLnRmssd;
        LnRmssdStandardDeviation = result.BaselineLnRmssdStandardDeviation;
        SignalQuality = Mathf.Clamp01(result.SignalQuality);
        ValidWindowCount = Mathf.Max(0, result.ValidWindowCount);
        IsValid = true;
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewRun()
    {
        IsValid = false;
        HeartRate = 0f;
        HeartRateStandardDeviation = 0f;
        Rmssd = 0f;
        LnRmssd = 0f;
        LnRmssdStandardDeviation = 0f;
        SignalQuality = 0f;
        ValidWindowCount = 0;
    }

    private static bool IsFinitePositive(float value)
    {
        return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
