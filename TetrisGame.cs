using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;
using Tetris.Audio;
using Tetris.OpenGL;
using static System.Net.Mime.MediaTypeNames;

namespace Tetris
{
    public class TetrisGame
    {
        public int Width = 10;
        public int Height = 20;

        public int[,] Grid;
        public bool GameOver { get; private set; }
        public bool IsMatchResolved = false;
        bool isWinner;

        public int Current;
        public int Next;
        public int Hold = -1;
        bool holdUsed;

        public Vector2i Pos;
        public int OffsetX;
        public Vector2i[] Blocks;

        double fallTimer;
        double moveTimer;

        const double MOVE_REPEAT = 0.08;

        public int Score;
        public int CurrentLevel;
        public int ScoreDelta;
        public int Lines;
        public int TetrisCount;
        public int LinesPerLevelIncrease = 10;
        int dropDistance;

        private int totalClearEvents;

        public bool DidTetrisThisTurn;


        Queue<int> outgoingGarbage = new();
        Queue<int> incomingGarbage = new();


        Random rng = new Random();

        readonly Vector3[] colors =
        {
            new(0,0,0), new(0,1,1), new(0.3f,0.3f,1), new(1,0.5f,0),
            new(1,1,0), new(0,1,0), new(0.6f,0,0.6f), new(1,0,0), new(0.5f,0.5f,0.5f) // 8: GREY (Garbage) <--- Add this!
        };

        public static readonly Vector2i[][] Shapes =
        {
            new[]{ new Vector2i(0,1),new Vector2i(1,1),new Vector2i(2,1),new Vector2i(3,1)}, // I
            new[]{ new Vector2i(0,0),new Vector2i(0,1),new Vector2i(1,1),new Vector2i(2,1)}, // J
            new[]{ new Vector2i(0,0),new Vector2i(1,0),new Vector2i(1,1),new Vector2i(2,1)}, // Z
            new[]{ new Vector2i(1,0),new Vector2i(2,0),new Vector2i(1,1),new Vector2i(2,1)}, // O
            new[]{ new Vector2i(1,0),new Vector2i(2,0),new Vector2i(0,1),new Vector2i(1,1)}, // S
            new[]{ new Vector2i(2,0),new Vector2i(0,1),new Vector2i(1,1),new Vector2i(2,1)}, // L
            new[]{ new Vector2i(1,0),new Vector2i(0,1),new Vector2i(1,1),new Vector2i(2,1)}, // T
        };

        public TetrisGame()
        {
            Next = rng.Next(7);
            Grid = new int[Width, Height];
            Spawn();
        }

        public void Update(double dt, TetrisInput i)
        {
            if (GameOver) return;

            // LEFT / RIGHT hold movement
            moveTimer += dt;
            if (moveTimer >= MOVE_REPEAT)
            {
                if (i.Left) TryMove(new Vector2i(-1, 0));
                if (i.Right) TryMove(new Vector2i(1, 0));
                moveTimer = 0;
            }

            if (i.RotateCCW) TryRotate();
            if (i.RotateCW) TryRotate(false);
            if (i.Hold && Program.Options.HoldPiece) HoldPiece();

            if (i.HardDrop && Program.Options.HardDrop)
            {
                //AduioLibrary.dashSound.Play();
                int dist = 0;
                while (!Collide(Pos + Vector2i.UnitY * -1, Blocks))
                {
                    Pos.Y--;
                    dist++;
                }
                dropDistance += dist; // hard drop bonus
                Lock();
            }

            CurrentLevel = Lines / LinesPerLevelIncrease;
            float level_increment = 0.05f;

            // SOFT DROP (down held)
            float interval = i.Down ? 0.05f : 0.6f;
            interval = Program.Options.InfiniteLevel ? interval : interval - (level_increment * CurrentLevel);

            fallTimer += dt;
            if (fallTimer >= interval)
            {
                fallTimer = 0;
                if (TryMove(new Vector2i(0, -1)))
                {
                    if (i.Down) dropDistance++;   // soft drop bonus
                }
                else Lock();
            }

        }
        public void Render(int shader, Texture texture, TextRenderer text, int vao, int vbo)
        {
            var verts = new List<float>();
            void Cell(float x, float y, Vector3 c, int shapeIndex)
            {
                float x0 = x;
                float y0 = y;
                float x1 = x + 1;
                float y1 = y + 1;

                // Assuming 5 sprites in a horizontal strip (3 shapes + 1 ghost + 1 border)
                float atlasCount = 5;
                float u0 = shapeIndex / atlasCount;
                float u1 = (shapeIndex + 1) / atlasCount;
                float v0 = 0.0f;
                float v1 = 1.0f;

                // TRI 1
                Push(x0, y0, u0, v0, c);
                Push(x1, y0, u1, v0, c);
                Push(x1, y1, u1, v1, c);

                // TRI 2
                Push(x0, y0, u0, v0, c);
                Push(x1, y1, u1, v1, c);
                Push(x0, y1, u0, v1, c);
            }
            void Push(float x, float y, float u, float v, Vector3 c)
            {
                verts.AddRange(new float[] {
                    x, y,
                    u, v,
                    c.X, c.Y, c.Z
                });
            }


            // GHOST PIECE
            if(Program.Options.GhostPiece)
            {
                int ghostY = GetDropY();
                Vector3 ghostColor = colors[Current + 1] * 0.3f;

                foreach (var b in Blocks)
                {
                    // Use shapeIndex 3 for a specific "ghost" texture, or same as Current
                    Cell(Pos.X + b.X + OffsetX, ghostY + b.Y, ghostColor, 3);
                }
            }

            // STACK
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (Grid[x, y] != 0)
                        Cell(x + OffsetX, y, colors[Grid[x, y]], (Grid[x, y] - 1) % 3);

            // CURRENT
            foreach (var b in Blocks)
                Cell(Pos.X + b.X+ OffsetX, Pos.Y + b.Y, colors[Current + 1], Current % 3);

            // BORDI CAMPO
            for (int y = 0; y < Height; y++)
            {
                Cell(-1 + OffsetX, y,
                    Vector3.One, 4);
                Cell(Width+ OffsetX, y, Vector3.One, 4);
            }
            for (int x = -1; x <= Width; x++)
                Cell(x + OffsetX, -1,
                    Vector3.One,
                    4);
            // NEXT PIECE
            if(Program.Options.NextPiece)
            {
                int nextX = 13 + OffsetX;
                int nextY = 14;

                    foreach (var b in TetrisGame.Shapes[Next])
                    {
                        Cell(nextX + b.X, nextY + b.Y,
                             colors[Next + 1],
                             Next % 3);
                    }
            }

            // HOLD PIECE
            if (Hold != -1 && Program.Options.HoldPiece)
            {
                int holdX = 13 + OffsetX;
                int holdY = 8;

                foreach (var b in TetrisGame.Shapes[Hold])
                {
                    Cell(holdX + b.X, holdY + b.Y,
                         colors[Hold + 1],
                         Hold % 3);
                }
            }

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, verts.Count * sizeof(float), verts.ToArray(), BufferUsageHint.DynamicDraw);

            GL.UseProgram(shader);
            texture.Bind();
            GL.Uniform1(GL.GetUniformLocation(shader, "uTex"), 0);

            var c = colors[(CurrentLevel % (colors.Length-1))+1];
            GL.Uniform4(GL.GetUniformLocation(shader, "uLevel"), c.X, c.Y, c.Z,1f);

            var proj = Matrix4.CreateOrthographicOffCenter(-2, 46, -2, 25, -1, 1);

            GL.UniformMatrix4(GL.GetUniformLocation(shader, "uProj"), false, ref proj);

            GL.BindVertexArray(vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, verts.Count / 5);

            PrintUI(text);
        }
        void PrintUI(TextRenderer text)
        {
            // ===== UI TEXT =====
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);

            text.Print(
                $"Level: {CurrentLevel}\nSCORE: {Score}",
                OffsetX + 14,
                5,
                0.75f,
                Vector4.One,
                Vector4.Zero
            );
            if (ScoreDelta != 0)
            {
                Vector4 col = ScoreDelta > 0
                    ? new Vector4(0, 1, 0, 1)
                    : new Vector4(1, 0, 0, 1);

                string sign = ScoreDelta > 0 ? "+" : "";

                text.Print(
                    $"{sign}{ScoreDelta}",
                    OffsetX + 14, 7,
                    0.6f,
                    col,
                    Vector4.Zero
                );
            }

            text.Print(
                $"NEXT",
                OffsetX + 15,
                8,
                0.75f,
                Vector4.One,
                Vector4.Zero
            );
            text.Print(
                $"HOLD",
                OffsetX + 15,
                14,
                0.75f,
                Vector4.One,
                Vector4.Zero
            );
            text.Print(
                $"Lines: {Lines}\nTetris rate: {GetTetrisRate().ToString("0.#")}%",
                OffsetX + 1,
                2.5f,
                0.75f,
                Vector4.One,
                Vector4.Zero
            );
            if (GameOver)
            {
                if (isWinner)
                    text.Print("YOU WIN", OffsetX + 4, 14, 1.2f, new Vector4(0, 1, 0, 1), Vector4.Zero);
                else
                    text.Print("GAME OVER", OffsetX + 4, 14, 1.2f, new Vector4(1, 0, 0, 1), Vector4.Zero);
            }
        }
        public void Restart()
        {
            Grid = new int[Width, Height];
            GameOver = false;
            Hold = -1;
            holdUsed = false;
            Next = rng.Next(7);
            Score = 0;
            Lines = 0;
            ScoreDelta = 0;
            totalClearEvents = 0;
            TetrisCount = 0;
            incomingGarbage = new();
            outgoingGarbage = new();
            IsMatchResolved = false;
            isWinner = false;
            Spawn();
        }
        public void SetGameOver(bool iswinner)
        {
            GameOver = true;
            isWinner = iswinner;
        }
        public void AddGarbageLines(int count)
        {
            if (GameOver) return;

            // valore sicuro per la tile garbage (ultimo indice disponibile)
            int garbageValue = colors.Length - 1;

            // costruisci nuova griglia e trasferisci le righe esistenti spostandole verso l'alto
            int[,] newGrid = new int[Width, Height];

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    int ny = y + count;
                    if (ny < Height)
                        newGrid[x, ny] = Grid[x, y];
                }
            }

            // aggiungi le righe di garbage in basso (riga 0 .. count-1)
            for (int i = 0; i < count; i++)
            {
                int hole = rng.Next(Width);
                int row = i; // bottom rows are 0..count-1
                for (int x = 0; x < Width; x++)
                    newGrid[x, row] = (x == hole) ? 0 : garbageValue;
            }

            Grid = newGrid;

            // sposta il pezzo corrente verso l'alto della stessa quantità,
            // perché la griglia è stata "alzata"
            Pos.Y += count;

            // se c'è collisione prova a nudgere il pezzo verso l'alto (tentativi limitati)
            int tries = 0;
            while (Collide(Pos, Blocks) && tries < Height)
            {
                Pos.Y++;
                tries++;
            }

            if (Collide(Pos, Blocks))
            {
                Console.WriteLine("[GARBAGE] crushed by garbage");
                GameOver = true;
            }
            else
            {
                Console.WriteLine($"[GARBAGE] applied {count} lines (hole random).");
            }
        }

        void Spawn()
        {
            Current = Next;
            Next = rng.Next(7);
            Blocks = (Vector2i[])Shapes[Current].Clone();
            Pos = new Vector2i(Width / 2 - 2, Height - 2);
            holdUsed = false;
            dropDistance = 0;
            if (Collide(Pos, Blocks)) GameOver = true;
        }
        void Lock()
        {
            foreach (var b in Blocks)
            {
                int x = Pos.X + b.X;
                int y = Pos.Y + b.Y;
                if (x >= 0 && x < Width && y >= 0 && y < Height)
                    Grid[x, y] = Current + 1;
            }

            Score += dropDistance;
            ClearLines();

            // prendi garbage in arrivo
            int incoming = ConsumeIncomingGarbage();

            // spawn prima del posizionamento della garbage: la garbage va applicata
            // sul nuovo pezzo (comportamento standard multiplayer)
            Spawn();

            // applica garbage (se presente) dopo lo spawn del nuovo pezzo
            if (incoming > 0)
                AddGarbageLines(incoming);
        }

        public int ConsumeIncomingGarbage()
        {
            int total = 0;
            while (incomingGarbage.Count > 0)
                total += incomingGarbage.Dequeue();

            if (total > 0)
                Console.WriteLine($"[GARBAGE] IN consuming {total}");

            return total;
        }
        public void QueueIncomingGarbage(int amount)
        {
            incomingGarbage.Enqueue(amount);
        }
        public int ConsumeOutgoingGarbage()
        {
            int total = 0;
            while (outgoingGarbage.Count > 0)
                total += outgoingGarbage.Dequeue();

            if (total > 0)
                Console.WriteLine($"[GARBAGE] OUT consuming {total}");

            return total;
        }
        void ClearLines()
        {
            int cleared=0;
            for (int y = 0; y < Height; y++)
            {
                bool full = true;
                for (int x = 0; x < Width; x++) if (Grid[x, y] == 0) { full = false; break; }
                if (full)
                {
                    for (int yy = y; yy < Height - 1; yy++)
                        for (int x = 0; x < Width; x++)
                            Grid[x, yy] = Grid[x, yy + 1];
                    y--;
                    cleared++;
                    Score+=10;
                    Lines++;
                }
            }
            if (cleared > 0)
            {
                
                totalClearEvents++;
                int garbageToSend = 0;
                if (cleared > 3)
                {
                    garbageToSend = 3;
                    AduioLibrary.tetrisSound.Play();
                    Score += 30;
                    TetrisCount++;
                    Console.WriteLine("BOOM! TETRIS!");
                }
                else if (cleared > 1)
                {
                    garbageToSend = 1;
                    AduioLibrary.clearSound.Play();
                }
                else AduioLibrary.clearSound.Play();

                if (garbageToSend > 0)
                {
                    outgoingGarbage.Enqueue(garbageToSend);
                    Console.WriteLine($"[GARBAGE] OUT queued {garbageToSend}");
                }
            }
            else
            {
                AduioLibrary.collisionSound.Play();
            }
        }
        public void HoldPiece()
        {
            if (holdUsed) return;
            if (Hold == -1)
            {
                Hold = Current;
                Spawn();
            }
            else
            {
                int tmp = Current;
                Current = Hold;
                Hold = tmp;
                Blocks = (Vector2i[])Shapes[Current].Clone();
                Pos = new Vector2i(Width / 2 - 2, Height - 2);
                dropDistance = 0;
            }
            holdUsed = true;
        }
        float GetTetrisRate()
        {
            if (totalClearEvents == 0) return 0f;
            return ((float)TetrisCount / totalClearEvents) *100f;
        }
        bool TryMove(Vector2i d)
        {
            if (!Collide(Pos + d, Blocks))
            {
                Pos += d;
                return true;
            }
            return false;
        }
        void TryRotate(bool rotatioCCW=true)
        {
            AduioLibrary.rotateSound.Play();

            // O piece: no rotation
            if (Current == 3)
                return;

            Vector2i[] rotated = new Vector2i[4];

            // ===== I PIECE =====
            if (Current == 0)
            {
                bool horizontal = Blocks[0].Y == Blocks[1].Y;
                if (horizontal)
                    rotated = new[]
                    {
                    new Vector2i(1,0),
                    new Vector2i(1,1),
                    new Vector2i(1,2),
                    new Vector2i(1,3)
                };
                else
                    rotated = new[]
                    {
                    new Vector2i(0,1),
                    new Vector2i(1,1),
                    new Vector2i(2,1),
                    new Vector2i(3,1)
                };
            }
            // ===== OTHER PIECES =====
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    var b = Blocks[i];

                    rotated[i] = rotatioCCW
                        ? new Vector2i(b.Y, -b.X + 2)
                        : new Vector2i(-b.Y + 2, b.X);
                }
            }

            if (!Collide(Pos, rotated))
                Blocks = rotated;
        }
        bool Collide(Vector2i p, Vector2i[] b)
        {
            foreach (var c in b)
            {
                int x = p.X + c.X;
                int y = p.Y + c.Y;
                if (x < 0 || x >= Width || y < 0) return true;
                if (y < Height && Grid[x, y] != 0) return true;
            }
            return false;
        }
        public bool CanMove(Vector2i delta, Vector2i[] blocks, Vector2i pos)
        {
            foreach (var b in blocks)
            {
                int x = pos.X + b.X + delta.X;
                int y = pos.Y + b.Y + delta.Y;

                // muri laterali
                if (x < 0 || x >= Width)
                    return false;

                // fondo
                if (y < 0)
                    return false;

                // collisione con stack
                if (y < Height && Grid[x, y] != 0)
                    return false;
            }

            return true;
        }
        public int GetDropY()
        {
            int y = Pos.Y;
            while (CanMove(new Vector2i(0, -1), Blocks, new Vector2i(Pos.X, y)))
                y--;
            return y;
        }
    }
}
