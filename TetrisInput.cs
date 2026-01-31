using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;
using System.Text;

namespace Tetris
{
    public class TetrisInput
    {
        public bool Left;
        public bool Right;
        public bool Down;
        public bool RotateCW;
        public bool RotateCCW;
        public bool Hold;
        public bool HardDrop;
        public bool Start;
        public bool Back;
        // Helper function inside TetrisInput or a Utility class
        private static bool IsAnyPressed(GamepadState js, GamepadButton[] buttons, bool down = false)
        {
            if (down)
                {foreach (var b in buttons)
                    if (js.ButtonDown(b)) return true; }
            else
                {foreach (var bp in buttons)
                    if (js.ButtonPressed(bp)) return true; }
            return false;
        }

        public static TetrisInput FromDevices(KeyboardState kb, GamepadState js, bool isPlayer2)
        {
            var kbBind = isPlayer2 ? Program.Options.KeyBindingsPlayer2 : Program.Options.KeyBindingsPlayer1;
            var gpBind = isPlayer2 ? Program.Options.GamepadBindingsPlayer2 : Program.Options.GamepadBindingsPlayer1;
            return new TetrisInput
            {
                // BUTTONS + AXES
                Down = kb.IsKeyDown(kbBind.Down)
                       || IsAnyPressed(js, gpBind.Down, true),

                Left = kb.IsKeyDown(kbBind.Left)
                       || IsAnyPressed(js, gpBind.Left, true),

                Right = kb.IsKeyDown(kbBind.Right)
                        || IsAnyPressed(js, gpBind.Right, true),

                Hold = kb.IsKeyPressed(kbBind.Hold)
                       || IsAnyPressed(js, gpBind.Hold),

                RotateCW = kb.IsKeyPressed(kbBind.RotateCW)
                           || IsAnyPressed(js, gpBind.RotateCW),

                RotateCCW = kb.IsKeyPressed(kbBind.RotateCCW)
                            || IsAnyPressed(js, gpBind.RotateCCW),

                HardDrop = kb.IsKeyPressed(kbBind.HardDrop)
                           || IsAnyPressed(js, gpBind.HardDrop),

                Start = kb.IsKeyPressed(kbBind.Start) || js.ButtonPressed(GamepadButton.Start),
                Back = kb.IsKeyPressed(kbBind.Back) || js.ButtonPressed(GamepadButton.Back),
            };
        }
    }
    public class KeyBindings
    {
        public Keys Left = Keys.Left;
        public Keys Right = Keys.Right;
        public Keys Down = Keys.Down;
        public Keys RotateCW = Keys.Up;
        public Keys RotateCCW = Keys.Z;
        public Keys Hold = Keys.C;
        public Keys HardDrop = Keys.Space;
        public Keys Start = Keys.Enter;
        public Keys Back = Keys.Escape;
    }
    public class GamepadBindings
    {
        // Use an array so we can store multiple "acceptable" buttons for one action
        public GamepadButton[] Left = { GamepadButton.DPadLeft  , GamepadButton.LeftStickLeft   };
        public GamepadButton[] Right = { GamepadButton.DPadRight, GamepadButton.LeftStickRight };
        public GamepadButton[] Down = { GamepadButton.DPadDown  , GamepadButton.LeftStickDown };
        public GamepadButton[] RotateCW = { GamepadButton.A };
        public GamepadButton[] RotateCCW = { GamepadButton.B };
        public GamepadButton[] Hold = { GamepadButton.LeftShoulder , GamepadButton.LeftTrigger };
        public GamepadButton[] HardDrop = { GamepadButton.DPadUp   , GamepadButton.LeftStickUp };
    }
    public static class MenuInputMapper
    {
        public static MenuInput FromDevices(KeyboardState kb, GamepadState js)
        {
            return new MenuInput
            {
                Up = kb.IsKeyPressed(Keys.Up) || js.ButtonPressed(GamepadButton.DPadUp),
                Down = kb.IsKeyPressed(Keys.Down) || js.ButtonPressed(GamepadButton.DPadDown),
                Left = kb.IsKeyPressed(Keys.Left) || js.ButtonPressed(GamepadButton.DPadLeft),
                Right = kb.IsKeyPressed(Keys.Right) || js.ButtonPressed(GamepadButton.DPadRight),

                Confirm = kb.IsKeyPressed(Keys.Enter) || js.ButtonPressed(GamepadButton.A),
                Back = kb.IsKeyPressed(Keys.Escape) || js.ButtonPressed(GamepadButton.Back),
                Start = kb.IsKeyPressed(Keys.Enter) || js.ButtonPressed(GamepadButton.Start),
            };
        }
    }
    public struct MenuInput
    {
        public bool Up;
        public bool Down;
        public bool Left;
        public bool Right;
        public bool Confirm;
        public bool Back;
        public bool Start;
    }

}
