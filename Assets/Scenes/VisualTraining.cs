using UnityEngine;
using System.Collections;

public class VisualTraining : TrainingMode
{
    [Header("Focus Source")]
    [SerializeField] private DataReceiverScript dataReceiver;

    [Header("Animal Feedback")]
    [SerializeField] private GameObject[] trainingAnimals;
    [SerializeField, Range(1f, 30f)] private float focusSecondsPerAnimal = 5f;
    [SerializeField, Range(0.1f, 5f)] private float fadeInSeconds = 0.75f;
    [SerializeField, Range(0.1f, 5f)] private float fadeOutSeconds = 1.5f;

    private TrainingAnimalVisual[] animalVisuals;
    private float focusTimer;

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
        focusTimer = 0f;
        HideAnimalsImmediately();
        base.StartTraining();
    }

    protected override IEnumerator TrainingRoutine()
    {
        yield return base.TrainingRoutine();
        focusTimer = 0f;
        HideAnimalsImmediately();
    }

    private void Update()
    {
        if (!IsTrainingActive)
        {
            FadeAnimalsToward(0);
            return;
        }

        bool isFocused = dataReceiver != null && dataReceiver.IsFocused;

        if (isFocused)
        {
            focusTimer += Time.deltaTime;
        }
        else
        {
            focusTimer = 0f;
        }

        int visibleAnimalCount = Mathf.Clamp(
            Mathf.FloorToInt(focusTimer / focusSecondsPerAnimal),
            0,
            animalVisuals == null ? 0 : animalVisuals.Length);

        FadeAnimalsToward(visibleAnimalCount);
    }

    public override void ResetTrainingUI()
    {
        base.ResetTrainingUI();
        focusTimer = 0f;
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

    private void FadeAnimalsToward(int visibleAnimalCount)
    {
        if (animalVisuals == null)
        {
            return;
        }

        for (int i = 0; i < animalVisuals.Length; i++)
        {
            TrainingAnimalVisual visual = animalVisuals[i];
            if (visual == null) continue;

            float targetVisibility = i < visibleAnimalCount ? 1f : 0f;
            float fadeSeconds = targetVisibility > visual.Visibility ? fadeInSeconds : fadeOutSeconds;
            visual.MoveVisibilityToward(targetVisibility, fadeSeconds);
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
            }
        }
    }
}
