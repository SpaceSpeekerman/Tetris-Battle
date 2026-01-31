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
        static void Main()
        {
            var gws = new GameWindowSettings { UpdateFrequency = 60 };
            var nws = new NativeWindowSettings
            {
                ClientSize = new Vector2i(1280 ,720),
                Title = "Tetris"
            };
            AduioLibrary.Init();
            using var win = new TetrisWindow(gws, nws);
            win.Run();
        }
        
        public struct GameOptions
        {
            public bool SoundOn;
            public bool GhostPiece;
            public bool HoldPiece;
            public bool NextPiece;
            public bool HardDrop;

            public bool LinePenalties;
            public bool FirstPlayerWins;
            public bool InfiniteLevel;

            public KeyBindings KeyBindingsPlayer1;
            public KeyBindings KeyBindingsPlayer2;
            public GamepadBindings GamepadBindingsPlayer1;
            public GamepadBindings GamepadBindingsPlayer2;
        }
        public static GameOptions Options = new GameOptions
        {
            KeyBindingsPlayer1 = new KeyBindings(),
            KeyBindingsPlayer2 = new KeyBindings(),
            GamepadBindingsPlayer1 = new GamepadBindings(),
            GamepadBindingsPlayer2 = new GamepadBindings(),
            SoundOn = true,
            GhostPiece = true,
            HoldPiece = true,
            NextPiece = true,
            HardDrop = true,
            LinePenalties = true,
            FirstPlayerWins = false,
            InfiniteLevel = false
        };
    }
}
