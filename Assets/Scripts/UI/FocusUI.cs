using TMPro; // use UnityEngine.UI if you're using normal Text
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

public class FocusUI : MonoBehaviour
{
    [SerializeField] private bool showFocusUI = false;
    [SerializeField] private KeyCode toggleKey = KeyCode.T;
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text toggleButtonLabel;
    [SerializeField] private bool createToggleButtonIfMissing = true;
    [SerializeField] private Vector2 toggleButtonOffset = new Vector2(0f, -48f);
    [SerializeField] private Vector2 toggleButtonSize = new Vector2(180f, 42f);
    [SerializeField] private string showLabel = "Show Vitals";
    [SerializeField] private string hideLabel = "Hide Vitals";

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

        CreateToggleButtonIfNeeded();
        SetStatusTextVisible(showFocusUI);
        UpdateToggleLabel();
    }

    void OnEnable()
    {
        if (interactable != null)
        {
            interactable.hoverEntered.AddListener(OnHoverEnter);
            interactable.hoverExited.AddListener(OnHoverExit);
        }

        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(ToggleFocusUI);
        }
    }

    void OnDisable()
    {
        if (interactable != null)
        {
            interactable.hoverEntered.RemoveListener(OnHoverEnter);
            interactable.hoverExited.RemoveListener(OnHoverExit);
        }

        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(ToggleFocusUI);
        }
    }

    void Update()
    {
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
        {
            ToggleFocusUI();
        }

        RefreshStatus();
    }

    public void ToggleFocusUI()
    {
        SetFocusUIVisible(!showFocusUI);
    }

    public void SetFocusUIVisible(bool isVisible)
    {
        showFocusUI = isVisible;
        SetStatusTextVisible(showFocusUI);
        UpdateToggleLabel();
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

    private void UpdateToggleLabel()
    {
        if (toggleButtonLabel == null) return;

        toggleButtonLabel.text = showFocusUI ? hideLabel : showLabel;
    }

    private void CreateToggleButtonIfNeeded()
    {
        if (!createToggleButtonIfMissing || toggleButton != null || statusText == null)
        {
            return;
        }

        RectTransform statusRect = statusText.rectTransform;
        Transform parent = statusRect.parent;
        if (parent == null)
        {
            return;
        }

        GameObject buttonObject = new GameObject("Vitals Toggle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = statusRect.anchorMin;
        buttonRect.anchorMax = statusRect.anchorMax;
        buttonRect.pivot = statusRect.pivot;
        buttonRect.anchoredPosition = statusRect.anchoredPosition + toggleButtonOffset;
        buttonRect.sizeDelta = toggleButtonSize;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0f, 0f, 0f, 0.65f);

        toggleButton = buttonObject.GetComponent<Button>();

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        toggleButtonLabel = labelObject.GetComponent<TextMeshProUGUI>();
        toggleButtonLabel.alignment = TextAlignmentOptions.Center;
        toggleButtonLabel.color = Color.white;
        toggleButtonLabel.fontSize = 18f;
        toggleButtonLabel.raycastTarget = false;
    }
}
