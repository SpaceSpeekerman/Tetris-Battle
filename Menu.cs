using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tetris.OpenGL;
using Tetris.Audio;

namespace Tetris
{
    // ===== MENU / OPTIONS =====
    public enum MenuState { None, Pause, Options, Ready, KeyConfig, GamepadConfig, Countdown, NameEntry }
    public enum PlayerState
    {
        Idle,       // non ha mai premuto START
        Playing,    // partita in corso
        Paused,     // pausa
        GameOver    // morto, aspetta restart
    }
    public class HighScoreEntry
    {
        public string Name { get; set; }
        public int Score { get; set; }
    }
    public class MenuManager
    {
        public MenuState State = MenuState.Ready;
        public PlayerState[] Player =
        {
            PlayerState.Idle,
            PlayerState.Idle
        };
        public int[] MatchWins = new int[2] { 0, 0 };

        public bool GameplayEnabled => State == MenuState.None;
        public bool MultiplayerActive { get; private set; }

        bool p1Ready, p2Ready;
        public double CountdownDuration = 3.0;
        private double countdown = 0;

        public event Action<int?> OnRestartRequested;

        int pauseIndex;
        string[] pauseItems = { "RESUME", "RESTART", "SAVE HIGHSCORE", "RESET MATCH SCORES", "OPTIONS", "QUIT" };
        int optionsIndex = 0;
        string[] optionsItems = {
    "Sound", "Textured", "Ghost Piece", "Hold Piece", "NextPiece",
    "Hard Drop", "Garbage Lines", "First Player Wins", "Infinite Level",
    "CONFIGURE KEYBOARD", "CONFIGURE GAMEPAD" // New sub-menus
};

        public bool[] Playing = new bool[2] { false, false };
        public int ActivePlayer = -1; // -1 = none, 0 = P1, 1 = P2

        public void Update(double dt, MenuInput p1, MenuInput p2, KeyboardState kb, GamepadState gp1, GamepadState gp2)
        {
            MenuInput input = new MenuInput()
            {
                Up = p1.Up || p2.Up,
                Down = p1.Down || p2.Down,
                Left = p1.Left || p2.Left,
                Right = p1.Right || p2.Right,
                Confirm = p1.Confirm || p2.Confirm,
                Back = p1.Back || p2.Back,
                Start = p1.Start || p2.Start
            };

            // --- 1. Handle Countdown ---
            if (State == MenuState.Countdown)
            {
                countdown -= dt*2;
                if (countdown <= 0)
                {
                    State = MenuState.None; // Enable Gameplay
                    countdown = CountdownDuration;
                }
                return;
            }

            // --- 2. Menu Navigation (Only when Menu is open) ---
            // If Gameplay is active (State == None), we do nothing here.
            // Pause/Options logic only runs when State is NOT None.

            if (State == MenuState.Pause)
            {
                // Allow both players to navigate, or strictly the ActivePlayer
                UpdatePauseMenu(input);
            }
            else if (State == MenuState.Options)
            {
                UpdateOptionsMenu(input);
            }
            else if (State == MenuState.NameEntry)
            {
                UpdateNameEntry(input);
            }
            else if (State == MenuState.KeyConfig)
            {
                UpdateKeyConfig(kb, p1, p2);
            }
            else if (State == MenuState.GamepadConfig)
            {
                // You will need to pass your gamepad state here
                UpdateGamepadConfig(gp1, gp2, p1, p2);
            }
            // Sync bool helpers for external access
            for (int i = 0; i < 2; i++)
                Playing[i] = (Player[i] == PlayerState.Playing);
        }
        public void Render(TextRenderer text)
        {
            switch (State)
            {
                //case MenuState.Ready:
                //    text.Print("PRESS START", 12, 14, 0.75f, Vector4.One, Vector4.Zero);
                //    break;
                case MenuState.Countdown:
                    text.Print(((int)Math.Ceiling(countdown)).ToString(), 20, 14, 1.2f, Vector4.One, Vector4.Zero);
                    break;

                case MenuState.Pause:
                    for (int i = 0; i < pauseItems.Length; i++)
                        text.Print(pauseItems[i], 14, 5 + i * 2, 0.75f,
                            i == pauseIndex ? new Vector4(1, 1, 0, 1) : Vector4.One,
                            Vector4.Zero);
                    break;
                case MenuState.NameEntry:
                    RenderNameEntry(text);
                    break;
                case MenuState.Options:
                    RenderOptionsMenu(text);
                    break;
                case MenuState.KeyConfig:
                    RenderKeyConfig(text);
                    break;
                case MenuState.GamepadConfig:
                    RenderGamepadConfig(text);
                    break;
            }
        }
        void StartCountdown()
        {
            State = MenuState.Countdown;
            countdown = CountdownDuration;

            // Sincronizza subito la UI
            for (int i = 0; i < 2; i++)
                Playing[i] = (Player[i] == PlayerState.Playing);
        }
        public void HandleStart(int i)
        {
            // Identify current player (me) and opponent (other)
            int otherIndex = (i + 1) % 2;
            PlayerState me = Player[i];
            PlayerState other = Player[otherIndex];

            // JOIN DURING COUNTDOWN
            if (State == MenuState.Countdown && Player[i] == PlayerState.Idle)
            {
                Console.WriteLine($"[MENU] Player {i} joined during countdown");

                Player[i] = PlayerState.Playing;

                // Restart countdown for fairness
                StartCountdown();

                return;
            }
            // 1. RESUME (Global): If in Pause menu, any Start press resumes the game
            if (State == MenuState.Pause)
            {
                Console.WriteLine($"[MENU] Resuming Game via Player {i} Start");
                State = MenuState.None;

                // Unpause both players to resume update loops
                if (Player[0] == PlayerState.Paused) Player[0] = PlayerState.Playing;
                if (Player[1] == PlayerState.Paused) Player[1] = PlayerState.Playing;
                return;
            }

            // 2. JOIN / START (From Idle): Player joins the game
            if (me == PlayerState.Idle)
            {
                Console.WriteLine($"[MENU] Player {i} Joined");
                Player[i] = PlayerState.Playing;
                ActivePlayer = i; // This player takes control of navigation if needed

                // Req 5: If the other player is already playing, this is a VS Join.
                // We must restart the match entirely to ensure a fair sync.
                if (other == PlayerState.Playing)
                {
                    Console.WriteLine("[MENU] VS Join detected. Restarting match...");
                    OnRestartRequested?.Invoke(null); // null = Restart ALL active players
                }

                StartCountdown();
                return;
            }

            // 3. PAUSE (From Playing): Toggles pause for EVERYONE
            if (me == PlayerState.Playing)
            {
                Console.WriteLine($"[MENU] Global Pause by Player {i}");
                State = MenuState.Pause;
                ActivePlayer = i; // The one who paused navigates the menu

                // Freeze both players
                if (Player[0] == PlayerState.Playing) Player[0] = PlayerState.Paused;
                if (Player[1] == PlayerState.Playing) Player[1] = PlayerState.Paused;
                return;
            }

            // 4. RESTART (From GameOver): Logic depends on opponent
            if (me == PlayerState.GameOver)
            {
                // If opponent is still Playing, we must wait (Ignore input)
                if (other == PlayerState.Playing)
                    return;

                // If opponent is NOT playing (Idle or GameOver), we restart.
                Console.WriteLine($"[MENU] Player {i} requested rematch");

                // Reset my state (and opponent's if they were GameOver) to Playing
                Player[i] = PlayerState.Playing;
                if (other == PlayerState.GameOver) Player[otherIndex] = PlayerState.Playing;

                OnRestartRequested?.Invoke(null); // Reset grids
                StartCountdown();
            }
        }
        void ExecutePauseOption()
        {
            switch (pauseIndex)
            {
                case 0: // RESUME
                        // Simulate a Start press behavior to resume
                    HandleStart(ActivePlayer);
                    break;
                case 1: // RESTART
                        // Riporta tutti i player attivi in Playing
                    for (int p = 0; p < 2; p++)
                    {
                        if (Player[p] != PlayerState.Idle)
                            Player[p] = PlayerState.Playing;
                    }

                    OnRestartRequested?.Invoke(null);
                    StartCountdown();
                    break;
                case 2: // SAVE HIGHSCORE
                        // Check which player had the higher score
                    int p1Score = Program.GetScore(0);
                    int p2Score = Program.GetScore(1);
                    OpenNameEntry(Math.Max(p1Score, p2Score));
                    break;
                case 3: // RESET SCORES
                    MatchWins = [0, 0];
                    break;
                case 4: // OPTIONS
                    State = MenuState.Options;
                    break;

                case 5: // QUIT
                    Environment.Exit(0);
                    break;
            }
        }
        void UpdatePauseMenu(MenuInput i)
        {
            // 1. Navigation Up
            if (i.Up)
            {
                pauseIndex--;
                if (pauseIndex < 0) pauseIndex = pauseItems.Length - 1;
            }
            // 2. Navigation Down
            else if (i.Down)
            {
                pauseIndex++;
                if (pauseIndex >= pauseItems.Length) pauseIndex = 0;
            }
            // 3. Confirm (A / Enter)
            else if (i.Confirm)
            {
                ExecutePauseOption(); // Moved switch logic to a helper for clarity
            }
            // 4. Back Options / B
            else if (i.Back)
            {
                State = MenuState.None;

                // Unpause both players to resume update loops
                if (Player[0] == PlayerState.Paused) Player[0] = PlayerState.Playing;
                if (Player[1] == PlayerState.Paused) Player[1] = PlayerState.Playing;
            }
            // REMOVED: "else if (p1.Start) ..." 
            // We removed the Resume check here because HandleStart already does it!
        }
        // HighScore
        private string currentName = "AAA";
        private int charIndex = 0; // 0, 1, or 2
        private string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789! ";
        private int alphabetIndex = 0;
        private int scoreToSave = 0;

        public void OpenNameEntry(int score)
        {
            scoreToSave = score;
            currentName = "AAAAA";
            charIndex = 0;
            State = MenuState.NameEntry;
        }
        private List<HighScoreEntry> LoadHighScores()
        {
            string path = "highscore.txt";
            if (!File.Exists(path)) return new List<HighScoreEntry>();

            return File.ReadAllLines(path)
                .Select(line => line.Split(','))
                .Select(parts => new HighScoreEntry { Name = parts[0], Score = int.Parse(parts[1]) })
                .OrderByDescending(s => s.Score)
                .ToList();
        }
        void UpdateNameEntry(MenuInput input)
        {
            // 1. Change Character (Up/Down)
            if (input.Up)
            {
                alphabetIndex = (alphabetIndex - 1 + alphabet.Length) % alphabet.Length;
                UpdateNameChar();
            }
            if (input.Down)
            {
                alphabetIndex = (alphabetIndex + 1) % alphabet.Length;
                UpdateNameChar();
            }

            // 2. Move Cursor (Left/Right)
            if (input.Left) charIndex = Math.Max(0, charIndex - 1);
            if (input.Right) charIndex = Math.Min(2, charIndex + 1);

            // 3. Confirm (Confirm button)
            if (input.Confirm)
            {
                SaveNewScore(currentName, scoreToSave);
                State = MenuState.Pause; // Return to pause or a "High Score Board" state
            }
            if (input.Back)
            {
                State = MenuState.Pause; // Return to pause
            }
        }

        void UpdateNameChar()
        {
            char[] nameArr = currentName.ToCharArray();
            nameArr[charIndex] = alphabet[alphabetIndex];
            currentName = new string(nameArr);
        }

        void RenderNameEntry(TextRenderer text)
        {
            text.Print("NEW HIGH SCORE!", 10, 5, 1.2f, new Vector4(1, 1, 0, 1), Vector4.Zero);
            text.Print(scoreToSave.ToString(), 15, 7, 1.0f, Vector4.One, Vector4.Zero);

            // Render the name with a highlight on the active char
            for (int i = 0; i < 5; i++)
            {
                Vector4 col = (i == charIndex) ? new Vector4(0, 1, 1, 1) : Vector4.One;
                text.Print(currentName[i].ToString(), 14 + i * 2, 10, 2.0f, col, Vector4.Zero);

                //// Draw an underline for the active char
                //if (i == charIndex) text.Print("_", 14 + i * 2, 10.5f, 1.0f, col, Vector4.Zero);
            }

            text.Print("UP/DOWN: SELECT  LEFT/RIGHT: POSITION  ENTER: SAVE", 6, 15, 0.6f, Vector4.One, Vector4.Zero);
        }
        private void SaveNewScore(string name, int score)
        {
            var scores = LoadHighScores();
            scores.Add(new HighScoreEntry { Name = name, Score = score });

            // Sort, take top 10, and format for text file
            var top10 = scores.OrderByDescending(s => s.Score).Take(10);
            File.WriteAllLines("highscore.txt", top10.Select(s => $"{s.Name},{s.Score}"));
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
                    (i > 8 ? " " :$"[{(value ? "*" : " ")}]") +
                    $"{optionsItems[i]}";

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
        void UpdateOptionsMenu(MenuInput i)
        {
            if (i.Up)
            {
                optionsIndex = (optionsIndex - 1 + optionsItems.Length) % optionsItems.Length;
            }
            else if (i.Down)
            {
                optionsIndex = (optionsIndex + 1) % optionsItems.Length;
            }
            else if (i.Confirm)
            {
                if (optionsIndex < 9)
                {
                    ToggleOption(optionsIndex);
                }
                else if (optionsIndex == 9)
                {
                    State = MenuState.KeyConfig; // Sub-menu Keyboard
                }
                else if (optionsIndex == 10)
                {
                    State = MenuState.GamepadConfig; // Sub-menu Gamepad
                }
            }
            else if (i.Back)
            {
                State = MenuState.Pause;
            }
        }
        bool GetOptionValue(int index)
        {
            return index switch
            {
                0 => Program.Options.SoundOn,
                1 => Program.Options.Textured,
                2 => Program.Options.GhostPiece,
                3 => Program.Options.HoldPiece,
                4 => Program.Options.NextPiece,
                5 => Program.Options.HardDrop,
                6 => Program.Options.GarbageLines,
                7 => Program.Options.FirstPlayerWins,
                8 => Program.Options.InfiniteLevel,
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
                case 1: Program.Options.Textured =        !Program.Options.GhostPiece; break;
                case 2: Program.Options.GhostPiece =        !Program.Options.GhostPiece; break;
                case 3: Program.Options.HoldPiece =         !Program.Options.HoldPiece; break;
                case 4: Program.Options.NextPiece =         !Program.Options.NextPiece; break;
                case 5: Program.Options.HardDrop =          !Program.Options.HardDrop; break;
                case 6: Program.Options.GarbageLines =      !Program.Options.GarbageLines; break;
                case 7: Program.Options.FirstPlayerWins =   !Program.Options.FirstPlayerWins; break;
                case 8: Program.Options.InfiniteLevel =     !Program.Options.InfiniteLevel; break;
            }
        }
        // Inside Menu Class
        private int keyIndex = 0;
        private bool isListening = false;
        private bool editingPlayer2 = false; // Which player are we rebinding?

        private string[] keyNames = {
            "Left", "Right", "Down", "Rotate CW", "Rotate CCW", "Hold", "Hard Drop"
        };
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
        public void UpdateKeyConfig(KeyboardState kb, MenuInput p1, MenuInput p2)
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
            if (p1.Up || p2.Up) keyIndex = (keyIndex - 1 + keyNames.Length) % keyNames.Length;
            if (p1.Down || p2.Down) keyIndex = (keyIndex + 1) % keyNames.Length;

            // Toggle between Player 1 and Player 2 bindings using Left/Right
            if (p1.Left || p1.Right || p2.Left ||p2.Right)
                editingPlayer2 = !editingPlayer2;

            if (p1.Confirm || p2.Confirm) isListening = true;
            if (p1.Back || p2.Back) State = MenuState.Options;
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

        public void UpdateGamepadConfig(GamepadState pad1, GamepadState pad2, MenuInput p1, MenuInput p2)
        {
            if (isListening)
            {
                // Select which pad to listen to based on who we are editing
                GamepadState activePad = editingPlayer2 ? pad2 : pad1;

                // Check all buttons on the selected controller
                foreach (GamepadButton b in Enum.GetValues(typeof(GamepadButton)))
                {
                    // Note: Using ButtonPressed (ensure this is your "JustPressed" logic)
                    if (activePad.ButtonPressed(b))
                    {
                        ApplyNewGamepadButton(b); // This uses editingPlayer2 internally
                        isListening = false;
                        break;
                    }
                }
                return;
            }

            // --- Standard Navigation ---
            // Use MenuInput which already combines P1 and P2 inputs for navigation
            if (p1.Up || p2.Up) keyIndex = (keyIndex - 1 + keyNames.Length) % keyNames.Length;
            if (p1.Down || p2.Down) keyIndex = (keyIndex + 1) % keyNames.Length;

            // Switch between editing P1 and P2
            if (p1.Left || p1.Right || p2.Left || p2.Right)
                editingPlayer2 = !editingPlayer2;

            if (p1.Confirm || p2.Confirm) isListening = true;

            // Return to Options sub-menu
            if (p1.Back || p2.Back) State = MenuState.Options;
        }
        void RenderGamepadConfig(TextRenderer text)
        {
            text.Print("CONFIGURE GAMEPAD", 6, 2, 1.2f, Vector4.One, Vector4.Zero);

            // Highlight the active player being edited
            string playerHeader = editingPlayer2 ? "  PLAYER 1   > PLAYER 2 <" : "> PLAYER 1 <   PLAYER 2  ";
            text.Print(playerHeader, 8, 4, 0.9f, new Vector4(0, 1, 1, 1), Vector4.Zero);

            if (isListening)
            {
                text.Print($"PRESS BUTTON FOR {keyNames[keyIndex].ToUpper()}...", 10, 15, 0.7f, new Vector4(1, 0, 0, 1), Vector4.Zero);
            }

            var b = editingPlayer2 ? Program.Options.GamepadBindingsPlayer2 : Program.Options.GamepadBindingsPlayer1;

            // Helper to get string representation of the first button in the array
            string GetBtn(GamepadButton[] arr) => arr.Length > 0 ? arr[0].ToString() : "NONE";

            string[] currentButtons = {
        GetBtn(b.Left), GetBtn(b.Right), GetBtn(b.Down),
        GetBtn(b.RotateCW), GetBtn(b.RotateCCW), GetBtn(b.Hold), GetBtn(b.HardDrop)
    };

            for (int i = 0; i < keyNames.Length; i++)
            {
                string line = $"{(i == keyIndex ? "> " : "  ")}{keyNames[i]}: {currentButtons[i]}";
                Vector4 color = (i == keyIndex) ? (isListening ? new Vector4(1, 0, 0, 1) : new Vector4(1, 1, 0, 1)) : Vector4.One;
                text.Print(line, 10, 6 + i * 1.5f, 0.8f, color, Vector4.Zero);
            }
        }
        private void ApplyNewGamepadButton(GamepadButton newButton)
        {
            // Routing to the correct Global options object
            var bindings = editingPlayer2 ? Program.Options.GamepadBindingsPlayer2 : Program.Options.GamepadBindingsPlayer1;
            SetGamepadValue(bindings, keyIndex, newButton);

            Console.WriteLine($"[MENU] Player {(editingPlayer2 ? 2 : 1)} bound {keyNames[keyIndex]} to {newButton}");
        }

        private void SetGamepadValue(GamepadBindings b, int index, GamepadButton button)
        {
            // Since your class uses arrays, we wrap the button in a new array
            var btnArray = new GamepadButton[] { button };

            switch (index)
            {
                case 0: b.Left = btnArray; break;
                case 1: b.Right = btnArray; break;
                case 2: b.Down = btnArray; break;
                case 3: b.RotateCW = btnArray; break;
                case 4: b.RotateCCW = btnArray; break;
                case 5: b.Hold = btnArray; break;
                case 6: b.HardDrop = btnArray; break;
            }
        }
    }
}
