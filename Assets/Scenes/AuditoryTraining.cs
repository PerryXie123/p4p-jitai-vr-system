using UnityEngine;
using System.Collections;

public class AuditoryTraining : TrainingMode
{
    [Header("Main Track")]
    [SerializeField] private AudioSource mainAudio;

    [Header("Distraction Sounds")]
    [SerializeField] private AudioSource oneShotAudio;
    [SerializeField] private AudioClip[] distractionClips;

    [SerializeField] private float minInterval = 2f;
    [SerializeField] private float maxInterval = 6f;

    [SerializeField] private float unfocusedMultiplier = 2.5f;

    private bool isFocused = false;

    public override void StartTraining()
    {
        Debug.Log("Starting auditory training");

        if (mainAudio != null)
        {
            mainAudio.loop = true;
            mainAudio.Play(); // always plays, fixed volume
        }

        StartCoroutine(DistractionRoutine());

        base.StartTraining();
    }

    protected override IEnumerator TrainingRoutine()
    {
        yield return base.TrainingRoutine();

        // Stop everything when training ends
        if (mainAudio != null) mainAudio.Stop();
        StopAllCoroutines();
    }

    // Called by gaze/orb system
    public void SetFocus(bool focused)
    {
        isFocused = focused;
    }

    private IEnumerator DistractionRoutine()
    {
        while (true)
        {
            float interval = Random.Range(minInterval, maxInterval);

            // If NOT focused → MORE distractions
            if (!isFocused)
            {
                interval /= unfocusedMultiplier;
            }

            yield return new WaitForSeconds(interval);

            if (distractionClips.Length > 0 && oneShotAudio != null)
            {
                AudioClip clip = distractionClips[Random.Range(0, distractionClips.Length)];
                oneShotAudio.PlayOneShot(clip);
            }
        }
    }
}