using Assets.Scripts.SignalProcessing;
using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class DataReceiverScript : MonoBehaviour
{
    [Header("TCP Input")]
    [SerializeField] private int port = 5005;

    [Header("Focus Thresholds")]
    [SerializeField, Range(0f, 1f)] private float hrvThreshold = 0.5f;

    [Header("Consumers")]
    [SerializeField] private AuditoryTraining auditoryTraining;

    private TcpGameServer<VitalSnapshot> tcpServer;
    private readonly object dataLock = new object();

    private VitalSnapshot vitalSnapshot;
    private bool hasReceivedData;
    private bool isLookingAtOrb;

    public VitalSnapshot HrvValue
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

    public bool IsHrvPassing => HasReceivedData && HrvPassingCheck();

    /**
     * Logic to determine if the HRV value is passing the threshold.
     * 
     * TODO: Implement a more sophisticated check based on the HRV value and the threshold.
     */
    private bool HrvPassingCheck()
    {
        return HrvValue.HeartRate <= 100;
    }
    public bool IsFocused => GetPassingCheckCount() >= 2;

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

        if (IsHrvPassing) passingChecks++;
        if (IsLookingAtOrb) passingChecks++;

        return passingChecks;
    }

    public string GetStatusText()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}\n<size=70%>HRV: {1:0.00} {2}\nOrb: {3}</size>",
            IsFocused ? "Focused" : "Not Focused",
            HrvValue == null? "HRV Unknown" : HrvValue.PrintVitals(),
            IsHrvPassing ? "pass" : "fail",
            IsLookingAtOrb ? "pass" : "fail");
    }

    private void StartReceiver()
    {
        try
        {
            if (tcpServer != null) { return; }

            tcpServer = new TcpGameServer<VitalSnapshot>();
            tcpServer.SetOnReceivedCallback(OnVitalSnapshotReceived);
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
        UpdateConsumers();
    }

    private void UpdateConsumers()
    {
        if (auditoryTraining != null)
        {
            auditoryTraining.SetFocus(IsFocused);
        }
    }

    private void OnVitalSnapshotReceived()
    {
        lock (dataLock)
        {
            Debug.Log("Received VitalSnapshot data from TCP server.");
            if (tcpServer.TryGetMessage(out VitalSnapshot snapshot))
            {
                vitalSnapshot = snapshot;

                if (!hasReceivedData)
                {
                    hasReceivedData = true;
                }

            }

        }
    }
}
