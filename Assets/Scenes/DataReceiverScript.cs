using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class DataReceiverScript : MonoBehaviour
{
    [Header("UDP Input")]
    [SerializeField] private int port = 5005;

    [Header("Focus Thresholds")]
    [SerializeField, Range(0f, 1f)] private float hrvThreshold = 0.5f;
    [SerializeField, Range(0f, 1f)] private float eyeGazeThreshold = 0.5f;

    [Header("Consumers")]
    [SerializeField] private AuditoryTraining auditoryTraining;

    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool isRunning;
    private readonly object dataLock = new object();

    private float hrvValue;
    private float eyeGazeValue;
    private bool hasReceivedData;
    private bool isLookingAtOrb;

    public float HrvValue
    {
        get
        {
            lock (dataLock) return hrvValue;
        }
    }

    public float EyeGazeValue
    {
        get
        {
            lock (dataLock) return eyeGazeValue;
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

    public bool IsHrvPassing => HasReceivedData && HrvValue < hrvThreshold;
    public bool IsEyeGazePassing => HasReceivedData && EyeGazeValue > eyeGazeThreshold;
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
        if (IsEyeGazePassing) passingChecks++;
        if (IsLookingAtOrb) passingChecks++;

        return passingChecks;
    }

    public string GetStatusText()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}\n<size=70%>HRV: {1:0.00} {2}\nEye gaze: {3:0.00} {4}\nOrb: {5}</size>",
            IsFocused ? "Focused" : "Not Focused",
            HrvValue,
            IsHrvPassing ? "pass" : "fail",
            EyeGazeValue,
            IsEyeGazePassing ? "pass" : "fail",
            IsLookingAtOrb ? "pass" : "fail");
    }

    private void StartReceiver()
    {
        try
        {
            udpClient = new UdpClient(port);
            isRunning = true;
            receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true
            };
            receiveThread.Start();
            Debug.Log("Data receiver listening on UDP port " + port, this);
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to start data receiver: " + ex.Message, this);
        }
    }

    private void StopReceiver()
    {
        isRunning = false;
        udpClient?.Close();
        udpClient = null;

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(100);
        }
    }

    private void ReceiveLoop()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                byte[] bytes = udpClient.Receive(ref remoteEndPoint);
                string message = Encoding.UTF8.GetString(bytes);
                SensorFrame frame = ParseSensorFrame(message);

                lock (dataLock)
                {
                    hrvValue = Clamp01(frame.hrv);
                    eyeGazeValue = Clamp01(frame.eyeGaze);
                    hasReceivedData = true;
                }
            }
            catch (SocketException)
            {
                if (isRunning)
                {
                    Debug.LogWarning("Socket error while receiving sensor data.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Invalid sensor frame: " + ex.Message);
            }
        }
    }

    private static SensorFrame ParseSensorFrame(string message)
    {
        SensorFrame frame = new SensorFrame();
        string trimmedMessage = message.Trim().Trim('{', '}');
        string[] pairs = trimmedMessage.Split(',');

        foreach (string pair in pairs)
        {
            string[] keyValue = pair.Split(':');
            if (keyValue.Length != 2) continue;

            string key = keyValue[0].Trim().Trim('"');
            string valueText = keyValue[1].Trim();

            if (!float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                continue;
            }

            if (key == "hrv")
            {
                frame.hrv = value;
            }
            else if (key == "eyeGaze")
            {
                frame.eyeGaze = value;
            }
        }

        return frame;
    }

    private static float Clamp01(float value)
    {
        if (value < 0f) return 0f;
        if (value > 1f) return 1f;
        return value;
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

    [Serializable]
    private struct SensorFrame
    {
        public float hrv;
        public float eyeGaze;
    }
}
