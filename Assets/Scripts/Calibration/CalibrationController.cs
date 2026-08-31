using Assets.Scripts.SignalProcessing;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum CalibrationFlowState
{
    Idle,
    AwaitingStartAcknowledgement,
    Collecting,
    AwaitingResult,
    Complete,
    Failed
}

public class CalibrationController : MonoBehaviour
{
    private const string MenuSceneName = "Menu";
    private const float StartAcknowledgementTimeoutSeconds = 5f;
    private const float ResultTimeoutSeconds = 30f;

    private static CalibrationController activeInstance;

    [Header("Panels")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject finishPanel;

    [Header("Controls")]
    [SerializeField] private Button beginButton;

    [Header("Timing")]
    [SerializeField, Range(60, 300)] private int calibrationDurationSeconds = 60;

    [Header("Connection")]
    [SerializeField] private DataReceiverScript dataReceiver;

    private Coroutine calibrationRoutine;
    private DataReceiverScript subscribedReceiver;
    private TMP_Text beginButtonLabel;
    private string originalBeginButtonText = "Begin";
    private string activeRequestId;
    private float elapsedCalibrationSeconds;
    private bool isPrimaryInstance;

    public CalibrationFlowState State { get; private set; } = CalibrationFlowState.Idle;

    private float DurationSeconds => Mathf.Max(1, calibrationDurationSeconds);

    private void OnValidate()
    {
        calibrationDurationSeconds = Mathf.Clamp(calibrationDurationSeconds, 60, 300);
    }

    private void Awake()
    {
        if (activeInstance != null && activeInstance != this)
        {
            Debug.LogWarning(
                "Duplicate CalibrationController detected. This instance will forward "
                + "serialized UI calls to the active controller.",
                this);
            return;
        }

        activeInstance = this;
        isPrimaryInstance = true;

        EnsureReferences();
        SubscribeToReceiver();

        if (beginButton != null)
        {
            beginButtonLabel = beginButton.GetComponentInChildren<TMP_Text>(true);
            if (beginButtonLabel != null && !string.IsNullOrWhiteSpace(beginButtonLabel.text))
            {
                originalBeginButtonText = beginButtonLabel.text.Trim();
            }

            beginButton.onClick.RemoveListener(StartCalibration);
            if (beginButton.onClick.GetPersistentEventCount() == 0)
            {
                beginButton.onClick.AddListener(StartCalibration);
            }
        }
    }

    private void Start()
    {
        if (!isPrimaryInstance) return;
        ResetCalibrationUI();
    }

    private void OnDestroy()
    {
        if (!isPrimaryInstance) return;

        CancelActiveCalibration("Calibration scene closed.");
        UnsubscribeFromReceiver();

        if (activeInstance == this)
        {
            activeInstance = null;
        }
    }

    public void StartCalibration()
    {
        if (!isPrimaryInstance)
        {
            if (activeInstance != null && activeInstance != this)
            {
                activeInstance.StartCalibration();
            }

            return;
        }

        EnsureReferences();
        SubscribeToReceiver();

        if (State == CalibrationFlowState.AwaitingStartAcknowledgement
            || State == CalibrationFlowState.Collecting
            || State == CalibrationFlowState.AwaitingResult)
        {
            return;
        }

        if (dataReceiver == null || !dataReceiver.IsSignalProcessingConnected)
        {
            FailCalibration("Signal processing is not connected.");
            return;
        }

        StopCalibrationRoutine();
        activeRequestId = Guid.NewGuid().ToString();
        elapsedCalibrationSeconds = 0f;
        State = CalibrationFlowState.AwaitingStartAcknowledgement;

        if (startPanel != null) startPanel.SetActive(true);
        if (finishPanel != null) finishPanel.SetActive(false);
        SetBeginButtonState(false, "Starting...");

        Debug.Log(
            $"Requesting a {calibrationDurationSeconds}-second calibration "
            + $"({activeRequestId}).",
            this);

        if (!OnCalibrationStarted())
        {
            FailCalibration("Calibration start could not be sent.");
            return;
        }

        calibrationRoutine = StartCoroutine(WaitForStartAcknowledgement());
    }

    public void LoadMenu()
    {
        if (!isPrimaryInstance)
        {
            if (activeInstance != null && activeInstance != this)
            {
                activeInstance.LoadMenu();
            }

            return;
        }

        CancelActiveCalibration("Calibration cancelled because the menu was opened.");
        SceneManager.LoadScene(MenuSceneName);
    }

    private IEnumerator WaitForStartAcknowledgement()
    {
        float deadline = Time.realtimeSinceStartup + StartAcknowledgementTimeoutSeconds;

        while (State == CalibrationFlowState.AwaitingStartAcknowledgement)
        {
            if (dataReceiver == null || !dataReceiver.IsSignalProcessingConnected)
            {
                calibrationRoutine = null;
                FailCalibration("Signal processing disconnected before calibration started.", false);
                yield break;
            }

            if (Time.realtimeSinceStartup >= deadline)
            {
                calibrationRoutine = null;
                FailCalibration("Signal processing did not acknowledge calibration in time.", false);
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator CalibrationRoutine()
    {
        while (elapsedCalibrationSeconds < DurationSeconds)
        {
            if (dataReceiver == null || !dataReceiver.IsSignalProcessingConnected)
            {
                calibrationRoutine = null;
                FailCalibration("Signal processing disconnected during calibration.", false);
                yield break;
            }

            elapsedCalibrationSeconds += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsedCalibrationSeconds = DurationSeconds;
        calibrationRoutine = null;
        OnCalibrationFinished();
    }

    private IEnumerator WaitForCalibrationResult()
    {
        float deadline = Time.realtimeSinceStartup + ResultTimeoutSeconds;

        while (State == CalibrationFlowState.AwaitingResult)
        {
            if (dataReceiver == null || !dataReceiver.IsSignalProcessingConnected)
            {
                calibrationRoutine = null;
                FailCalibration("Signal processing disconnected while calculating the baseline.", false);
                yield break;
            }

            if (Time.realtimeSinceStartup >= deadline)
            {
                calibrationRoutine = null;
                FailCalibration("Signal processing did not return a calibration result in time.", false);
                yield break;
            }

            yield return null;
        }
    }

    private bool OnCalibrationStarted()
    {
        return dataReceiver != null
            && dataReceiver.TryStartCalibration(activeRequestId, DurationSeconds);
    }

    private void OnCalibrationFinished()
    {
        State = CalibrationFlowState.AwaitingResult;

        if (!dataReceiver.TryFinishCalibration(activeRequestId, elapsedCalibrationSeconds))
        {
            FailCalibration("Calibration finish could not be sent.");
            return;
        }

        Debug.Log($"Calibration samples captured for request {activeRequestId}.", this);
        calibrationRoutine = StartCoroutine(WaitForCalibrationResult());
    }

    private void HandleCalibrationStarted(
        string requestId,
        CalibrationStartedPayload payload)
    {
        if (!isPrimaryInstance
            || State != CalibrationFlowState.AwaitingStartAcknowledgement
            || !IsActiveRequest(requestId))
        {
            return;
        }

        StopCalibrationRoutine();

        if (!payload.Accepted)
        {
            FailCalibration(FormatProtocolError(
                payload.ErrorCode,
                payload.Error,
                "Signal processing rejected calibration."));
            return;
        }

        State = CalibrationFlowState.Collecting;
        elapsedCalibrationSeconds = 0f;
        if (startPanel != null) startPanel.SetActive(false);
        if (finishPanel != null) finishPanel.SetActive(false);

        Debug.Log($"Calibration {activeRequestId} accepted; collection started.", this);
        calibrationRoutine = StartCoroutine(CalibrationRoutine());
    }

    private void HandleCalibrationResult(
        string requestId,
        CalibrationResultPayload payload)
    {
        if (!isPrimaryInstance
            || State != CalibrationFlowState.AwaitingResult
            || !IsActiveRequest(requestId))
        {
            return;
        }

        StopCalibrationRoutine();

        if (!payload.Success)
        {
            FailCalibration(FormatProtocolError(
                payload.ErrorCode,
                payload.Error,
                "Signal processing could not calculate a valid baseline."));
            return;
        }

        if (dataReceiver == null || !dataReceiver.TryApplyCalibrationResult(payload))
        {
            FailCalibration("Signal processing returned invalid baseline values.");
            return;
        }

        State = CalibrationFlowState.Complete;
        activeRequestId = null;
        SetBeginButtonState(false, originalBeginButtonText);
        if (startPanel != null) startPanel.SetActive(false);
        if (finishPanel != null) finishPanel.SetActive(true);

        Debug.Log(
            $"Calibration complete. Baseline HR: {payload.BaselineHeartRate:0.0} bpm; "
            + $"RMSSD: {payload.BaselineRmssd:0.0} ms; "
            + $"valid windows: {payload.ValidWindowCount}.",
            this);
    }

    private void FailCalibration(string reason, bool stopRoutine = true)
    {
        if (stopRoutine)
        {
            StopCalibrationRoutine();
        }

        bool wasActive = State == CalibrationFlowState.AwaitingStartAcknowledgement
            || State == CalibrationFlowState.Collecting
            || State == CalibrationFlowState.AwaitingResult;

        if (wasActive
            && !string.IsNullOrWhiteSpace(activeRequestId)
            && dataReceiver != null
            && dataReceiver.IsSignalProcessingConnected)
        {
            dataReceiver.TryCancelCalibration(activeRequestId, reason);
        }

        State = CalibrationFlowState.Failed;
        activeRequestId = null;
        elapsedCalibrationSeconds = 0f;

        if (startPanel != null) startPanel.SetActive(true);
        if (finishPanel != null) finishPanel.SetActive(false);
        SetBeginButtonState(true, "Retry");
        Debug.LogError(reason, this);
    }

    private void CancelActiveCalibration(string reason)
    {
        bool isActive = State == CalibrationFlowState.AwaitingStartAcknowledgement
            || State == CalibrationFlowState.Collecting
            || State == CalibrationFlowState.AwaitingResult;

        if (isActive
            && !string.IsNullOrWhiteSpace(activeRequestId)
            && dataReceiver != null
            && dataReceiver.IsSignalProcessingConnected)
        {
            dataReceiver.TryCancelCalibration(activeRequestId, reason);
        }

        StopCalibrationRoutine();
        activeRequestId = null;
        elapsedCalibrationSeconds = 0f;

        if (isActive)
        {
            State = CalibrationFlowState.Idle;
        }
    }

    private void ResetCalibrationUI()
    {
        CancelActiveCalibration("Calibration UI reset.");
        State = CalibrationFlowState.Idle;

        if (startPanel != null) startPanel.SetActive(true);
        if (finishPanel != null) finishPanel.SetActive(false);
        SetBeginButtonState(true, originalBeginButtonText);
    }

    private void StopCalibrationRoutine()
    {
        if (calibrationRoutine == null) return;

        StopCoroutine(calibrationRoutine);
        calibrationRoutine = null;
    }

    private void EnsureReferences()
    {
        if (beginButton == null)
        {
            GameObject beginObject = GameObject.Find("Begin");
            if (beginObject != null) beginButton = beginObject.GetComponent<Button>();
        }

        if (dataReceiver == null)
        {
            dataReceiver = FindFirstObjectByType<DataReceiverScript>();
        }
    }

    private void SubscribeToReceiver()
    {
        if (dataReceiver == null || subscribedReceiver == dataReceiver) return;

        UnsubscribeFromReceiver();
        dataReceiver.CalibrationStartedReceived += HandleCalibrationStarted;
        dataReceiver.CalibrationResultReceived += HandleCalibrationResult;
        subscribedReceiver = dataReceiver;
    }

    private void UnsubscribeFromReceiver()
    {
        if (subscribedReceiver == null) return;

        subscribedReceiver.CalibrationStartedReceived -= HandleCalibrationStarted;
        subscribedReceiver.CalibrationResultReceived -= HandleCalibrationResult;
        subscribedReceiver = null;
    }

    private void SetBeginButtonState(bool interactable, string label)
    {
        if (beginButton != null)
        {
            beginButton.interactable = interactable;
        }

        if (beginButtonLabel != null)
        {
            beginButtonLabel.text = label;
        }
    }

    private bool IsActiveRequest(string requestId)
    {
        return !string.IsNullOrWhiteSpace(activeRequestId)
            && string.Equals(activeRequestId, requestId, StringComparison.Ordinal);
    }

    private static string FormatProtocolError(
        string errorCode,
        string error,
        string fallback)
    {
        string message = string.IsNullOrWhiteSpace(error) ? fallback : error;
        return string.IsNullOrWhiteSpace(errorCode)
            ? message
            : $"{errorCode}: {message}";
    }
}
