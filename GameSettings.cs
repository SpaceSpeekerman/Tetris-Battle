using System;
using System.Collections.Generic;
using System.Text;

namespace Tetris
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Globalization;
    using OpenTK.Mathematics;

    public class GameSettings
    {
        private Dictionary<string, string> _data = new();

        // Default values if file is missing
        public float LockDelay = 0.333f;
        public float DasDelay = 0.1f;
        public float ArrRate = 0.08f;
        public int Width = 10;
        public int Height = 20;
        public List<Vector3> PieceColors = new();
        public float StartSpeed;
        public float LevelIncrement;
        public float MinSpeed;

        public GameSettings(string filePath)
        {
            if (File.Exists(filePath))
            {
                foreach (var line in File.ReadAllLines(filePath))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                    var parts = line.Split('=');
                    if (parts.Length == 2) _data[parts[0].Trim()] = parts[1].Trim();
                }
            }

            // Parse standard variables
            LockDelay = GetFloat("LockDelay", LockDelay);
            DasDelay = GetFloat("DelayAfterMove", DasDelay);
            ArrRate = GetFloat("AutoMoveRate", ArrRate);
            Width = GetInt("Width", Width);
            Height = GetInt("Height", Height);

            StartSpeed = GetFloat("StartSpeed", StartSpeed);
            LevelIncrement = GetFloat("LevelIncrement", LevelIncrement);
            MinSpeed = GetFloat("MinSpeed", MinSpeed);

            // Parse Colors (Format: Color0=0.5,0.5,0.5)
            for (int i = 0; i < 9; i++)
            {
                string key = $"Color{i}";
                if (_data.ContainsKey(key))
                {
                    var rgb = _data[key].Split(',');
                    PieceColors.Add(new Vector3(
                        float.Parse(rgb[0], CultureInfo.InvariantCulture),
                        float.Parse(rgb[1], CultureInfo.InvariantCulture),
                        float.Parse(rgb[2], CultureInfo.InvariantCulture)));
                }
            }
        }

        private float GetFloat(string key, float def) => _data.ContainsKey(key) ? float.Parse(_data[key], CultureInfo.InvariantCulture) : def;
        private double GetDouble(string key, double def) => _data.ContainsKey(key) ? double.Parse(_data[key], CultureInfo.InvariantCulture) : def;
        private int GetInt(string key, int def) => _data.ContainsKey(key) ? int.Parse(_data[key]) : def;
    }
}
