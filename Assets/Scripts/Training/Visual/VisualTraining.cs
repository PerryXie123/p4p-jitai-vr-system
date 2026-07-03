using UnityEngine;
using System.Collections;

public class VisualTraining : TrainingMode
{
    [Header("Focus Source")]
    [SerializeField] private DataReceiverScript dataReceiver;

    [Header("Animal Feedback")]
    [SerializeField] private GameObject[] trainingAnimals;
    [SerializeField, Range(0f, 30f)] private float focusSecondsBeforeAnimalsAppear = 5f;
    [SerializeField, Range(0.1f, 5f)] private float fadeInSeconds = 0.75f;
    [SerializeField, Range(0.1f, 5f)] private float fadeOutSeconds = 2f;

    private TrainingAnimalVisual[] animalVisuals;
    private float focusTimer;
    private bool animalsHaveAppeared;
    private bool positionsResetAfterFade;

    private void Awake()
    {
        if (dataReceiver == null)
        {
            dataReceiver = FindFirstObjectByType<DataReceiverScript>();
        }

        PrepareAnimals();
        HideAnimalsImmediately();
    }

    public override void StartTraining()
    {
        Debug.Log("Starting visual training");
        HideAnimalsImmediately();
        base.StartTraining();
    }

    protected override IEnumerator TrainingRoutine()
    {
        yield return base.TrainingRoutine();
        HideAnimalsImmediately();
    }

    private void Update()
    {
        if (!IsTrainingActive)
        {
            FadeAnimalsToward(false);
            return;
        }

        bool isFocused = dataReceiver != null && dataReceiver.IsFocused;

        if (!isFocused)
        {
            focusTimer = 0f;
            FadeAnimalsToward(false);
            return;
        }

        // The five-second dwell gates the first appearance. If focus returns
        // during a fade-out, animalsHaveAppeared is still true, so the fade
        // reverses immediately instead of requiring another five seconds.
        if (!animalsHaveAppeared)
        {
            focusTimer += Time.deltaTime;
            animalsHaveAppeared = focusTimer >= focusSecondsBeforeAnimalsAppear;
        }

        FadeAnimalsToward(animalsHaveAppeared);
    }

    public override void ResetTrainingUI()
    {
        base.ResetTrainingUI();
        HideAnimalsImmediately();
    }

    private void PrepareAnimals()
    {
        if (trainingAnimals == null)
        {
            animalVisuals = new TrainingAnimalVisual[0];
            return;
        }

        animalVisuals = new TrainingAnimalVisual[trainingAnimals.Length];

        for (int i = 0; i < trainingAnimals.Length; i++)
        {
            GameObject animal = trainingAnimals[i];
            if (animal == null) continue;

            TrainingAnimalWalker walker = animal.GetComponent<TrainingAnimalWalker>();
            if (walker == null)
            {
                animal.AddComponent<TrainingAnimalWalker>();
            }

            TrainingAnimalVisual visual = animal.GetComponent<TrainingAnimalVisual>();
            if (visual == null)
            {
                visual = animal.AddComponent<TrainingAnimalVisual>();
            }

            animalVisuals[i] = visual;
        }
    }

    private void FadeAnimalsToward(bool shouldBeVisible)
    {
        if (animalVisuals == null)
        {
            return;
        }

        if (shouldBeVisible)
        {
            positionsResetAfterFade = false;
        }

        bool allAnimalsHidden = true;

        for (int i = 0; i < animalVisuals.Length; i++)
        {
            TrainingAnimalVisual visual = animalVisuals[i];
            if (visual == null) continue;

            float targetVisibility = shouldBeVisible ? 1f : 0f;
            float fadeSeconds = targetVisibility > visual.Visibility ? fadeInSeconds : fadeOutSeconds;
            visual.MoveVisibilityToward(targetVisibility, fadeSeconds);
            allAnimalsHidden &= visual.Visibility <= 0f;
        }

        if (!shouldBeVisible && allAnimalsHidden && !positionsResetAfterFade)
        {
            ResetAnimalPositions();
            focusTimer = 0f;
            animalsHaveAppeared = false;
            positionsResetAfterFade = true;
        }
    }

    private void ResetAnimalPositions()
    {
        foreach (TrainingAnimalVisual visual in animalVisuals)
        {
            if (visual != null)
            {
                visual.ResetToStartingPosition();
            }
        }
    }

    private void HideAnimalsImmediately()
    {
        if (animalVisuals == null)
        {
            return;
        }

        foreach (TrainingAnimalVisual visual in animalVisuals)
        {
            if (visual != null)
            {
                visual.SetVisibility(0f);
                visual.ResetToStartingPosition();
            }
        }

        focusTimer = 0f;
        animalsHaveAppeared = false;
        positionsResetAfterFade = true;
    }
}
