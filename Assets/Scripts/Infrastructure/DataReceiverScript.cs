using Assets.Scripts.SignalProcessing;
using System;
using System.Globalization;
using UnityEngine;

public class DataReceiverScript : MonoBehaviour
{
    [Header("Physiological Load")]
    [SerializeField, Min(0f)] private float loadZThreshold = 1f;

    [Header("Consumers")]
    [SerializeField] private AuditoryTraining auditoryTraining;

    private TcpGameServer<SignalProcessingMessage> tcpServer;
    private readonly object dataLock = new object();

    private VitalSnapshot vitalSnapshot;
    private bool hasReceivedData;
    private bool isLookingAtOrb;

    public event Action<string, CalibrationStartedPayload> CalibrationStartedReceived;
    public event Action<string, CalibrationResultPayload> CalibrationResultReceived;

    public VitalSnapshot CurrentVitals
    {
        get
        {
            lock (dataLock) return vitalSnapshot;
        }
    }

    public bool IsLookingAtOrb
    {
        get
        {
            lock (dataLock) return isLookingAtOrb;
        }
    }

    public bool HasReceivedData
    {
        get
        {
            lock (dataLock) return hasReceivedData;
        }
    }

    public bool IsSignalProcessingConnected =>
        tcpServer != null && tcpServer.IsClientConnected;

    public bool HasCalibratedBaseline => RuntimeBaselineState.IsValid;
    public float BaselineHeartRate => RuntimeBaselineState.HeartRate;
    public float BaselineRmssd => RuntimeBaselineState.Rmssd;
    public float LoadZThreshold => loadZThreshold;
    public bool AreVitalsAvailable => RuntimeBaselineState.IsValid
        && HasValidHeartRate(CurrentVitals)
        && HasValidRmssd(CurrentVitals);

    /**
     * Vitals pass when neither HR nor inverse-lnRMSSD is elevated relative to
     * the participant's calibrated baseline.
     */
    public bool AreVitalsPassing()
    {
        return IsHeartRatePassing() && IsRmssdPassing();
    }

    public bool IsHeartRatePassing()
    {
        VitalSnapshot snapshot = CurrentVitals;

        if (!RuntimeBaselineState.IsValid || !HasValidHeartRate(snapshot))
        {
            return false;
        }

        float heartRateLoadZ = (snapshot.HeartRate - RuntimeBaselineState.HeartRate)
            / RuntimeBaselineState.HeartRateStandardDeviation;
        return heartRateLoadZ < loadZThreshold;
    }

    public bool IsRmssdPassing()
    {
        VitalSnapshot snapshot = CurrentVitals;

        if (!RuntimeBaselineState.IsValid || !HasValidRmssd(snapshot))
        {
            return false;
        }

        float hrvLoadZ = (RuntimeBaselineState.LnRmssd - Mathf.Log(snapshot.RMSSD))
            / RuntimeBaselineState.LnRmssdStandardDeviation;
        return hrvLoadZ < loadZThreshold;
    }

    public bool IsFocused => AreVitalsAvailable
        ? GetPassingCheckCount() >= 2
        : IsLookingAtOrb;

    private void Awake()
    {
        if (auditoryTraining == null)
        {
            auditoryTraining = FindFirstObjectByType<AuditoryTraining>();
        }

        ApplyStoredBaseline();
    }

    private void Start()
    {
        StartReceiver();
    }

    private void OnDestroy()
    {
        StopReceiver();
    }

    public void SetOrbFocus(bool focused)
    {
        lock (dataLock)
        {
            isLookingAtOrb = focused;
        }

        UpdateConsumers();
    }

    public int GetPassingCheckCount()
    {
        int passingChecks = 0;

        if (AreVitalsPassing()) passingChecks++;
        if (IsLookingAtOrb) passingChecks++;

        return passingChecks;
    }

    public string GetStatusText()
    {
        bool isHeartRatePassing = IsHeartRatePassing();
        bool isRmssdPassing = IsRmssdPassing();
        bool isLookingAtOrb = IsLookingAtOrb;

        return string.Format(
            CultureInfo.InvariantCulture,
            "<size=70%><color={0}>HR: {1} {2}</color>\n"
            + "<color={3}>HRV: {4}</color>\n"
            + "<color={5}>Orb: {6}</color></size>",
            isHeartRatePassing ? "green" : "red",
            CurrentVitals == null ? "—" : CurrentVitals.HeartRate.ToString("0"),
            isHeartRatePassing ? "pass" : "fail",
            isRmssdPassing ? "green" : "red",
            isRmssdPassing ? "pass" : "fail",
            isLookingAtOrb ? "green" : "red",
            isLookingAtOrb ? "pass" : "fail");
    }

    public bool TrySendMessage(SignalProcessingMessage message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.Type))
        {
            return false;
        }

        return tcpServer != null && tcpServer.TrySend(message);
    }

    public bool TryStartCalibration(string requestId, float requestedDurationSeconds)
    {
        if (string.IsNullOrWhiteSpace(requestId) || requestedDurationSeconds <= 0f)
        {
            return false;
        }

        return TrySendMessage(new SignalProcessingMessage
        {
            Type = MessageTypes.CalibrationStart,
            RequestId = requestId,
            CalibrationStart = new CalibrationStartPayload
            {
                RequestedDurationSeconds = requestedDurationSeconds
            }
        });
    }

    public bool TryFinishCalibration(string requestId, float elapsedDurationSeconds)
    {
        if (string.IsNullOrWhiteSpace(requestId) || elapsedDurationSeconds < 0f)
        {
            return false;
        }

        return TrySendMessage(new SignalProcessingMessage
        {
            Type = MessageTypes.CalibrationFinish,
            RequestId = requestId,
            CalibrationFinish = new CalibrationFinishPayload
            {
                ElapsedDurationSeconds = elapsedDurationSeconds
            }
        });
    }

    public bool TryCancelCalibration(string requestId, string reason)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return false;
        }

        return TrySendMessage(new SignalProcessingMessage
        {
            Type = MessageTypes.CalibrationCancel,
            RequestId = requestId,
            CalibrationCancel = new CalibrationCancelPayload
            {
                Reason = reason ?? string.Empty
            }
        });
    }

    public bool TryApplyCalibrationResult(CalibrationResultPayload payload)
    {
        if (!RuntimeBaselineState.TryApply(payload))
        {
            return false;
        }

        ApplyStoredBaseline();
        return true;
    }

    private void StartReceiver()
    {
        try
        {
            if (tcpServer != null) return;

            tcpServer = new TcpGameServer<SignalProcessingMessage>();
            tcpServer.InitConnection();
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to start data receiver: " + ex.Message, this);
        }
    }

    private void StopReceiver()
    {
        tcpServer?.CloseSocket();
        tcpServer = null;
    }

    private void Update()
    {
        if (tcpServer != null)
        {
            while (tcpServer.TryGetMessage(out SignalProcessingMessage message))
            {
                RouteMessage(message);
            }
        }

        UpdateConsumers();
    }

    private void RouteMessage(SignalProcessingMessage message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.Type))
        {
            Debug.LogWarning("Received a signal-processing message without a Type.", this);
            return;
        }

        switch (message.Type)
        {
            case MessageTypes.VitalsSnapshot:
                HandleVitals(message.Vitals);
                break;

            case MessageTypes.CalibrationStarted:
                HandleCalibrationStarted(message.RequestId, message.CalibrationStarted);
                break;

            case MessageTypes.CalibrationResult:
                HandleCalibrationResult(message.RequestId, message.CalibrationResult);
                break;

            default:
                Debug.LogWarning($"Unknown signal-processing message type: {message.Type}", this);
                break;
        }
    }

    private void HandleVitals(VitalSnapshot snapshot)
    {
        if (snapshot == null)
        {
            Debug.LogWarning("Received a VitalsSnapshot message without Vitals.", this);
            return;
        }

        lock (dataLock)
        {
            vitalSnapshot = snapshot;
            hasReceivedData = true;
        }

        Debug.Log(
            $"Vitals check: HR={snapshot.HeartRate:F2}, RMSSD={snapshot.RMSSD:F2}, "
            + $"passing={AreVitalsPassing()}",
            this);
    }

    private void HandleCalibrationStarted(
        string requestId,
        CalibrationStartedPayload payload)
    {
        if (payload == null)
        {
            Debug.LogWarning("Received CalibrationStarted without a payload.", this);
            return;
        }

        // Route on Unity's main thread, outside dataLock.
        CalibrationStartedReceived?.Invoke(requestId, payload);
    }

    private void HandleCalibrationResult(
        string requestId,
        CalibrationResultPayload payload)
    {
        if (payload == null)
        {
            Debug.LogWarning("Received CalibrationResult without a payload.", this);
            return;
        }

        // Route on Unity's main thread, outside dataLock.
        CalibrationResultReceived?.Invoke(requestId, payload);
    }

    private void ApplyStoredBaseline()
    {
        if (!RuntimeBaselineState.IsValid) return;

        Debug.Log(string.Format(
            CultureInfo.InvariantCulture,
            "Physiological baseline loaded: HR={0:F2}±{1:F2} bpm, "
            + "lnRMSSD={2:F4}±{3:F4}, RMSSD={4:F2} ms",
            RuntimeBaselineState.HeartRate,
            RuntimeBaselineState.HeartRateStandardDeviation,
            RuntimeBaselineState.LnRmssd,
            RuntimeBaselineState.LnRmssdStandardDeviation,
            RuntimeBaselineState.Rmssd),
            this);
    }

    private static bool IsFinitePositive(float value)
    {
        return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void UpdateConsumers()
    {
        if (auditoryTraining != null)
        {
            auditoryTraining.SetFocus(IsFocused);
        }
    }

    private bool HasValidHeartRate(VitalSnapshot snapshot)
    {
        return HasReceivedData && snapshot != null
            && !float.IsNaN(snapshot.HeartRate) && !float.IsInfinity(snapshot.HeartRate)
            && snapshot.HeartRate > 0f;
    }

    private bool HasValidRmssd(VitalSnapshot snapshot)
    {
        return HasReceivedData && snapshot != null
            && !float.IsNaN(snapshot.RMSSD) && !float.IsInfinity(snapshot.RMSSD)
            && snapshot.RMSSD > 0f;
    }
}
