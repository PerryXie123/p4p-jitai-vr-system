using TMPro; // use UnityEngine.UI if you're using normal Text
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class FocusUI : MonoBehaviour
{
    public TextMeshProUGUI statusText;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;

    void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
    }

    void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEnter);
        interactable.hoverExited.AddListener(OnHoverExit);
    }

    void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEnter);
        interactable.hoverExited.RemoveListener(OnHoverExit);
    }

    void OnHoverEnter(HoverEnterEventArgs args)
    {
        statusText.text = "Focused";
        statusText.color = Color.green;
    }

    void OnHoverExit(HoverExitEventArgs args)
    {
        statusText.text = "Not Focused";
        statusText.color = Color.red;
    }
}