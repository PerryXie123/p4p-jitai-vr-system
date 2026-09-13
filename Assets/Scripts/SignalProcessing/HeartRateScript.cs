using TMPro;
using UnityEngine;

public class HeartRateScript : MonoBehaviour
{
    private DataReceiverScript dataReceiver;
    public TextMeshProUGUI heartRateText;
    public TextMeshProUGUI statusText;

    private void Start()
    {
        dataReceiver = DataReceiverScript.Instance ?? FindFirstObjectByType<DataReceiverScript>();
    }

    private void Update()
    {
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        if (dataReceiver == null)
        {
            dataReceiver = DataReceiverScript.Instance ?? FindFirstObjectByType<DataReceiverScript>();
            return;
        }

        if (dataReceiver.HasReceivedData && dataReceiver.CurrentVitals != null)
        {
            heartRateText.text = dataReceiver.CurrentVitals.PrintVitals();
        }

        statusText.text = dataReceiver.IsSignalProcessingConnected
            ? "Status: Client connected"
            : "Status: Listening...";
    }
}
