using Assets.Scripts.SignalProcessing;
using TMPro;
using UnityEngine;

public class HeartRateScript : MonoBehaviour
{
    private TcpGameServer<SignalProcessingMessage> sensorClient;
    public TextMeshProUGUI heartRateText;
    public TextMeshProUGUI statusText;

    private void Start()
    {
        InitSensorSocket();
    }

    private void Update()
    {
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        while (sensorClient.TryGetMessage(out SignalProcessingMessage message))
        {
            if (message != null
                && message.Type == MessageTypes.VitalsSnapshot
                && message.Vitals != null)
            {
                heartRateText.text = message.Vitals.PrintVitals();
            }
        }

        if (sensorClient.TryGetError(out string error))
        {
            statusText.text = $"Status: {error}";
        }
    }

    private void OnDestroy()
    {
        sensorClient?.CloseSocket();
    }

    private void InitSensorSocket()
    {
        sensorClient = new TcpGameServer<SignalProcessingMessage>();
        sensorClient.InitConnection();
    }
}
