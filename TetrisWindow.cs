using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;
using Tetris;
using Tetris.OpenGL;

namespace Tetris
{
    
    public class TetrisWindow : GameWindow
    {
        MenuManager menu;
        TetrisGame game0 = new TetrisGame();
        TetrisGame game1 = new TetrisGame()
        {
            OffsetX = 24
        };
        Gamepad gamepad0 = new Gamepad(0) { AllowRepeat = false };
        Gamepad gamepad1 = new Gamepad(1) { AllowRepeat = false };

        int vao, vbo, shader;

        TextRenderer text;
        Font font;
        Texture blocksTex;

        Random rand = new Random();
        public TetrisWindow(GameWindowSettings g, NativeWindowSettings n) : base(g, n) { }

        protected override void OnLoad()
        {
            GL.ClearColor(0, 0, 0, 1);

            menu = new MenuManager();
            menu.OnRestartRequested += HandleRestart;

            font = new Font("Asset\\font_8x8.png", 8, 8, 8, 32); // esempio ASCII
            text = new TextRenderer(font);
            GL.Enable(EnableCap.Texture2D);

            // passa la risoluzione reale della finestra
            text.screenResolution(Size.X, Size.Y);

            shader = SimpleShader();
            ShaderWatcherInit();

            blocksTex = new Texture("Asset\\blocks_8.png");

            vao = GL.GenVertexArray();
            
            vbo = GL.GenBuffer();
            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            int stride = 5 * sizeof(float); // x, y, u, v, type
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0); // Pos
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float)); // UV
            GL.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, stride, 4 * sizeof(float)); // Type
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.EnableVertexAttribArray(2);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            UpdateShaderRealTime();

            var js0 = gamepad0.Update((float)e.Time);
            var js1 = gamepad1.Update((float)e.Time);

            var input0 = TetrisInput.FromDevices(KeyboardState, js0, false);
            var input1 = TetrisInput.FromDevices(KeyboardState, js1, true);

            var menuInput0 = MenuInputMapper.FromDevices(KeyboardState, js0);
            var menuInput1 = MenuInputMapper.FromDevices(KeyboardState, js1);

            // Let menu tick (countdown, menus). Menu no longer processes Start presses itself.
            menu.Update(e.Time, menuInput0, menuInput1, KeyboardState);

            // --- Handle START centrally (join / pause / resume / restart requests)
            if (input0.Start) menu.HandleStart(0);
            if (input1.Start) menu.HandleStart(1);

            // If menu is counting down, block gameplay updates until countdown finishes
            if (menu.State == MenuState.Countdown)
                return;


            // Player 0: update only when in Playing state
            if (menu.Player[0] == PlayerState.Playing)
            {
                game0.Update((float)e.Time, input0);
                if (game0.GameOver)
                    menu.Player[0] = PlayerState.GameOver;
            }


            // Player 1: update only when in Playing state
            if (menu.Player[1] == PlayerState.Playing)
            {
                game1.Update((float)e.Time, input1);
                if (game1.GameOver)
                    menu.Player[1] = PlayerState.GameOver;
            }


            bool vs =
                menu.Player[0] != PlayerState.Idle &&
                menu.Player[1] != PlayerState.Idle;
            bool bothPlaying =
                menu.Player[0] == PlayerState.Playing &&
                menu.Player[1] == PlayerState.Playing;
            if (Program.Options.GarbageLines && bothPlaying)
            {
                int g0 = game0.ConsumeOutgoingGarbage();
                if (g0 > 0 && menu.Playing[1])
                    game1.QueueIncomingGarbage(g0);

                int g1 = game1.ConsumeOutgoingGarbage();
                if (g1 > 0 && menu.Playing[0])
                    game0.QueueIncomingGarbage(g1);

                int diff = game0.Score - game1.Score;
                game0.ScoreDelta = diff;
                game1.ScoreDelta = -diff;

            }
            else if (vs)
            {
                if (Program.Options.FirstPlayerWins && !game0.IsMatchResolved &&
                     (menu.Player[0] == PlayerState.GameOver || menu.Player[1] == PlayerState.GameOver)
                     )
                {
                    bool p0Lost = menu.Player[0] == PlayerState.GameOver;
                    bool p1Lost = menu.Player[1] == PlayerState.GameOver;

                    if (!p0Lost) menu.MatchWins[0]++;
                    if (!p1Lost) menu.MatchWins[1]++;
                    // Set outcome visivo
                    game0.SetGameOver(!p0Lost);
                    game1.SetGameOver(!p1Lost);

                    menu.Player[0] = PlayerState.GameOver;
                    menu.Player[1] = PlayerState.GameOver;

                    game0.IsMatchResolved = true;
                    game1.IsMatchResolved = true;

                    Console.WriteLine("[VS] Match ended by first death");
                    Console.WriteLine($"[MATCH] SCORE {menu.MatchWins[0]} - {menu.MatchWins[1]}");
                }
                else if(
                    !Program.Options.FirstPlayerWins &&
                    menu.Player[0] == PlayerState.GameOver &&
                    menu.Player[1] == PlayerState.GameOver
                    )
                {
                    // Match already resolved?
                    if (!game0.IsMatchResolved)   // flag semplice, vedi sotto
                    {
                        if (game0.Score > game1.Score)
                        {
                            game0.SetGameOver(true);
                            game1.SetGameOver(false);
                            menu.MatchWins[0]++;
                        }
                        else if (game1.Score > game0.Score)
                        {
                            game0.SetGameOver(false);
                            game1.SetGameOver(true);
                            menu.MatchWins[1]++;
                        }
                        else
                        {
                            // pareggio → entrambi perdono o nessun winner
                            game0.SetGameOver(true);
                            game1.SetGameOver(true);
                        }

                        game0.IsMatchResolved = true;
                        game1.IsMatchResolved = true;

                        Console.WriteLine("[VS] Match ended by score");
                    }
                }
            }
        }
        // Called when the menu requests a restart. The parameter indicates which player to restart
        // null = restart all non-idle players (used by the pause menu 'RESTART' item)
        void HandleRestart(int? player)
        {
            int new_seed = rand.Next();
            if (menu.State == MenuState.Countdown)
                return;
            // Called when MenuManager invokes OnRestartRequested.
            // `player == null` -> restart all non-idle players
            // `player == 0` or `1` -> restart only that player
            // Restart all non-idle players
            if (player == null)
            {
                if (menu.Player[0] != PlayerState.Idle)
                {
                    Console.WriteLine("[RESTART] Restarting player 0");
                    game0.Restart(new_seed);

                    // if needed, clear queued garbage or other per-game transient state here
                    // e.g. game0.ClearPendingGarbage();
                }

                if (menu.Player[1] != PlayerState.Idle)
                {
                    Console.WriteLine("[RESTART] Restarting player 1");
                    game1.Restart(new_seed);

                    // e.g. game1.ClearPendingGarbage();
                }
                return;
            }

            // Restart a single player
            int p = player.Value;
            if (p == 0)
            {
                Console.WriteLine("[RESTART] Restarting player 0 (single)");
                game0.Restart(new_seed);
                // optional: game0.ClearPendingGarbage();
            }
            else if (p == 1)
            {
                Console.WriteLine("[RESTART] Restarting player 1 (single)");
                game1.Restart(new_seed);
                // optional: game1.ClearPendingGarbage();
            }
        }
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            GL.ClearColor(Color4.Black);
            GL.Clear(ClearBufferMask.ColorBufferBit);


            // Always render both players so the second board is visible even if not playing
            if(menu.State == MenuState.None || menu.State == MenuState.Countdown || menu.State == MenuState.Ready)
            {
                game0.Render(shader, blocksTex, text, vao, vbo);
                game1.Render(shader, blocksTex, text, vao, vbo);
                text.Print(
                    $"{menu.MatchWins[0]} - {menu.MatchWins[1]}",
                    18, 3, 0.8f,
                    Vector4.One,
                    Vector4.Zero
                );
            }

            // Overlay "PRESS PLAY" on any board that is not currently playing
            if (menu.Player[0] == PlayerState.Idle)
            {
                text.Print("PRESS PLAY", game0.OffsetX + 4, 10, 1.0f, Vector4.One, Vector4.Zero);
            }
            if (menu.Player[1] == PlayerState.Idle)
            {
                text.Print("PRESS PLAY", game1.OffsetX + 4, 10, 1.0f, Vector4.One, Vector4.Zero);
            }


            // Draw menu (pause / countdown) on top
            menu.Render(text);

            SwapBuffers();
        }
        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            float targetAspect = 16.0f / 9.0f;
            int windowWidth = Size.X;
            int windowHeight = Size.Y;

            int viewWidth = windowWidth;
            int viewHeight = (int)(windowWidth / targetAspect + 0.5f);

            if (viewHeight > windowHeight)
            {
                // Window is too "tall", so we limit height and pillarbox the sides
                viewHeight = windowHeight;
                viewWidth = (int)(windowHeight * targetAspect + 0.5f);
            }

            // Center the viewport in the window
            int xOffset = (windowWidth - viewWidth) / 2;
            int yOffset = (windowHeight - viewHeight) / 2;

            GL.Viewport(xOffset, yOffset, viewWidth, viewHeight);

            // Keep your text rendering informed of the 'virtual' space if needed
            text.screenResolution(viewWidth, viewHeight);
        }

        private FileSystemWatcher shaderWatcher;
        private bool shadersDirty = false;
        void ShaderWatcherInit()
        {
            // Set up the watcher
            shaderWatcher = new FileSystemWatcher(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Asset"));
            shaderWatcher.Filter = "game_shader.*"; // Watch both .vertex and .fragment
            shaderWatcher.NotifyFilter = NotifyFilters.LastWrite;
            shaderWatcher.Changed += (s, e) => shadersDirty = true;
            shaderWatcher.EnableRaisingEvents = true;
        }
        void UpdateShaderRealTime()
        {
            if (shadersDirty)
            {
                try
                {
                    int newShader = SimpleShader();
                    // Optional: Delete the old shader program to prevent memory leaks
                    GL.DeleteProgram(shader);
                    shader = newShader;
                    Console.WriteLine("[SHADER] Reloaded successfully.");
                    shadersDirty = false;
                }
                catch (Exception ex)
                {
                    // If there's a syntax error, we print it and wait for the user to fix it
                    Console.WriteLine($"[SHADER] Reload failed: {ex.Message}");
                    shadersDirty = false;
                }
            }
        }
        int SimpleShader()
        {
            // Load source from files
            string vs = File.ReadAllText("Asset\\game_shader.vertex");
            string fs = File.ReadAllText("Asset\\game_shader.fragment");

            int V = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(V, vs);
            GL.CompileShader(V);
            CheckShaderErrors(V, "VERTEX");

            int F = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(F, fs);
            GL.CompileShader(F);
            CheckShaderErrors(F, "FRAGMENT");

            int P = GL.CreateProgram();
            GL.AttachShader(P, V);
            GL.AttachShader(P, F);
            GL.LinkProgram(P);

            // Check for linking errors (mismatched in/out variables)
            GL.GetProgram(P, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetProgramInfoLog(P);
                Console.WriteLine($"ERROR::SHADER::PROGRAM::LINKING_FAILED\n{infoLog}");
            }

            // Cleanup individual shaders once linked
            GL.DeleteShader(V);
            GL.DeleteShader(F);

            return P;
        }

        void CheckShaderErrors(int shader, string type)
        {
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(shader);
                Console.WriteLine($"ERROR::SHADER::{type}::COMPILATION_FAILED\n{infoLog}");
            }
        }
    }
}
