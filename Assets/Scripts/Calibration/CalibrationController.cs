using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CalibrationController : MonoBehaviour
{
    private const string MenuSceneName = "Menu";

    [Header("Panels")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject finishPanel;

    [Header("Controls")]
    [SerializeField] private Button beginButton;

    [Header("Timing")]
    [SerializeField, Range(10, 300)] private int calibrationDurationSeconds = 60;

    private Coroutine calibrationRoutine;

    private float DurationSeconds => Mathf.Max(1, calibrationDurationSeconds);

    private void OnValidate()
    {
        calibrationDurationSeconds = Mathf.Clamp(calibrationDurationSeconds, 10, 300);
    }

    private void Awake()
    {
        EnsureReferences();

        if (beginButton != null)
        {
            beginButton.onClick.RemoveListener(StartCalibration);
            if (beginButton.onClick.GetPersistentEventCount() == 0)
            {
                beginButton.onClick.AddListener(StartCalibration);
            }
        }
    }

    private void Start()
    {
        ResetCalibrationUI();
    }

    public void StartCalibration()
    {
        if (calibrationRoutine != null)
        {
            StopCoroutine(calibrationRoutine);
        }

        Debug.Log($"Starting calibration for {calibrationDurationSeconds} seconds.", this);
        calibrationRoutine = StartCoroutine(CalibrationRoutine());
    }

    public void LoadMenu()
    {
        if (calibrationRoutine != null)
        {
            StopCoroutine(calibrationRoutine);
            calibrationRoutine = null;
        }

        SceneManager.LoadScene(MenuSceneName);
    }

    private IEnumerator CalibrationRoutine()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (finishPanel != null) finishPanel.SetActive(false);
        OnCalibrationStarted();

        float elapsedSeconds = 0f;
        while (elapsedSeconds < DurationSeconds)
        {
            elapsedSeconds += Time.deltaTime;
            yield return null;
        }

        OnCalibrationFinished();
        if (finishPanel != null) finishPanel.SetActive(true);
        calibrationRoutine = null;
    }

    private void OnCalibrationStarted()
    {
        // FOR WEYMAN: Add calibration-start integration code here.
    }

    private void OnCalibrationFinished()
    {
        // FOR WEYMAN: Add calibration-finished integration code here.
    }

    private void ResetCalibrationUI()
    {
        if (calibrationRoutine != null)
        {
            StopCoroutine(calibrationRoutine);
            calibrationRoutine = null;
        }

        if (startPanel != null) startPanel.SetActive(true);
        if (finishPanel != null) finishPanel.SetActive(false);
    }

    private void EnsureReferences()
    {
        if (beginButton == null)
        {
            GameObject beginObject = GameObject.Find("Begin");
            if (beginObject != null) beginButton = beginObject.GetComponent<Button>();
        }
    }
}
