using UnityEngine;
using System.Collections;

public abstract class TrainingMode : MonoBehaviour
{
    [SerializeField] protected GameObject orb;
    [SerializeField] protected GameObject startPanel;
    [SerializeField] protected GameObject finishPanel;
    [SerializeField] protected GameObject focusText;

    public virtual void StartTraining()
    {
        StartCoroutine(TrainingRoutine());
    }

    protected virtual IEnumerator TrainingRoutine()
    {
        if (orb == null || startPanel == null || finishPanel == null || focusText == null)
        {
            Debug.LogError("Missing references.", this);
            yield break;
        }

        startPanel.SetActive(false);
        finishPanel.SetActive(false);

        orb.SetActive(true);
        focusText.SetActive(true);   // 👈 SHOW when training starts

        yield return new WaitForSeconds(30f);

        orb.SetActive(false);
        focusText.SetActive(false);  // 👈 HIDE when training ends

        finishPanel.SetActive(true);
    }

    public virtual void ResetTrainingUI()
    {
        if (orb != null) orb.SetActive(false);
        if (startPanel != null) startPanel.SetActive(true);
        if (finishPanel != null) finishPanel.SetActive(false);
        if (focusText != null) focusText.SetActive(false); // 👈 ensure hidden
    }
}