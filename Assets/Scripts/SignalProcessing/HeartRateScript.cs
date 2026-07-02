using Assets.Scripts.SignalProcessing;
using TMPro;
using UnityEngine;

public class HeartRateScript : MonoBehaviour
{
    private TcpGameServer<VitalSnapshot> sensorClient;
    public TextMeshProUGUI heartRateText;
    public TextMeshProUGUI statusText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        InitSensorSocket();
    }

    // Update is called once per frame
    void Update()
    {
        UpdateLabels();
    }

    void UpdateLabels()
    {
        if (sensorClient.TryGetMessage(out VitalSnapshot stats))
        {
            heartRateText.text = $"{stats.PrintVitals()}";
        }
        if (sensorClient.TryGetError(out string error))
        {
            statusText.text = $"Status: {error}";
        }
    }

    void OnDestroy()
    {
        sensorClient?.CloseSocket();
    }

    private void InitSensorSocket()
    {
        sensorClient = new TcpGameServer<VitalSnapshot>();
        sensorClient.InitConnection();
    }
}
