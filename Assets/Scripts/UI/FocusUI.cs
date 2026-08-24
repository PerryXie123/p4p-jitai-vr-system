using TMPro; // use UnityEngine.UI if you're using normal Text
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class FocusUI : MonoBehaviour
{
    [SerializeField] private bool showFocusUI = true;
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

        SetStatusTextVisible(showFocusUI);
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
        if (!showFocusUI)
        {
            SetStatusTextVisible(false);
            return;
        }

        if (statusText == null || dataReceiver == null) return;

        SetStatusTextVisible(true);
        statusText.text = dataReceiver.GetStatusText();
        statusText.color = Color.white;
    }

    private void SetStatusTextVisible(bool isVisible)
    {
        if (statusText == null) return;

        GameObject statusTextObject = statusText.gameObject;
        if (statusTextObject.activeSelf != isVisible)
        {
            statusTextObject.SetActive(isVisible);
        }
    }
}
