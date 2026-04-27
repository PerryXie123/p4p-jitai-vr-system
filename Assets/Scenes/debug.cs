using UnityEngine;


public class debug : MonoBehaviour
{
    void Start()
    {
        var interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        interactable.hoverEntered.AddListener((args) =>
        {
            Debug.Log("Gaze detected on orb");
        });
    }
}