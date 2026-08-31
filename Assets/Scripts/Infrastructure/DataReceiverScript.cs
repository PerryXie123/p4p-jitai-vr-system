using Assets.Scripts.SignalProcessing;
using System;
using System.Globalization;
using UnityEngine;

public class DataReceiverScript : MonoBehaviour
{
    [Header("Focus Thresholds")]
    [SerializeField, Min(1f)] private float baselineHeartRate = 70f;
    [SerializeField, Min(0.1f)] private float baselineRmssd = 40f;
    [SerializeField, Min(0f)] private float allowedHeartRateIncrease = 10f;
    [SerializeField, Range(0f, 1f)] private float hrvThreshold = 0.9f;

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
    public float BaselineHeartRate => baselineHeartRate;
    public float BaselineRmssd => baselineRmssd;

    /**
     * Vitals pass when heart rate is no more than the configured amount above
     * the participant's calm baseline and RMSSD remains at or above the
     * configured proportion of its calm baseline.
     */
    public bool AreVitalsPassing()
    {
        VitalSnapshot snapshot = CurrentVitals;

        if (!HasReceivedData || snapshot == null
            || float.IsNaN(snapshot.HeartRate) || float.IsInfinity(snapshot.HeartRate)
            || float.IsNaN(snapshot.RMSSD) || float.IsInfinity(snapshot.RMSSD)
            || snapshot.HeartRate <= 0f || snapshot.RMSSD <= 0f)
        {
            return false;
        }

        bool isHeartRatePassing =
            snapshot.HeartRate <= baselineHeartRate + allowedHeartRateIncrease;
        bool isRmssdPassing =
            snapshot.RMSSD >= baselineRmssd * hrvThreshold;

        return isHeartRatePassing && isRmssdPassing;
    }

    public bool IsFocused => GetPassingCheckCount() >= 2;
    public float HrvThreshold => hrvThreshold;

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
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}\n<size=70%>HR: {1} {2}\nOrb: {3}</size>",
            IsFocused ? "Focused" : "Not Focused",
            CurrentVitals == null ? "—" : CurrentVitals.HeartRate.ToString("0"),
            AreVitalsPassing() ? "pass" : "fail",
            IsLookingAtOrb ? "pass" : "fail");
    }

    public bool TrySendMessage(SignalProcessingMessage message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.Type))
        {
            return false;
        }

        message.ProtocolVersion = SignalProcessingMessage.CurrentProtocolVersion;
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

        if (message.ProtocolVersion != SignalProcessingMessage.CurrentProtocolVersion)
        {
            Debug.LogWarning(
                $"Ignoring signal-processing protocol version {message.ProtocolVersion}.",
                this);
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

        baselineHeartRate = RuntimeBaselineState.HeartRate;
        baselineRmssd = RuntimeBaselineState.Rmssd;
    }

    private void UpdateConsumers()
    {
        if (auditoryTraining != null)
        {
            auditoryTraining.SetFocus(IsFocused);
        }
    }
}
