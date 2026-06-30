using TMPro; // use UnityEngine.UI if you're using normal Text
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class FocusUI : MonoBehaviour
{
    public TextMeshProUGUI statusText;
    [SerializeField] private DataReceiverScript dataReceiver;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;

    void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();

        if (dataReceiver == null)
        {
            dataReceiver = FindFirstObjectByType<DataReceiverScript>();
        }
    }

    void OnEnable()
    {
        if (interactable == null) return;

        interactable.hoverEntered.AddListener(OnHoverEnter);
        interactable.hoverExited.AddListener(OnHoverExit);
    }

    void OnDisable()
    {
        if (interactable == null) return;

        interactable.hoverEntered.RemoveListener(OnHoverEnter);
        interactable.hoverExited.RemoveListener(OnHoverExit);
    }

    void Update()
    {
        RefreshStatus();
    }

    void OnHoverEnter(HoverEnterEventArgs args)
    {
        RefreshStatus();
    }

    void OnHoverExit(HoverExitEventArgs args)
    {
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        if (statusText == null || dataReceiver == null) return;

        statusText.text = dataReceiver.GetStatusText();
        statusText.color = dataReceiver.IsFocused ? Color.green : Color.red;
    }
}
