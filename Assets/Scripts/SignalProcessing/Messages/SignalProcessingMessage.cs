using System;

namespace Assets.Scripts.SignalProcessing
{
    public static class MessageTypes
    {
        public const string VitalsSnapshot = "VitalsSnapshot";
        public const string CalibrationStart = "CalibrationStart";
        public const string CalibrationStarted = "CalibrationStarted";
        public const string CalibrationFinish = "CalibrationFinish";
        public const string CalibrationCancel = "CalibrationCancel";
        public const string CalibrationResult = "CalibrationResult";
    }

    [Serializable]
    public class SignalProcessingMessage
    {
        public const int CurrentProtocolVersion = 1;

        public string Type;
        public string RequestId;
        public int ProtocolVersion;

        public VitalSnapshot Vitals;
        public CalibrationStartPayload CalibrationStart;
        public CalibrationStartedPayload CalibrationStarted;
        public CalibrationFinishPayload CalibrationFinish;
        public CalibrationCancelPayload CalibrationCancel;
        public CalibrationResultPayload CalibrationResult;
    }

    [Serializable]
    public class CalibrationStartPayload
    {
        public float RequestedDurationSeconds;
    }

    [Serializable]
    public class CalibrationStartedPayload
    {
        public bool Accepted;
        public string ErrorCode;
        public string Error;
    }

    [Serializable]
    public class CalibrationFinishPayload
    {
        public float ElapsedDurationSeconds;
    }

    [Serializable]
    public class CalibrationCancelPayload
    {
        public string Reason;
    }

    [Serializable]
    public class CalibrationResultPayload
    {
        public bool Success;
        public float BaselineHeartRate;
        public float BaselineRmssd;
        public float BaselineLnRmssd;
        public float CapturedDurationSeconds;
        public int WindowSeconds;
        public int ValidWindowCount;
        public int RejectedWindowCount;
        public float SignalQuality;
        public string ErrorCode;
        public string Error;
    }
}
