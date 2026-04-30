using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class InputHandler : MonoBehaviour
{
    public static event Action<Vector2> OnClick;

    void Update()
    {
        //Touch FIRST (priority)
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            OnClick?.Invoke(Touchscreen.current.primaryTouch.position.ReadValue());
            return; //IMPORTANT — stops double call
        }

        //Mouse
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            OnClick?.Invoke(Mouse.current.position.ReadValue());
        }
    }
}