using UnityEngine;
using System.Collections;

public abstract class TrainingMode : MonoBehaviour
{
    [SerializeField] protected GameObject orb;
    [SerializeField] protected GameObject startPanel;
    [SerializeField] protected GameObject finishPanel;

    public virtual void StartTraining()
    {
        StartCoroutine(TrainingRoutine());
    }

    protected virtual IEnumerator TrainingRoutine()
    {
        if (orb == null || startPanel == null || finishPanel == null)
        {
            Debug.LogError("Orb, Start Panel, or Finish Panel is not assigned.", this);
            yield break;
        }

        startPanel.SetActive(false);
        finishPanel.SetActive(false);
        orb.SetActive(true);

        yield return new WaitForSeconds(10f);

        orb.SetActive(false);
        finishPanel.SetActive(true);
    }

    public virtual void ResetTrainingUI()
    {
        if (orb != null) orb.SetActive(false);
        if (startPanel != null) startPanel.SetActive(true);
        if (finishPanel != null) finishPanel.SetActive(false);
    }
}