using UnityEngine;
using System;
using System.Collections;

public abstract class TrainingMode : MonoBehaviour
{
    [SerializeField] protected GameObject orb;
    [SerializeField] protected GameObject startPanel;
    [SerializeField] protected GameObject finishPanel;
    [SerializeField] protected GameObject focusText;
    [SerializeField] protected DataReceiverScript focusDataReceiver;
    [SerializeField, Range(1f, 3600f)] protected float trainingDurationSeconds = 30f;

    public bool IsTrainingActive { get; private set; }

    private float focusedSeconds;
    private float elapsedTrainingSeconds;
    private DateTime sessionStartedAt;
    private Coroutine trainingCoroutine;
    private bool hasRecordedTrainingRun;

    public virtual void StartTraining()
    {
        if (trainingCoroutine != null)
        {
            StopCoroutine(trainingCoroutine);
        }

        trainingCoroutine = StartCoroutine(TrainingRoutine());
    }

    protected virtual IEnumerator TrainingRoutine()
    {
        if (orb == null || startPanel == null || finishPanel == null || focusText == null)
        {
            Debug.LogError("Missing references.", this);
            yield break;
        }

        if (focusDataReceiver == null)
        {
            focusDataReceiver = FindFirstObjectByType<DataReceiverScript>();
        }

        focusedSeconds = 0f;
        elapsedTrainingSeconds = 0f;
        sessionStartedAt = DateTime.Now;
        hasRecordedTrainingRun = false;
        IsTrainingActive = true;

        startPanel.SetActive(false);
        finishPanel.SetActive(false);

        orb.SetActive(true);
        focusText.SetActive(true);

        while (elapsedTrainingSeconds < trainingDurationSeconds)
        {
            float frameSeconds = Mathf.Min(Time.deltaTime, trainingDurationSeconds - elapsedTrainingSeconds);
            elapsedTrainingSeconds += frameSeconds;

            if (focusDataReceiver != null && focusDataReceiver.IsFocused)
            {
                focusedSeconds += frameSeconds;
            }

            yield return null;
        }

        orb.SetActive(false);
        focusText.SetActive(false);

        DateTime sessionEndedAt = DateTime.Now;
        finishPanel.SetActive(true);
        IsTrainingActive = false;
        RecordTrainingRunIfNeeded(sessionEndedAt);
        trainingCoroutine = null;
    }

    public virtual void ResetTrainingUI()
    {
        if (trainingCoroutine != null)
        {
            StopCoroutine(trainingCoroutine);
            trainingCoroutine = null;
        }

        if (IsTrainingActive)
        {
            RecordTrainingRunIfNeeded(DateTime.Now);
        }

        IsTrainingActive = false;

        if (orb != null) orb.SetActive(false);
        if (startPanel != null) startPanel.SetActive(true);
        if (finishPanel != null) finishPanel.SetActive(false);
        if (focusText != null) focusText.SetActive(false);
    }

    protected virtual void OnDestroy()
    {
        if (IsTrainingActive)
        {
            RecordTrainingRunIfNeeded(DateTime.Now);
        }
    }

    private void RecordTrainingRunIfNeeded(DateTime sessionEndedAt)
    {
        if (hasRecordedTrainingRun)
        {
            return;
        }

        float focusPercentage = elapsedTrainingSeconds <= 0f
            ? 0f
            : focusedSeconds / elapsedTrainingSeconds * 100f;

        TrainingSessionLogger.RecordTrainingRun(new TrainingSessionLogger.TrainingRunRecord
        {
            TrainingType = GetType().Name,
            StartedAt = sessionStartedAt,
            EndedAt = sessionEndedAt,
            DurationSeconds = elapsedTrainingSeconds,
            FocusedSeconds = focusedSeconds,
            FocusedPercentage = focusPercentage,
            LoadZThreshold = focusDataReceiver != null ? focusDataReceiver.LoadZThreshold : 0f
        });

        hasRecordedTrainingRun = true;
    }
}
