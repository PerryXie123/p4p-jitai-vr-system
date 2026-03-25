using UnityEngine;
using UnityEngine.UI;

public class OpenTrainScript : MonoBehaviour
{
    public GameObject MenuUI;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void LoadTrain()
    {
        Debug.Log("train pressed");
        UnityEngine.SceneManagement.SceneManager.LoadScene("Train");
    }

    public void LoadTest()
    {
        Debug.Log("test pressed");
        UnityEngine.SceneManagement.SceneManager.LoadScene("Test");
    }

    public void LoadMenu()
    {
        Debug.Log("menu pressed");
        UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
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
}
