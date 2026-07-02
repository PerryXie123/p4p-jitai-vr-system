using UnityEngine;

public class DebugHeadTracking : MonoBehaviour
{
    void Start()
    {
        Debug.Log("starting");
    }
    void Update()
    {
        Debug.Log(transform.rotation);
    }
}