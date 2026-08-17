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

    private TcpGameServer<VitalSnapshot> tcpServer;
    private readonly object dataLock = new object();

    private VitalSnapshot vitalSnapshot;
    private bool hasReceivedData;
    private bool isLookingAtOrb;

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

    private void StartReceiver()
    {
        try
        {
            if (tcpServer != null) { return; }

            tcpServer = new TcpGameServer<VitalSnapshot>();
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
    }


    private void Update()
    {
        if (tcpServer != null)
        {
            lock (dataLock)
            {
                while (tcpServer.TryGetMessage(out VitalSnapshot snapshot))
                {
                    vitalSnapshot = snapshot;
                    hasReceivedData = true;
                }
            }
        }

        UpdateConsumers();
    }

    private void UpdateConsumers()
    {
        if (auditoryTraining != null)
        {
            auditoryTraining.SetFocus(IsFocused);
        }
    }
}
