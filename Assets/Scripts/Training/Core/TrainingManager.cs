using UnityEngine;

public class TrainingManager : MonoBehaviour
{
    private TrainingMode currentTrainingMode;

    public void SetTrainingMode(TrainingMode mode)
    {
        currentTrainingMode = mode;
    }

    public void StartTraining()
    {
        if (currentTrainingMode == null)
        {
            Debug.LogWarning("No training mode assigned.");
            return;
        }

        currentTrainingMode.StartTraining();
    }

    public void ResetTraining()
    {
        currentTrainingMode?.ResetTrainingUI();
    }
}