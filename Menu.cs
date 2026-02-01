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
    public enum PlayerState
    {
        Idle,       // non ha mai premuto START
        Playing,    // partita in corso
        Paused,     // pausa
        GameOver    // morto, aspetta restart
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
        string[] pauseItems = { "RESUME", "RESTART", "OPTIONS", "CONTROLS", "QUIT" };
        int optionsIndex = 0;
        string[] optionsItems = { "Sound", "Ghost Piece", "Hold Piece", "NextPiece",
            "Hard Drop", "Garbage Lines", "First Player Wins", "Infinite Level" };

        public bool[] Playing = new bool[2] { false, false };
        public int ActivePlayer = -1; // -1 = none, 0 = P1, 1 = P2

        public void Update(double dt, MenuInput p1, MenuInput p2, KeyboardState kb)
        {
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
                UpdatePauseMenu(p1, p2);
            }
            else if (State == MenuState.Options)
            {
                UpdateOptionsMenu(p1, p2);
            }
            else if (State == MenuState.KeyConfig)
            {
                UpdateKeyConfig(kb, p1, p2);
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
                case MenuState.Options:
                    RenderOptionsMenu(text);
                    break;
                case MenuState.KeyConfig:
                    RenderKeyConfig(text);
                    break;
            }
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
                case 2: // OPTIONS
                    State = MenuState.Options;
                    break;

                case 3: // CONTROLS
                    State = MenuState.KeyConfig;
                    break;

                case 4: // QUIT
                    Environment.Exit(0);
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
        void UpdatePauseMenu(MenuInput p1, MenuInput p2)
        {
            // 1. Navigation Up
            if (p1.Up)
            {
                pauseIndex--;
                if (pauseIndex < 0) pauseIndex = pauseItems.Length - 1;
            }
            // 2. Navigation Down
            else if (p1.Down)
            {
                pauseIndex++;
                if (pauseIndex >= pauseItems.Length) pauseIndex = 0;
            }
            // 3. Confirm (A / Enter)
            else if (p1.Confirm)
            {
                ExecutePauseOption(); // Moved switch logic to a helper for clarity
            }

            // REMOVED: "else if (p1.Start) ..." 
            // We removed the Resume check here because HandleStart already does it!
        }
        public void ProcessStart(int player)
        {
            // se quel player non stava giocando → entra
            if (!Playing[player])
            {
                Playing[player] = true;
                ActivePlayer = player;
                State = MenuState.Countdown;
                countdown = CountdownDuration;
                Console.WriteLine($"[MENU] Player {player + 1} joined");
                return;
            }

            // se era attivo → pausa / resume
            if (ActivePlayer == player)
            {
                State = (State == MenuState.None) ? MenuState.Pause : MenuState.None;
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
        void UpdateOptionsMenu(MenuInput p1, MenuInput  p2)
        {
            // SU
            if ( p1.Up)
            {
                optionsIndex--;
                if (optionsIndex < 0)
                    optionsIndex = optionsItems.Length - 1;
            }
            // GIÙ
            else if ( p1.Down)
            {
                optionsIndex++;
                if (optionsIndex >= optionsItems.Length)
                    optionsIndex = 0;
            }
            // TOGGLE (A / ENTER)
            else if (p1.Confirm)
            {
                ToggleOption(optionsIndex);
                Console.WriteLine($"[OPTIONS] Toggle: {optionsItems[optionsIndex]}");
            }
            // BACK → PAUSE
            else if ( p1.Back)
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
                5 => Program.Options.GarbegeLines,
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
                case 5: Program.Options.GarbegeLines =      !Program.Options.GarbegeLines; break;
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
            if (p1.Up) keyIndex = (keyIndex - 1 + keyNames.Length) % keyNames.Length;
            if (p1.Down) keyIndex = (keyIndex + 1) % keyNames.Length;

            // Toggle between Player 1 and Player 2 bindings using Left/Right
            if (p1.Left || p1.Right)
                editingPlayer2 = !editingPlayer2;

            if (p1.Confirm) isListening = true;
            if (p1.Back) State = MenuState.Pause;
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
