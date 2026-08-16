using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class OpenTrainScript : MonoBehaviour
{
    private const string MenuSceneName = "Menu";
    private const string MenuXSceneName = "MenuX";
    private const string StandardTrainingSceneName = "Train";
    private const string RealisticTrainingSceneName = "TrainRealistic";
    private const string LowPolyTrainingSceneName = "TrainLowPoly";
    private const string CalibrationSceneName = "Calibration";

    public GameObject MenuUI;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ConfigureTrainingMenuButtons();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void LoadTrain()
    {
        Debug.Log("train pressed");
        LoadTrainingScene(StandardTrainingSceneName);
    }

    public void LoadTrainRealistic()
    {
        Debug.Log("train realistic pressed");
        LoadTrainingScene(RealisticTrainingSceneName);
    }

    public void LoadTrainLowPoly()
    {
        Debug.Log("train low poly pressed");
        LoadTrainingScene(LowPolyTrainingSceneName);
    }

    public void LoadTrainingScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("No training scene name provided.");
            return;
        }

        TrainingSessionLogger.StartSession();
        SceneManager.LoadScene(sceneName);
    }

    public void LoadTest()
    {
        Debug.Log("test button pressed; loading realistic training scene");
        LoadTrainRealistic();
    }

    public void LoadCalibration()
    {
        Debug.Log("calibration pressed");
        SceneManager.LoadScene(CalibrationSceneName);
    }

    public void LoadMenu()
    {
        Debug.Log("menu pressed");
        FinalizeActiveTrainingModes();
        TrainingSessionLogger.EndSession();
        SceneManager.LoadScene(MenuSceneName);
    }

    private void OnApplicationQuit()
    {
        FinalizeActiveTrainingModes();
        TrainingSessionLogger.EndSession();
    }

    private void FinalizeActiveTrainingModes()
    {
        TrainingMode[] trainingModes = FindObjectsByType<TrainingMode>(FindObjectsSortMode.None);
        foreach (TrainingMode trainingMode in trainingModes)
        {
            if (trainingMode != null && trainingMode.IsTrainingActive)
            {
                trainingMode.ResetTrainingUI();
            }
        }
    }

    public void RemoveUIElement()
    {
        if (MenuUI != null)
        {
            // Deactivate the GameObject to hide it
            MenuUI.SetActive(false);
            // Alternatively, use Destroy(uiElementToRemove) to permanently remove it from the scene
        }
    }

    private void ConfigureTrainingMenuButtons()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        if (activeSceneName != MenuSceneName && activeSceneName != MenuXSceneName)
        {
            return;
        }

        Button standardButton = FindMenuButton("Train");
        Button realisticButton = FindMenuButton("TrainRealistic") ?? FindMenuButton("Test");

        if (standardButton == null || realisticButton == null)
        {
            Debug.LogWarning("Could not find existing Train/Test menu buttons to configure.");
            return;
        }

        ConfigureButton(standardButton, "Train", "Train", LoadTrain);
        ConfigureButton(realisticButton, "TrainRealistic", "Realistic", LoadTrainRealistic);

        Button lowPolyButton = FindMenuButton("TrainLowPoly");
        if (lowPolyButton == null)
        {
            lowPolyButton = Instantiate(realisticButton, realisticButton.transform.parent);
        }

        ConfigureButton(lowPolyButton, "TrainLowPoly", "Low Poly", LoadTrainLowPoly);
        PositionTrainingButtons(standardButton, realisticButton, lowPolyButton);
    }

    private static Button FindMenuButton(string objectName)
    {
        GameObject buttonObject = GameObject.Find(objectName);
        return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
    }

    private static void ConfigureButton(Button button, string objectName, string label, UnityEngine.Events.UnityAction action)
    {
        button.gameObject.name = objectName;
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);

        RectTransform rectTransform = button.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(220f, rectTransform.sizeDelta.y);
        }

        Text labelText = button.GetComponentInChildren<Text>();
        if (labelText != null)
        {
            labelText.text = label;
            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = 12;
            labelText.resizeTextMaxSize = labelText.fontSize;
        }
    }

    private static void PositionTrainingButtons(Button standardButton, Button realisticButton, Button lowPolyButton)
    {
        RectTransform standardTransform = standardButton.GetComponent<RectTransform>();
        RectTransform realisticTransform = realisticButton.GetComponent<RectTransform>();
        RectTransform lowPolyTransform = lowPolyButton.GetComponent<RectTransform>();

        if (standardTransform == null || realisticTransform == null || lowPolyTransform == null)
        {
            return;
        }

        Vector2 step = realisticTransform.anchoredPosition - standardTransform.anchoredPosition;
        if (step.sqrMagnitude < 1f)
        {
            step = new Vector2(0f, -28f);
        }

        lowPolyTransform.anchoredPosition = realisticTransform.anchoredPosition + step;
        lowPolyTransform.localScale = realisticTransform.localScale;
        lowPolyTransform.localRotation = realisticTransform.localRotation;
    }
}
