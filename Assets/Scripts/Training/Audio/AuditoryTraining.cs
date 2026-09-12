using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    [Header("Ambient Distraction")]
    [SerializeField] private AudioClip ambientClip;
    [SerializeField, Range(0f, 1f)] private float ambientVolume = 0.65f;
    [SerializeField, Range(0.5f, 30f)] private float ambientUnfocusedDelay = 5f;
    [SerializeField, Range(0f, 100f)] private float ambientFadeInPerSecond = 12f;
    [SerializeField, Range(0f, 100f)] private float ambientFadeOutPerSecond = 20f;

    [Header("Focused Distractions")]
    [SerializeField] private AudioClip[] distractionClips;
    [SerializeField, Range(1, 8)] private int soundsPerDistractionSet = 3;
    [SerializeField, Range(0f, 30f)] private float focusSecondsBeforeDistractionsAppear = 5f;
    [SerializeField, Range(0.1f, 30f)] private float additionalSetSpawnInterval = 5f;
    [SerializeField, Range(0f, 1f)] private float distractionVolume = 0.35f;
    [SerializeField, Range(0.1f, 5f)] private float distractionFadeInSeconds = 0.75f;
    [SerializeField, Range(0.1f, 5f)] private float distractionFadeOutSeconds = 2f;
    [SerializeField, Range(1f, 60f)] private float maxDistractionSetActiveSeconds = 15f;

    [Header("Diagnostics")]
    [SerializeField] private bool playTestToneOnStart = true;
    [SerializeField, Range(100f, 2000f)] private float testToneFrequency = 880f;
    [SerializeField, Range(0.1f, 3f)] private float testToneDuration = 0.5f;

    private bool isFocused = false;
    private float focusTimer = 0f;
    private float distractionFocusTimer = 0f;
    private float unfocusedTimer = 0f;
    private float currentAmbientAmount = 0f;
    private bool hasPlayed = false;
    private AudioSource fallbackAudioSource;
    private readonly List<DistractionSoundSet> distractionSoundSets = new();
    private bool distractionSetsHaveAppeared;

    public bool ManagesAmbientAudio => ambientClip != null;
    public AudioSource MainAudioSource => mainAudio;
    public float MainVolume => mainVolume;

    private sealed class DistractionSoundSet
    {
        public DistractionSoundSet(AudioSource[] sources)
        {
            Sources = sources;
        }

        public AudioSource[] Sources { get; }
        public float ActiveSeconds { get; set; }
        public bool HasExpired { get; set; }

        public void ResetLifetime()
        {
            ActiveSeconds = 0f;
            HasExpired = false;
        }
    }

    public override void StartTraining()
    {
        Debug.Log("Starting auditory training");

        EnsureAudioListener();
        AudioListener.pause = false;
        AudioListener.volume = 1f;

        ConfigureAudioSource(mainAudio, mainVolume);
        ConfigureAudioSource(oneShotAudio, triggerVolume);
        ConfigureAmbientAudio();
        StopDistractionSoundSetsImmediately();

        if (playTestToneOnStart)
        {
            PlayTestTone();
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
        UpdateAmbientAudio();
        UpdateFocusedDistractions();

        if (!IsTrainingActive)
        {
            focusTimer = 0f;
            hasPlayed = false;
            return;
        }

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
        unfocusedTimer = 0f;
        currentAmbientAmount = 0f;
        StopDistractionSoundSetsImmediately();
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

    private void ConfigureAmbientAudio()
    {
        if (mainAudio == null || ambientClip == null) return;

        ConfigureAudioSource(mainAudio, 0f);
        mainAudio.clip = ambientClip;
        mainAudio.loop = true;
        mainAudio.volume = 0f;
        unfocusedTimer = 0f;
        currentAmbientAmount = 0f;
        mainAudio.Play();
    }

    private void UpdateAmbientAudio()
    {
        if (mainAudio == null || ambientClip == null) return;

        if (IsTrainingActive && !mainAudio.isPlaying)
        {
            mainAudio.Play();
        }

        if (!IsTrainingActive || isFocused)
        {
            unfocusedTimer = 0f;
            currentAmbientAmount = Mathf.MoveTowards(
                currentAmbientAmount,
                0f,
                ambientFadeOutPerSecond * Time.deltaTime);
        }
        else
        {
            unfocusedTimer += Time.deltaTime;

            if (unfocusedTimer >= ambientUnfocusedDelay)
            {
                currentAmbientAmount = Mathf.MoveTowards(
                    currentAmbientAmount,
                    100f,
                    ambientFadeInPerSecond * Time.deltaTime);
            }
        }

        mainAudio.volume = ambientVolume * currentAmbientAmount / 100f;

        if (!IsTrainingActive && mainAudio.volume <= 0.001f)
        {
            mainAudio.Stop();
        }
    }

    private void UpdateFocusedDistractions()
    {
        if (!IsTrainingActive)
        {
            FadeDistractionSoundSetsToward(false);
            return;
        }

        if (!isFocused)
        {
            distractionFocusTimer = 0f;
            FadeDistractionSoundSetsToward(false);
            return;
        }

        distractionFocusTimer += Time.deltaTime;
        float spawnInterval = distractionSetsHaveAppeared
            ? additionalSetSpawnInterval
            : Mathf.Max(0.01f, focusSecondsBeforeDistractionsAppear);

        if (distractionFocusTimer >= spawnInterval && HasDistractionClips())
        {
            distractionFocusTimer -= spawnInterval;
            distractionSetsHaveAppeared = true;
            SpawnDistractionSoundSet();
        }

        FadeDistractionSoundSetsToward(distractionSetsHaveAppeared);
    }

    private void SpawnDistractionSoundSet()
    {
        int sourceCount = Mathf.Max(1, soundsPerDistractionSet);
        AudioSource[] sources = new AudioSource[sourceCount];
        List<AudioClip> selectedClips = new();

        for (int i = 0; i < sourceCount; i++)
        {
            AudioSource audioSource = gameObject.AddComponent<AudioSource>();
            ConfigureDistractionAudioSource(audioSource);
            audioSource.clip = ChooseRandomDistractionClip(null, selectedClips);
            if (audioSource.clip != null)
            {
                selectedClips.Add(audioSource.clip);
            }

            audioSource.pitch = Random.Range(0.95f, 1.05f);
            sources[i] = audioSource;
        }

        distractionSoundSets.Add(new DistractionSoundSet(sources));
    }

    private void ConfigureDistractionAudioSource(AudioSource audioSource)
    {
        if (audioSource == null) return;

        ConfigureAudioSource(audioSource, 0f);
        audioSource.loop = false;
        audioSource.priority = 160;
    }

    private void FadeDistractionSoundSetsToward(bool shouldBeAudible)
    {
        bool allSoundsSilent = true;

        foreach (DistractionSoundSet soundSet in distractionSoundSets)
        {
            bool setShouldBeAudible = shouldBeAudible && !soundSet.HasExpired;

            if (setShouldBeAudible)
            {
                soundSet.ActiveSeconds += Time.deltaTime;
                if (soundSet.ActiveSeconds >= maxDistractionSetActiveSeconds)
                {
                    soundSet.HasExpired = true;
                    setShouldBeAudible = false;
                }
            }
            else if (!shouldBeAudible)
            {
                soundSet.ResetLifetime();
            }

            foreach (AudioSource audioSource in soundSet.Sources)
            {
                if (audioSource == null)
                {
                    continue;
                }

                if (setShouldBeAudible)
                {
                    EnsureDistractionAudioIsPlaying(audioSource);
                }

                float targetVolume = setShouldBeAudible ? distractionVolume : 0f;
                float fadeSeconds = targetVolume > audioSource.volume
                    ? distractionFadeInSeconds
                    : distractionFadeOutSeconds;
                float volumeRange = Mathf.Max(distractionVolume, 0.001f);
                float maxDelta = fadeSeconds > 0f ? volumeRange * Time.deltaTime / fadeSeconds : volumeRange;

                audioSource.volume = Mathf.MoveTowards(audioSource.volume, targetVolume, maxDelta);
                allSoundsSilent &= audioSource.volume <= 0.001f;

                if (!setShouldBeAudible && audioSource.volume <= 0.001f)
                {
                    audioSource.Stop();
                    audioSource.clip = null;
                }
            }
        }

        RemoveExpiredSilentDistractionSoundSets();

        if (!shouldBeAudible && allSoundsSilent && distractionSoundSets.Count > 0)
        {
            StopDistractionSoundSetsImmediately();
        }
    }

    private void EnsureDistractionAudioIsPlaying(AudioSource audioSource)
    {
        if (audioSource.isPlaying && audioSource.clip != null) return;

        AudioClip clip = ChooseRandomDistractionClip(audioSource.clip);
        if (clip == null) return;

        audioSource.clip = clip;
        audioSource.pitch = Random.Range(0.95f, 1.05f);
        audioSource.Play();
    }

    private bool HasDistractionClips()
    {
        if (distractionClips == null) return false;

        foreach (AudioClip clip in distractionClips)
        {
            if (clip != null) return true;
        }

        return false;
    }

    private AudioClip ChooseRandomDistractionClip(AudioClip previousClip)
    {
        return ChooseRandomDistractionClip(previousClip, null);
    }

    private AudioClip ChooseRandomDistractionClip(AudioClip previousClip, List<AudioClip> excludedClips)
    {
        if (distractionClips == null || distractionClips.Length == 0) return null;

        AudioClip fallbackClip = null;
        int availableClipCount = 0;
        int unexcludedClipCount = 0;

        foreach (AudioClip clip in distractionClips)
        {
            if (clip == null) continue;

            fallbackClip = clip;
            availableClipCount++;

            if (excludedClips == null || !excludedClips.Contains(clip))
            {
                unexcludedClipCount++;
            }
        }

        if (availableClipCount == 0) return null;
        if (availableClipCount == 1) return fallbackClip;

        for (int attempts = 0; attempts < 8; attempts++)
        {
            AudioClip candidate = distractionClips[Random.Range(0, distractionClips.Length)];
            bool isExcluded = excludedClips != null && excludedClips.Contains(candidate);
            if (candidate != null && candidate != previousClip && (!isExcluded || unexcludedClipCount == 0))
            {
                return candidate;
            }
        }

        if (unexcludedClipCount > 0)
        {
            foreach (AudioClip clip in distractionClips)
            {
                if (clip != null && (excludedClips == null || !excludedClips.Contains(clip)))
                {
                    return clip;
                }
            }
        }

        return fallbackClip;
    }

    private void RemoveExpiredSilentDistractionSoundSets()
    {
        for (int setIndex = distractionSoundSets.Count - 1; setIndex >= 0; setIndex--)
        {
            DistractionSoundSet soundSet = distractionSoundSets[setIndex];
            if (!soundSet.HasExpired || !AllSourcesSilent(soundSet.Sources))
            {
                continue;
            }

            DestroyDistractionSoundSet(soundSet);
            distractionSoundSets.RemoveAt(setIndex);
        }
    }

    private static bool AllSourcesSilent(AudioSource[] sources)
    {
        foreach (AudioSource audioSource in sources)
        {
            if (audioSource != null && audioSource.volume > 0f)
            {
                return false;
            }
        }

        return true;
    }

    private void StopDistractionSoundSetsImmediately()
    {
        foreach (DistractionSoundSet soundSet in distractionSoundSets)
        {
            DestroyDistractionSoundSet(soundSet);
        }

        distractionSoundSets.Clear();
        distractionFocusTimer = 0f;
        distractionSetsHaveAppeared = false;
    }

    private void DestroyDistractionSoundSet(DistractionSoundSet soundSet)
    {
        if (soundSet == null) return;

        foreach (AudioSource audioSource in soundSet.Sources)
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                Destroy(audioSource);
            }
        }
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
