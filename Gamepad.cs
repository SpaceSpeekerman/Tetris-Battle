 using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using static System.Reflection.Metadata.BlobBuilder;

public class Gamepad
{
    public int ControllerIndex = 0;

    public bool AllowRepeat = true;         // can auto repeat after pressing
    public float InitialDelay = 0.33f;      // seconds before repeating
    public float RepeatInterval = 0.066f;   // interval between repeats

    public float stickThreshold = 0.8f;     // treshold to act as a button

    public Action<GamepadButton> OnButtonTriggered;

    private Dictionary<GamepadButton, float> holdTimers = new Dictionary<GamepadButton, float>();
    private Dictionary<GamepadButton, bool> wasPressed = new Dictionary<GamepadButton, bool>();
    GamepadState currentState = new GamepadState();
    public Gamepad(int controllerIndex)
    {
        ControllerIndex = controllerIndex;
    }
    public GamepadState Update(float deltaTime) // DELTA TIME!!!
    {
        XInputState state = new XInputState();
        if (XInputGetState(ControllerIndex, ref state) != 0) // Controller not connected
            return new GamepadState();
        currentState = new GamepadState();
        currentState.LeftStick = new Vector2(NormalizeStickValue(state.Gamepad.sThumbLX), NormalizeStickValue(state.Gamepad.sThumbLY));
        currentState.RightStick = new Vector2(NormalizeStickValue(state.Gamepad.sThumbRX), NormalizeStickValue(state.Gamepad.sThumbRY));
        currentState.LeftTrigger = state.Gamepad.bLeftTrigger / 255f; // Normalize to range [0, 1]
        currentState.RightTrigger = state.Gamepad.bRightTrigger / 255f; // Normalize to range [0, 1]

        // Left stick directions
        if (currentState.LeftStick.Y > stickThreshold)
            currentState.Pressed.Add(GamepadButton.LeftStickUp);
        if (currentState.LeftStick.Y < -stickThreshold)
            currentState.Pressed.Add(GamepadButton.LeftStickDown);
        if (currentState.LeftStick.X < -stickThreshold)
            currentState.Pressed.Add(GamepadButton.LeftStickLeft);
        if (currentState.LeftStick.X > stickThreshold)
            currentState.Pressed.Add(GamepadButton.LeftStickRight);

        // Right stick directions
        if (currentState.RightStick.Y > stickThreshold)
            currentState.Pressed.Add(GamepadButton.RightStickUp);
        if (currentState.RightStick.Y < -stickThreshold)
            currentState.Pressed.Add(GamepadButton.RightStickDown);
        if (currentState.RightStick.X < -stickThreshold)
            currentState.Pressed.Add(GamepadButton.RightStickLeft);
        if (currentState.RightStick.X > stickThreshold)
            currentState.Pressed.Add(GamepadButton.RightStickRight);

        if (currentState.LeftTrigger > stickThreshold)
            currentState.Pressed.Add(GamepadButton.LeftTrigger);
        if (currentState.RightTrigger > stickThreshold)
            currentState.Pressed.Add(GamepadButton.RightTrigger);


        foreach (GamepadButton button in Enum.GetValues(typeof(GamepadButton)))
        {
            bool isPressed;
            if (IsPhysicalButton(button))
            {
                isPressed = (state.Gamepad.wButtons & (ushort)button) != 0;
            }
            else
            {
                // Check stick axes for virtual buttons
                isPressed = currentState.Pressed.Contains(button);
            }
            if (!wasPressed.ContainsKey(button))
                wasPressed[button] = false;

            if (!holdTimers.ContainsKey(button))
                holdTimers[button] = 0;

            if (isPressed)
            {
                currentState.Pressed.Add(button);
                if (!wasPressed[button])
                {
                    // Just pressed
                    currentState.JustPressed.Add(button);

                    //          Console.WriteLine(button.ToString());
                    OnButtonTriggered?.Invoke(button);
                    holdTimers[button] = 0;
                }
                else
                {
                    holdTimers[button] += deltaTime;

                    if (holdTimers[button] >= InitialDelay && AllowRepeat)
                    {
                        // Fire repeatedly every RepeatInterval
                        float overshoot = holdTimers[button] - InitialDelay;
                        if (overshoot % RepeatInterval < deltaTime)
                        {
                            //      Console.WriteLine(button.ToString());
                            currentState.JustPressed.Add(button);
                            OnButtonTriggered?.Invoke(button);
                        }
                    }
                }
            }
            else
            {
                // Reset if released
                holdTimers[button] = 0;
            }

            wasPressed[button] = isPressed;


        }


        return currentState;
    }
    bool IsPhysicalButton(GamepadButton button)
    {
        return (ushort)button <= 0x8000;
    }
    public bool IsButtonDown(GamepadButton button)
    {
        XInputState state = new XInputState();
        if (XInputGetState(ControllerIndex, ref state) != 0) // Controller not connected
            return false;
        return (state.Gamepad.wButtons & (ushort)button) != 0;
    }
    public bool ButtonPressed(GamepadButton button)
    {
        XInputState state = new XInputState();
        if (XInputGetState(ControllerIndex, ref state) != 0) // Controller not connected
            return false;
        if ((state.Gamepad.wButtons & (ushort)button) != 0)
        {
            return !wasPressed[button];//&& wasPressed[button];
        }
        return false;
    }

    public (float x, float y) GetLeftStick()
    {
        XInputState state = new XInputState();
        if (XInputGetState(ControllerIndex, ref state) != 0) // Controller not connected
            return (0, 0);

        float x = NormalizeStickValue(state.Gamepad.sThumbLX);
        float y = NormalizeStickValue(state.Gamepad.sThumbLY);
        return (x, y);
    }

    public (float x, float y) GetRightStick()
    {
        XInputState state = new XInputState();
        if (XInputGetState(ControllerIndex, ref state) != 0) // Controller not connected
            return (0, 0);

        float x = NormalizeStickValue(state.Gamepad.sThumbRX);
        float y = NormalizeStickValue(state.Gamepad.sThumbRY);
        return (x, y);
    }

    public float GetLeftTrigger()
    {
        XInputState state = new XInputState();
        if (XInputGetState(ControllerIndex, ref state) != 0) // Controller not connected
            return 0;

        return state.Gamepad.bLeftTrigger / 255f; // Normalize to range [0, 1]
    }

    public float GetRightTrigger()
    {
        XInputState state = new XInputState();
        if (XInputGetState(ControllerIndex, ref state) != 0) // Controller not connected
            return 0;

        return state.Gamepad.bRightTrigger / 255f; // Normalize to range [0, 1]
    }

    private float NormalizeStickValue(short value)
    {
        const float maxStickValue = 32767f; // Maximum value for a stick axis  
        return Clamp(value / (float)maxStickValue, -1f, 1f); // Normalize to range [-1, 1]  
    }

    public static float Clamp(float value, float min, float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    // XInput API declarations
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState(int dwUserIndex, ref XInputState pState);

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint dwPacketNumber;
        public XInputGamepad Gamepad;
    }
}

public class GamepadState
{
    public List<GamepadButton> JustPressed = new List<GamepadButton>();
    public List<GamepadButton> Pressed = new List<GamepadButton>();
    public Vector2 LeftStick = new Vector2(0, 0);
    public Vector2 RightStick = new Vector2(0, 0);
    public float LeftTrigger = 0;
    public float RightTrigger = 0;
    public GamepadState()
    {

    }
    public bool ButtonDown(GamepadButton button)
    {
        return Pressed.Contains(button);
    }
    public bool ButtonPressed(GamepadButton button)
    {
        return JustPressed.Contains(button);
    }
}

[StructLayout(LayoutKind.Sequential)]
struct XInputGamepad
{
    public ushort wButtons;
    public byte bLeftTrigger;
    public byte bRightTrigger;
    public short sThumbLX;
    public short sThumbLY;
    public short sThumbRX;
    public short sThumbRY;
}

//[Flags]
public enum GamepadButton : uint
{
    // No buttons pressed
    None = 0,

    // Physical Buttons (Standard XInput / HID mapping)
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftThumb = 0x0040,
    RightThumb = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,

    LeftStickUp ,
    LeftStickDown ,
    LeftStickLeft ,
    LeftStickRight ,

    RightStickUp,
    RightStickDown ,
    RightStickLeft ,
    RightStickRight,

    LeftTrigger,
    RightTrigger
}