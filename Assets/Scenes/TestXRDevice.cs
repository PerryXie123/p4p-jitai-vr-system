using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class XRDeviceCheck : MonoBehaviour
{
    void Start()
    {
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevices(devices);

        Debug.Log("XR devices found: " + devices.Count);

        foreach (var device in devices)
        {
            Debug.Log("Device: " + device.name + " | Role: " + device.characteristics);
        }
    }
}