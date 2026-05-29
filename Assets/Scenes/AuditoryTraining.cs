using UnityEngine;
using System.Collections;

public class AuditoryTraining : TrainingMode
{
    [Header("Main Track")]
    [SerializeField] private AudioSource mainAudio;

    [Header("One-Shot Trigger")]
    [SerializeField] private AudioSource oneShotAudio;
    [SerializeField] private AudioClip triggerClip;
    [Range(1f, 30f)]
    [SerializeField] private float focusTriggerTime = 5f;

    private bool isFocused = false;
    private float focusTimer = 0f;
    private bool hasPlayed = false;

    public override void StartTraining()
    {
        Debug.Log("Starting auditory training");

        if (mainAudio != null)
        {
            mainAudio.loop = true;
            mainAudio.volume = 0.2f;
            mainAudio.Play(); // always playing, fixed volume
        }

        base.StartTraining();
    }

    protected override IEnumerator TrainingRoutine()
    {
        yield return base.TrainingRoutine();

        StopAudio();
    }

    private void Update()
    {
        if (isFocused)
        {
            focusTimer += Time.deltaTime;

            // Debug to verify timer
            // Debug.Log("Focus time: " + focusTimer);

            if (focusTimer >= focusTriggerTime && !hasPlayed)
            {
                PlayTrigger();
                hasPlayed = true;
            }
        }
        else
        {
            // reset when user looks away
            focusTimer = 0f;
            hasPlayed = false;
        }
    }

    public void SetFocus(bool focused)
    {
        isFocused = focused;
        Debug.Log("Focus: " + focused);
    }

    private void PlayTrigger()
    {
        if (oneShotAudio == null)
        {
            Debug.LogError("OneShot AudioSource missing", this);
            return;
        }

        if (triggerClip == null)
        {
            Debug.LogError("Trigger clip missing", this);
            return;
        }

        Debug.Log("PLAYING TRIGGER SOUND");

        oneShotAudio.PlayOneShot(triggerClip);
    }

    private void StopAudio()
    {
        if (mainAudio != null) mainAudio.Stop();
        if (oneShotAudio != null) oneShotAudio.Stop();
    }

    public override void ResetTrainingUI()
    {
        base.ResetTrainingUI();
        StopAudio();
    }
}
