using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using Tetris.OpenGL;
using Tetris.Audio;

namespace Tetris
{
    // ===== MENU / OPTIONS =====
    public enum MenuState { None, Pause, Options, Ready, KeyConfig, Countdown }


    public class MenuManager
    {
        public MenuState State = MenuState.Ready;
        public bool GameplayEnabled => State == MenuState.None;
        public bool MultiplayerActive { get; private set; }

        bool p1Ready, p2Ready;
        double countdown = 3.0;

        public event Action OnRestartRequested;

        int pauseIndex;
        string[] pauseItems = { "RESUME", "RESTART", "OPTIONS", "CONTROLS", "QUIT" };
        int optionsIndex = 0;
        string[] optionsItems = { "Sound", "Ghost Piece", "Hold Piece", "NextPiece",
            "Hard Drop", "Line Penalties", "First Player Wins", "Infinite Level" };


        public void Update(double dt, KeyboardState kb, GamepadState p1, GamepadState p2, bool isGameOver)
        {
            // OPEN MENUS
            switch (State)
            {
                case MenuState.None:
                    // Only allow Pause if the game is NOT over
                    if (p1.ButtonPressed(GamepadButton.Start))
                    {
                        if (isGameOver)
                            OnRestartRequested?.Invoke(); // Quick restart on Game Over
                        else
                            State = MenuState.Pause;
                    }
                    break;
                case MenuState.Ready:
                    if (p1.ButtonPressed(GamepadButton.Start)) p1Ready = true;
                    if (p2.ButtonPressed(GamepadButton.Start)) { p2Ready = true; MultiplayerActive = true; }

                    if (p1Ready && (!MultiplayerActive || p2Ready))
                        State = MenuState.Countdown;
                    break;
                case MenuState.Countdown:
                    countdown -= dt;
                    if (countdown <= 0)
                    {
                        State = MenuState.None;
                        countdown = 3.0;
                    }
                    break;
                case MenuState.Pause:
                    UpdatePauseMenu(kb, p1);
                    break;
                case MenuState.Options:
                    UpdateOptionsMenu(kb, p1);
                    break;
                case MenuState.KeyConfig:
                    UpdateKeyConfig(kb, p1);
                    break;
            }
        }
        void UpdatePauseMenu(KeyboardState kb, GamepadState p1)
        {
            // navigazione su
            if (kb.IsKeyPressed(Keys.Space)
                           || p1.ButtonPressed(GamepadButton.DPadUp)
                           || p1.ButtonPressed(GamepadButton.LeftStickUp))
            {
                pauseIndex--;
                if (pauseIndex < 0)
                    pauseIndex = pauseItems.Length - 1;

            }
            // navigazione giù
            else if (kb.IsKeyPressed(Keys.Down)
                       || p1.ButtonPressed(GamepadButton.DPadDown)
                       || p1.ButtonPressed(GamepadButton.LeftStickDown))
            {
                pauseIndex++;
                if (pauseIndex >= pauseItems.Length)
                    pauseIndex = 0;

            }
            // conferma (A / invio)
            else if (kb.IsKeyPressed(Keys.Enter)
                       || p1.ButtonPressed(GamepadButton.A))
            {
                switch (pauseIndex)
                {
                    case 0: // RESUME
                        State = MenuState.None;
                        break;

                    case 1: // RESTART
                        OnRestartRequested?.Invoke();
                        State = MenuState.Countdown;
                        p1Ready = true;
                        p2Ready = MultiplayerActive;
                        countdown = 3.0;
                        break;

                    case 2: // OPTIONS
                        State = MenuState.Options;
                        break;

                    case 3: // QUIT
                        State = MenuState.KeyConfig;
                        break;
                    case 4: // QUIT
                        Environment.Exit(0);
                        break;
                }
            }
            // start = resume immediato
            else if (kb.IsKeyPressed(Keys.Escape)
                       || p1.ButtonPressed(GamepadButton.Start) || p1.ButtonPressed(GamepadButton.Back))
            {
                State = MenuState.None;
            }
        }

        public void Render(TextRenderer text)
        {
            switch (State)
            {
                case MenuState.Ready:
                    text.Print("PRESS START", 12, 14, 0.75f, Vector4.One, Vector4.Zero);
                    break;

                case MenuState.Countdown:
                    text.Print(((int)Math.Ceiling(countdown)).ToString(), 16, 14, 1.2f, Vector4.One, Vector4.Zero);
                    break;

                case MenuState.Pause:
                    for (int i = 0; i < pauseItems.Length; i++)
                        text.Print(pauseItems[i], 14, 5 + i * 2, 0.75f,
                            i == pauseIndex ? new Vector4(1, 1, 0, 1) : Vector4.One,
                            Vector4.Zero);
                    break;
                case MenuState.Options:
                    RenderOptionsMenu(text);
                    break;
                case MenuState.KeyConfig:
                    RenderKeyConfig(text);
                    break;
            }
        }
        void RenderOptionsMenu(TextRenderer text)
        {
            text.Print(
                "OPTIONS",
                6,
                3,
                1.2f,
                Vector4.One,
                Vector4.Zero
            );

            for (int i = 0; i < optionsItems.Length; i++)
            {
                bool value = GetOptionValue(i);

                string line =
                    $"{(i == optionsIndex ? ">" : " ")}" +
                    $"[{(value ? "*" : " ")}] {optionsItems[i]}";

                text.Print(
                    line,
                    10,
                    5 + i * 1.5f,
                    0.9f,
                    i == optionsIndex ? new Vector4(1, 1, 0, 1) : Vector4.One,
                    Vector4.Zero
                );
            }

            text.Print(
                "A / ENTER : TOGGLE   BACK / ESC : RETURN",
                12,
                18,

                0.6f,
                Vector4.One,
                Vector4.Zero
            );
        }
        void RenderKeyConfig(TextRenderer text)
        {
            text.Print("CONFIGURE KEYS", 6, 2, 1.2f, Vector4.One, Vector4.Zero);

            // Header to show which player is being edited
            string playerHeader = editingPlayer2 ? "< PLAYER 2 >" : "< PLAYER 1 >";
            text.Print(playerHeader, 12, 4, 1.0f, new Vector4(0, 1, 1, 1), Vector4.Zero);

            var b = editingPlayer2 ? Program.Options.KeyBindingsPlayer2 : Program.Options.KeyBindingsPlayer1;
            Keys[] currentKeys = { b.Left, b.Right, b.Down, b.RotateCW, b.RotateCCW, b.Hold, b.HardDrop };

            for (int i = 0; i < keyNames.Length; i++)
            {
                string line = $"{(i == keyIndex ? "> " : "  ")}{keyNames[i]}: {currentKeys[i]}";
                Vector4 color = (i == keyIndex) ? (isListening ? new Vector4(1, 0, 0, 1) : new Vector4(1, 1, 0, 1)) : Vector4.One;

                text.Print(line, 10, 6 + i * 1.5f, 0.8f, color, Vector4.Zero);
            }

            text.Print("LEFT/RIGHT: SWITCH PLAYER", 12, 17, 0.6f, Vector4.One, Vector4.Zero);
            text.Print("ENTER: CHANGE   ESC: BACK", 12, 18.5f, 0.6f, Vector4.One, Vector4.Zero);
        }
        void UpdateOptionsMenu(KeyboardState kb, GamepadState p1)
        {
            // SU
            if (kb.IsKeyPressed(Keys.Up) || p1.ButtonPressed(GamepadButton.DPadUp))
            {
                optionsIndex--;
                if (optionsIndex < 0)
                    optionsIndex = optionsItems.Length - 1;

                Console.WriteLine($"[OPTIONS] Select: {optionsItems[optionsIndex]}");
            }
            // GIÙ
            else if (kb.IsKeyPressed(Keys.Down) || p1.ButtonPressed(GamepadButton.DPadDown))
            {
                optionsIndex++;
                if (optionsIndex >= optionsItems.Length)
                    optionsIndex = 0;

                Console.WriteLine($"[OPTIONS] Select: {optionsItems[optionsIndex]}");
            }
            // TOGGLE (A / ENTER)
            else if (kb.IsKeyPressed(Keys.Enter) || p1.ButtonPressed(GamepadButton.A))
            {
                ToggleOption(optionsIndex);
                Console.WriteLine($"[OPTIONS] Toggle: {optionsItems[optionsIndex]}");
            }
            // BACK → PAUSE
            else if (kb.IsKeyPressed(Keys.Escape) || p1.ButtonPressed(GamepadButton.Back))
            {
                State = MenuState.Pause;
                Console.WriteLine("[OPTIONS] Back to Pause");
            }
        }
        bool GetOptionValue(int index)
        {
            return index switch
            {
                0 => Program.Options.SoundOn,
                1 => Program.Options.GhostPiece,
                2 => Program.Options.HoldPiece,
                3 => Program.Options.NextPiece,
                4 => Program.Options.HardDrop,
                5 => Program.Options.LinePenalties,
                6 => Program.Options.FirstPlayerWins,
                7 => Program.Options.InfiniteLevel,
                _ => false
            };
        }

        void ToggleOption(int index)
        {
            switch (index)
            {
                case 0: Program.Options.SoundOn =           !Program.Options.SoundOn;
                    AduioLibrary.Mute(!Program.Options.SoundOn);
                    break;
                case 1: Program.Options.GhostPiece =        !Program.Options.GhostPiece; break;
                case 2: Program.Options.HoldPiece =         !Program.Options.HoldPiece; break;
                case 3: Program.Options.NextPiece =         !Program.Options.NextPiece; break;
                case 4: Program.Options.HardDrop =          !Program.Options.HardDrop; break;
                case 5: Program.Options.LinePenalties =     !Program.Options.LinePenalties; break;
                case 6: Program.Options.FirstPlayerWins =   !Program.Options.FirstPlayerWins; break;
                case 7: Program.Options.InfiniteLevel =     !Program.Options.InfiniteLevel; break;
            }
        }

        // Inside Menu Class
        private int keyIndex = 0;
        private bool isListening = false;
        private bool editingPlayer2 = false; // Which player are we rebinding?

        private string[] keyNames = {
            "Left", "Right", "Down", "Rotate CW", "Rotate CCW", "Hold", "Hard Drop"
        };

        public void UpdateKeyConfig(KeyboardState kb, GamepadState p1)
        {
            if (isListening)
            {
                foreach (Keys k in Enum.GetValues(typeof(Keys)))
                {
                    if (k != Keys.Unknown && kb.IsKeyPressed(k))
                    {
                        ApplyNewKey(k);
                        isListening = false;
                        break;
                    }
                }
                return;
            }

            // Navigation
            if (kb.IsKeyPressed(Keys.Up) || p1.ButtonPressed(GamepadButton.DPadUp)) keyIndex = (keyIndex - 1 + keyNames.Length) % keyNames.Length;
            if (kb.IsKeyPressed(Keys.Down) || p1.ButtonPressed(GamepadButton.DPadDown)) keyIndex = (keyIndex + 1) % keyNames.Length;

            // Toggle between Player 1 and Player 2 bindings using Left/Right
            if (kb.IsKeyPressed(Keys.Left) || kb.IsKeyPressed(Keys.Right) || p1.ButtonPressed(GamepadButton.DPadLeft) || p1.ButtonPressed(GamepadButton.DPadRight))
                editingPlayer2 = !editingPlayer2;

            if (kb.IsKeyPressed(Keys.Enter) || p1.ButtonPressed(GamepadButton.A)) isListening = true;
            if (kb.IsKeyPressed(Keys.Escape) || p1.ButtonPressed(GamepadButton.Back)) State = MenuState.Pause;
        }

        private void ApplyNewKey(Keys newKey)
        {
            // Access the correct struct based on editingPlayer2
            if (editingPlayer2)
                SetKeyValue(ref Program.Options.KeyBindingsPlayer2, keyIndex, newKey);
            else
                SetKeyValue(ref Program.Options.KeyBindingsPlayer1, keyIndex, newKey);
        }

        private void SetKeyValue(ref KeyBindings b, int index, Keys key)
        {
            switch (index)
            {
                case 0: b.Left = key; break;
                case 1: b.Right = key; break;
                case 2: b.Down = key; break;
                case 3: b.RotateCW = key; break;
                case 4: b.RotateCCW = key; break;
                case 5: b.Hold = key; break;
                case 6: b.HardDrop = key; break;
            }
        }
    }
}
