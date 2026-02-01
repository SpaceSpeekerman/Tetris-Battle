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

        readonly Vector3[] colors =
        {
            new(0,0,0), new(0,1,1), new(0,0,1), new(1,0.5f,0),
            new(1,1,0), new(0,1,0), new(0.6f,0,0.6f), new(1,0,0)
        };

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

            blocksTex = new Texture("Asset\\blocks_8.png");

            vao = GL.GenVertexArray();
            

            vbo = GL.GenBuffer();
            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            int stride = 7 * sizeof(float);

            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(0);

            // UV
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            // COLOR
            GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, stride, 4 * sizeof(float));
            GL.EnableVertexAttribArray(2);
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            var js0 = gamepad0.Update((float)e.Time);
            var js1 = gamepad1.Update((float)e.Time);


            var input0 = TetrisInput.FromDevices(KeyboardState, js0, false);
            var input1 = TetrisInput.FromDevices(KeyboardState, js1, true);


            var menuInput0 = MenuInputMapper.FromDevices(KeyboardState, js0);
            var menuInput1 = MenuInputMapper.FromDevices(KeyboardState, js1);

            // Let menu tick (countdown, menus). Menu no longer processes Start presses itself.
            menu.Update(e.Time, menuInput0, menuInput1, KeyboardState);


            // If menu is counting down, block gameplay updates until countdown finishes
            if (menu.State == MenuState.Countdown)
                return;
            // --- Handle START centrally (join / pause / resume / restart requests)
            if (input0.Start) menu.HandleStart(0);
            if (input1.Start) menu.HandleStart(1);

            // Player 0: update only when in Playing state
            if (menu.Player[0] == PlayerState.Playing)
            {
                game0.Update(e.Time, input0);
                if (game0.GameOver)
                    menu.Player[0] = PlayerState.GameOver;
            }


            // Player 1: update only when in Playing state
            if (menu.Player[1] == PlayerState.Playing)
            {
                game1.Update(e.Time, input1);
                if (game1.GameOver)
                    menu.Player[1] = PlayerState.GameOver;
            }

            bool vs =
                menu.Player[0] != PlayerState.Idle &&
                menu.Player[1] != PlayerState.Idle;
            bool bothPlaying =
                menu.Player[0] == PlayerState.Playing &&
                menu.Player[1] == PlayerState.Playing;
            if (Program.Options.GarbegeLines && bothPlaying)
            {
                int g0 = game0.ConsumeOutgoingGarbage();
                if (g0 > 0 && menu.Playing[1])
                    game1.QueueIncomingGarbage(g0);

                int g1 = game1.ConsumeOutgoingGarbage();
                if (g1 > 0 && menu.Playing[0])
                    game0.QueueIncomingGarbage(g1);
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
                int diff = game0.Score - game1.Score;
                game0.ScoreDelta = diff;
                game1.ScoreDelta = -diff;
            }
        }
        // Called when the menu requests a restart. The parameter indicates which player to restart
        // null = restart all non-idle players (used by the pause menu 'RESTART' item)
        void HandleRestart(int? player)
        {
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
                    game0.Restart();

                    // if needed, clear queued garbage or other per-game transient state here
                    // e.g. game0.ClearPendingGarbage();
                }

                if (menu.Player[1] != PlayerState.Idle)
                {
                    Console.WriteLine("[RESTART] Restarting player 1");
                    game1.Restart();

                    // e.g. game1.ClearPendingGarbage();
                }

                return;
            }

            // Restart a single player
            int p = player.Value;
            if (p == 0)
            {
                Console.WriteLine("[RESTART] Restarting player 0 (single)");
                game0.Restart();
                // optional: game0.ClearPendingGarbage();
            }
            else if (p == 1)
            {
                Console.WriteLine("[RESTART] Restarting player 1 (single)");
                game1.Restart();
                // optional: game1.ClearPendingGarbage();
            }
        }
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            GL.ClearColor(Color4.Black);
            GL.Clear(ClearBufferMask.ColorBufferBit);


            // Always render both players so the second board is visible even if not playing
            game0.Render(shader, blocksTex, text, vao, vbo);
            game1.Render(shader, blocksTex, text, vao, vbo);


            // Overlay "PRESS PLAY" on any board that is not currently playing
            if (menu.Player[1] == PlayerState.Idle)
            {
                text.Print("PRESS PLAY", game0.OffsetX + 4, 10, 1.0f, Vector4.One, Vector4.Zero);
            }
            if (menu.Player[1] == PlayerState.Idle)
            {
                text.Print("PRESS PLAY", game1.OffsetX + 4, 10, 1.0f, Vector4.One, Vector4.Zero);
            }


            // Draw menu (pause / countdown) on top
            menu.Render(text);

            text.Print(
                $"{menu.MatchWins[0]} - {menu.MatchWins[1]}",
                14, 1, 0.8f,
                Vector4.One,
                Vector4.Zero
            );

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

        int SimpleShader()
        {
            // color
            // string vs = "#version 330 core\r\nlayout (location = 0) in vec2 aPos;\r\nlayout (location = 1) in vec2 aUV;\r\nlayout (location = 2) in vec3 aColor;\r\n\r\nout vec2 vUV;\r\nout vec3 vColor;\r\n\r\nuniform mat4 uProj;\r\n\r\nvoid main()\r\n{\r\n    vUV = aUV;\r\n    vColor = aColor;\r\n    gl_Position = uProj * vec4(aPos, 0, 1);\r\n}\r\n";
            // string fs = "#version 330 core\r\nin vec2 vUV;\r\nin vec3 vColor;\r\n\r\nout vec4 FragColor;\r\n\r\nuniform sampler2D uTex;\r\n\r\nvoid main()\r\n{\r\n    vec4 tex = texture(uTex, vUV);\r\n    FragColor = tex * vec4(vColor, 1.0);\r\n}\r\n";
            // ignore color
            string vs = @"
#version 330 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec2 aUV;
layout (location = 2) in vec3 aColor;
out vec2 vUV;
out vec3 vColor;
uniform mat4 uProj;
void main(){
    vUV = aUV;
    vColor = aColor;
    gl_Position = uProj * vec4(aPos, 0, 1);
}";
            string fs = @"
#version 330 core
in vec2 vUV;
in vec3 vColor;
out vec4 FragColor;

uniform sampler2D uTex;
uniform vec4 uLevel; // Your tint color

void main()
{
    vec4 tex = texture(uTex, vUV);
    
    // 1. Determine how ""white"" the pixel is.
    float brightness = (tex.r + tex.g + tex.b) / 3.0;
    float whiteMask = smoothstep(0.6, 0.9, brightness);
    
    // 2. Standard multiplication for the dark/colored parts
    vec3 tintedBody = tex.rgb * uLevel.rgb;
    
    // 3. Slightly multiply the white area too (at ~20% intensity)
    // We mix white (1,1,1) with uLevel so the white isn't pure white anymore
    vec3 tintedWhite = tex.rgb * mix(vec3(1.0), uLevel.rgb, 0.2);
    
    // 4. Combine based on the mask
    vec3 combined = mix(tintedBody, tintedWhite, whiteMask);
    
    // 5. Global Brightness Boost (Gain)
    // Multiplying the final result by 1.2 makes everything 20% brighter
    FragColor = vec4(combined * 1.2, tex.a);
}
";
            int V = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(V, vs); GL.CompileShader(V);
            int F = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(F, fs); GL.CompileShader(F);
            int P = GL.CreateProgram();
            GL.AttachShader(P, V); GL.AttachShader(P, F); GL.LinkProgram(P);
            return P;
        }
    }
}
