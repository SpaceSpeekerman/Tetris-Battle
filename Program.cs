using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;
using Tetris.Audio;
using static Tetris.TetrisInput;

namespace Tetris
{
    class Program
    {
        public static TetrisWindow win;
        static void Main()
        {
            var gws = new GameWindowSettings { UpdateFrequency = 60 };
            var nws = new NativeWindowSettings
            {
                ClientSize = new Vector2i(1280 ,720),
                Title = "Tetris"
            };
            AduioLibrary.Init();
            win = new TetrisWindow(gws, nws);
            win.Run();
        }
        public static int GetScore(int player)
        {
            return win.GetScore(player);
        }
        public struct GameOptions
        {
            public bool SoundOn;
            public bool GhostPiece;
            public bool HoldPiece;
            public bool NextPiece;
            public bool HardDrop;
            public bool LockDelay;

            public bool GarbageLines;
            public bool FirstPlayerWins;
            public bool InfiniteLevel;

            public bool Textured;

            public KeyBindings KeyBindingsPlayer1;
            public KeyBindings KeyBindingsPlayer2;
            public GamepadBindings GamepadBindingsPlayer1;
            public GamepadBindings GamepadBindingsPlayer2;
        }
        public static GameOptions Options = new GameOptions
        {
            KeyBindingsPlayer1 = new KeyBindings(),
            KeyBindingsPlayer2 = new KeyBindings()
            {
                Left = Keys.A,
                Right = Keys.D,
                Down = Keys.S,
                HardDrop = Keys.X,
                RotateCW = Keys.C,
                RotateCCW = Keys.V,
                Hold = Keys.Q,
                Start = Keys.D2
            },
            GamepadBindingsPlayer1 = new GamepadBindings(),
            GamepadBindingsPlayer2 = new GamepadBindings(),
            SoundOn = true,
            GhostPiece = true,
            HoldPiece = true,
            NextPiece = true,
            HardDrop = true,
            LockDelay = true,
            GarbageLines = true,
            FirstPlayerWins = true,
            InfiniteLevel = false,
            Textured = false
        };
    }

}
