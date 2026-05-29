using UnityEngine;
using System.Collections;

public class AuditoryTraining : TrainingMode
{
    [Header("Main Track")]
    [SerializeField] private AudioSource mainAudio;
    [SerializeField, Range(0f, 1f)] private float mainVolume = 1f;

    [Header("One-Shot Trigger")]
    [SerializeField] private AudioSource oneShotAudio;
    [SerializeField] private AudioClip triggerClip;
    [SerializeField, Range(0f, 1f)] private float triggerVolume = 1f;
    [Range(1f, 30f)]
    [SerializeField] private float focusTriggerTime = 5f;

    [Header("Diagnostics")]
    [SerializeField] private bool playTestToneOnStart = true;
    [SerializeField, Range(100f, 2000f)] private float testToneFrequency = 880f;
    [SerializeField, Range(0.1f, 3f)] private float testToneDuration = 0.5f;

    private bool isFocused = false;
    private float focusTimer = 0f;
    private bool hasPlayed = false;
    private AudioSource fallbackAudioSource;

    public AudioSource MainAudioSource => mainAudio;
    public float MainVolume => mainVolume;

    public override void StartTraining()
    {
        Debug.Log("Starting auditory training");

        EnsureAudioListener();
        AudioListener.pause = false;
        AudioListener.volume = 1f;

        ConfigureAudioSource(mainAudio, mainVolume);
        ConfigureAudioSource(oneShotAudio, triggerVolume);

        if (playTestToneOnStart)
        {
            PlayTestTone();
        }

        if (mainAudio != null)
        {
            mainAudio.loop = true;
            mainAudio.volume = mainVolume;
            mainAudio.Play();
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

        oneShotAudio.PlayOneShot(triggerClip, triggerVolume);
    }

    private void StopAudio()
    {
        if (mainAudio != null) mainAudio.Stop();
        if (oneShotAudio != null) oneShotAudio.Stop();
        if (fallbackAudioSource != null) fallbackAudioSource.Stop();
    }

    public override void ResetTrainingUI()
    {
        base.ResetTrainingUI();
        StopAudio();
    }

    private void ConfigureAudioSource(AudioSource audioSource, float volume)
    {
        if (audioSource == null) return;

        audioSource.playOnAwake = false;
        audioSource.mute = false;
        audioSource.volume = volume;
        audioSource.spatialBlend = 0f;
        audioSource.bypassEffects = false;
        audioSource.bypassListenerEffects = false;
        audioSource.bypassReverbZones = false;
    }

    private void EnsureAudioListener()
    {
        if (FindFirstObjectByType<AudioListener>() != null) return;

        Camera audioCamera = Camera.main;
        if (audioCamera == null)
        {
            audioCamera = FindFirstObjectByType<Camera>();
        }

        if (audioCamera == null)
        {
            Debug.LogWarning("No camera found for AudioListener. Quest audio may be silent.", this);
            return;
        }

        audioCamera.gameObject.AddComponent<AudioListener>();
        Debug.Log("Added missing AudioListener to " + audioCamera.name, audioCamera);
    }

    [ContextMenu("Play Test Tone")]
    private void PlayTestTone()
    {
        AudioSource audioSource = oneShotAudio != null ? oneShotAudio : GetFallbackAudioSource();
        if (audioSource == null)
        {
            Debug.LogError("No AudioSource available for test tone.", this);
            return;
        }

        ConfigureAudioSource(audioSource, triggerVolume);
        AudioClip testTone = CreateTestToneClip(testToneFrequency, testToneDuration);
        audioSource.PlayOneShot(testTone, triggerVolume);
        Debug.Log("Playing generated audio test tone.", this);
    }

    private AudioSource GetFallbackAudioSource()
    {
        if (fallbackAudioSource != null) return fallbackAudioSource;

        AudioListener listener = FindFirstObjectByType<AudioListener>();
        GameObject audioHost = listener != null ? listener.gameObject : gameObject;
        fallbackAudioSource = audioHost.AddComponent<AudioSource>();
        fallbackAudioSource.playOnAwake = false;
        fallbackAudioSource.spatialBlend = 0f;
        fallbackAudioSource.volume = triggerVolume;
        return fallbackAudioSource;
    }

    private AudioClip CreateTestToneClip(float frequency, float duration)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float time = (float)i / sampleRate;
            float fadeIn = Mathf.Clamp01(time / 0.02f);
            float fadeOut = Mathf.Clamp01((duration - time) / 0.05f);
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * fadeIn * fadeOut * 0.25f;
        }

        AudioClip clip = AudioClip.Create("Generated Test Tone", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
