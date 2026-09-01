using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class VisualTraining : TrainingMode
{
    [Header("Focus Source")]
    [SerializeField] private DataReceiverScript dataReceiver;

    [Header("Animal Feedback")]
    [SerializeField] private GameObject[] trainingAnimals;
    [SerializeField, Range(0f, 30f)] private float focusSecondsBeforeAnimalsAppear = 5f;
    [SerializeField, Range(0.1f, 30f)] private float additionalSetSpawnInterval = 5f;
    [SerializeField, Range(0.1f, 5f)] private float fadeInSeconds = 0.75f;
    [SerializeField, Range(0.1f, 5f)] private float fadeOutSeconds = 2f;
    [SerializeField, Range(0f, 45f)] private float fadeInRotationVariation = 20f;

    private TrainingAnimalVisual[] originalAnimalVisuals;
    private readonly List<TrainingAnimalVisual[]> animalSets = new();
    private float focusTimer;
    private bool animalSetsHaveAppeared;
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

        focusTimer += Time.deltaTime;
        float spawnInterval = animalSetsHaveAppeared
            ? additionalSetSpawnInterval
            : Mathf.Max(0.01f, focusSecondsBeforeAnimalsAppear);

        if (focusTimer >= spawnInterval)
        {
            focusTimer -= spawnInterval;

            if (!animalSetsHaveAppeared)
            {
                animalSetsHaveAppeared = true;
            }
            else
            {
                SpawnAnimalSet();
            }
        }

        // Refocusing during a fade restores every existing set immediately.
        FadeAnimalsToward(animalSetsHaveAppeared);
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
            originalAnimalVisuals = new TrainingAnimalVisual[0];
            return;
        }

        originalAnimalVisuals = new TrainingAnimalVisual[trainingAnimals.Length];

        for (int i = 0; i < trainingAnimals.Length; i++)
        {
            GameObject animal = trainingAnimals[i];
            if (animal == null) continue;

            originalAnimalVisuals[i] = PrepareAnimal(animal);
        }

        animalSets.Add(originalAnimalVisuals);
    }

    private static TrainingAnimalVisual PrepareAnimal(GameObject animal)
    {
        if (animal.GetComponent<TrainingAnimalWalker>() == null)
        {
            animal.AddComponent<TrainingAnimalWalker>();
        }

        TrainingAnimalVisual visual = animal.GetComponent<TrainingAnimalVisual>();
        return visual != null ? visual : animal.AddComponent<TrainingAnimalVisual>();
    }

    private void SpawnAnimalSet()
    {
        TrainingAnimalVisual[] newSet = new TrainingAnimalVisual[trainingAnimals.Length];
        int setNumber = animalSets.Count + 1;

        for (int i = 0; i < trainingAnimals.Length; i++)
        {
            GameObject sourceAnimal = trainingAnimals[i];
            TrainingAnimalVisual sourceVisual = originalAnimalVisuals[i];
            if (sourceAnimal == null || sourceVisual == null) continue;

            GameObject copy = Instantiate(sourceAnimal, sourceAnimal.transform.parent);
            copy.name = $"{sourceAnimal.name} (Training Set {setNumber})";

            TrainingAnimalVisual copyVisual = PrepareAnimal(copy);
            copyVisual.CopyStartingPoseFrom(sourceVisual);
            copyVisual.SetVisibility(0f);
            newSet[i] = copyVisual;
        }

        animalSets.Add(newSet);
    }

    private void FadeAnimalsToward(bool shouldBeVisible)
    {
        if (originalAnimalVisuals == null)
        {
            return;
        }

        if (shouldBeVisible)
        {
            positionsResetAfterFade = false;
        }

        bool allAnimalsHidden = true;

        foreach (TrainingAnimalVisual[] animalSet in animalSets)
        {
            foreach (TrainingAnimalVisual visual in animalSet)
            {
                if (visual == null) continue;

                float targetVisibility = shouldBeVisible ? 1f : 0f;
                float fadeSeconds = targetVisibility > visual.Visibility ? fadeInSeconds : fadeOutSeconds;
                visual.MoveVisibilityToward(targetVisibility, fadeSeconds, fadeInRotationVariation);
                allAnimalsHidden &= visual.Visibility <= 0f;
            }
        }

        if (!shouldBeVisible && allAnimalsHidden && !positionsResetAfterFade)
        {
            ResetToOriginalAnimalSet();
            focusTimer = 0f;
            animalSetsHaveAppeared = false;
            positionsResetAfterFade = true;
        }
    }

    private void ResetToOriginalAnimalSet()
    {
        for (int setIndex = animalSets.Count - 1; setIndex >= 1; setIndex--)
        {
            foreach (TrainingAnimalVisual visual in animalSets[setIndex])
            {
                if (visual != null)
                {
                    Destroy(visual.gameObject);
                }
            }

            animalSets.RemoveAt(setIndex);
        }

        foreach (TrainingAnimalVisual visual in originalAnimalVisuals)
        {
            if (visual != null) visual.ResetToStartingPosition();
        }
    }

    private void HideAnimalsImmediately()
    {
        if (originalAnimalVisuals == null)
        {
            return;
        }

        ResetToOriginalAnimalSet();

        foreach (TrainingAnimalVisual visual in originalAnimalVisuals)
        {
            if (visual != null)
            {
                visual.SetVisibility(0f);
                visual.ResetToStartingPosition();
            }
        }

        focusTimer = 0f;
        animalSetsHaveAppeared = false;
        positionsResetAfterFade = true;
    }
}
