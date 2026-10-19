using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public static class KeyboardInputUtility
{
    public static bool WasPressed(KeyCode keyCode)
    {
        bool pressed = Input.GetKeyDown(keyCode);

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return pressed;
        }

        Key inputSystemKey = ConvertKey(keyCode);
        return pressed ||
               (inputSystemKey != Key.None &&
                keyboard[inputSystemKey].wasPressedThisFrame);
#else
        return pressed;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static Key ConvertKey(KeyCode keyCode)
    {
        if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
        {
            return Key.A + (keyCode - KeyCode.A);
        }

        switch (keyCode)
        {
            case KeyCode.Alpha0: return Key.Digit0;
            case KeyCode.Alpha1: return Key.Digit1;
            case KeyCode.Alpha2: return Key.Digit2;
            case KeyCode.Alpha3: return Key.Digit3;
            case KeyCode.Alpha4: return Key.Digit4;
            case KeyCode.Alpha5: return Key.Digit5;
            case KeyCode.Alpha6: return Key.Digit6;
            case KeyCode.Alpha7: return Key.Digit7;
            case KeyCode.Alpha8: return Key.Digit8;
            case KeyCode.Alpha9: return Key.Digit9;
            case KeyCode.Keypad0: return Key.Numpad0;
            case KeyCode.Keypad1: return Key.Numpad1;
            case KeyCode.Keypad2: return Key.Numpad2;
            case KeyCode.Keypad3: return Key.Numpad3;
            case KeyCode.Keypad4: return Key.Numpad4;
            case KeyCode.Keypad5: return Key.Numpad5;
            case KeyCode.Keypad6: return Key.Numpad6;
            case KeyCode.Keypad7: return Key.Numpad7;
            case KeyCode.Keypad8: return Key.Numpad8;
            case KeyCode.Keypad9: return Key.Numpad9;
            case KeyCode.Space: return Key.Space;
            case KeyCode.Return: return Key.Enter;
            case KeyCode.KeypadEnter: return Key.NumpadEnter;
            case KeyCode.Escape: return Key.Escape;
            case KeyCode.Backspace: return Key.Backspace;
            default: return Key.None;
        }
    }
#endif
}
