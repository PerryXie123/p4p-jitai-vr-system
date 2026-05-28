using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class OrbFocus : MonoBehaviour
{
    [SerializeField] private AuditoryTraining auditoryTraining;
    [SerializeField] private DataReceiverScript dataReceiver;

    private XRBaseInteractable interactable;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        
        if (dataReceiver == null)
        {
            dataReceiver = FindFirstObjectByType<DataReceiverScript>();
        }

        if (interactable == null)
        {
            Debug.LogError("OrbFocus requires an XR Simple Interactable or XRBaseInteractable on the same GameObject.", this);
        }
    }

    private void OnEnable()
    {
        if (interactable == null) return;

        interactable.hoverEntered.AddListener(OnHoverEnter);
        interactable.hoverExited.AddListener(OnHoverExit);
    }

    private void OnDisable()
    {
        if (interactable == null) return;

        interactable.hoverEntered.RemoveListener(OnHoverEnter);
        interactable.hoverExited.RemoveListener(OnHoverExit);
    }

    private void OnHoverEnter(HoverEnterEventArgs args)
    {
        Debug.Log("Focused on orb");

        if (dataReceiver != null)
        {
            dataReceiver.SetOrbFocus(true);
        }

        if (dataReceiver == null && auditoryTraining != null)
        {
            auditoryTraining.SetFocus(true);
        }
    }

    private void OnHoverExit(HoverExitEventArgs args)
    {
        Debug.Log("Stopped focusing on orb");

        if (dataReceiver != null)
        {
            dataReceiver.SetOrbFocus(false);
        }

        if (dataReceiver == null && auditoryTraining != null)
        {
            auditoryTraining.SetFocus(false);
        }
    }
}
