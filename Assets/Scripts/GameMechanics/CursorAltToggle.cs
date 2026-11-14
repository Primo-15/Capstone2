using UnityEngine;
using UnityEngine.InputSystem;

public class CursorAltToggle : MonoBehaviour
{
    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.leftAltKey.isPressed)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = false;
        }
    }
}