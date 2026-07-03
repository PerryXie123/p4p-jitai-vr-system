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

    private TcpListener tcpListener;
    private TcpClient tcpClient;
    private Thread receiveThread;
    private volatile bool isRunning;
    private readonly object dataLock = new object();

    private float hrvValue;
    private bool hasReceivedData;
    private bool isLookingAtOrb;

    public float HrvValue
    {
        get
        {
            lock (dataLock) return hrvValue;
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
            HrvValue,
            IsHrvPassing ? "pass" : "fail",
            IsLookingAtOrb ? "pass" : "fail");
    }

    private void StartReceiver()
    {
        try
        {
            tcpListener = new TcpListener(IPAddress.Any, port);
            tcpListener.Start();
            isRunning = true;
            receiveThread = new Thread(ListenLoop)
            {
                IsBackground = true
            };
            receiveThread.Start();
            Debug.Log("Data receiver listening on TCP port " + port, this);
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to start data receiver: " + ex.Message, this);
        }
    }

    private void StopReceiver()
    {
        isRunning = false;
        tcpClient?.Close();
        tcpClient = null;
        tcpListener?.Stop();
        tcpListener = null;

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(100);
        }
    }

    private void ListenLoop()
    {
        while (isRunning)
        {
            try
            {
                tcpClient = tcpListener.AcceptTcpClient();
                Debug.Log("Sensor sender connected.", this);
                ReceiveClient(tcpClient);
            }
            catch (SocketException)
            {
                if (isRunning)
                {
                    Debug.LogWarning("TCP socket error while receiving sensor data.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Invalid sensor frame: " + ex.Message);
            }
            finally
            {
                tcpClient?.Close();
                tcpClient = null;
            }
        }
    }

    private void ReceiveClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[4096];
        StringBuilder pending = new StringBuilder();

        while (isRunning)
        {
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0) break;

            pending.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
            DispatchCompleteFrames(pending);
        }
    }

    private void DispatchCompleteFrames(StringBuilder pending)
    {
        while (true)
        {
            string bufferedText = pending.ToString();
            int newlineIndex = bufferedText.IndexOf('\n');
            if (newlineIndex < 0) return;

            string message = bufferedText.Substring(0, newlineIndex).Trim();
            pending.Remove(0, newlineIndex + 1);
            if (message.Length == 0) continue;

            SensorFrame frame = ParseSensorFrame(message);
            lock (dataLock)
            {
                hrvValue = Clamp01(frame.hrv);
                hasReceivedData = true;
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
    }
}
