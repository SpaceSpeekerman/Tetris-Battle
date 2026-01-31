using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Tetris.OpenGL
{
    public class TextRenderer : IDisposable
    {
        #region shaders code
        string vertexShaderSource = @"
            #version 330 core

            layout (location = 0) in vec2 aPosition;
            layout (location = 1) in vec2 aTexCoord;

            out vec2 TexCoord;

            uniform mat4 model;

            void main()
            {
                gl_Position = model * vec4(aPosition, 0.0, 1.0);
                TexCoord = aTexCoord;
            }
        ";
        string fragmentShaderSource = @"
            #version 330 core
            in vec2 TexCoord;
            out vec4 FragColor;

            uniform sampler2D textureSampler;
            uniform vec4 textColor;
            uniform vec4 backColor;
            uniform vec2 offset;

void main()
{
    vec4 color = texture(textureSampler, TexCoord.xy + offset.xy) * textColor;
    if (color.a < 0.0001) {
        color = backColor;
    }
    FragColor = color;
}
";

        #endregion
        private Vao vao;
        private Shader shaderProgram;
        private Texture texture;
        private int asciiWidth;
        private int asciiHeight;
        float spriteWidth;
        float spriteHeight;
        private int charW;
        private int charH;
        private int asciiOffset;
        Vector2 screenRes = new Vector2(1, 1); // IMPORTANTE se non usi le coordinate -1/1 per lo scermo ficcacele qua
        // e se le lasci così devi fare occhio con size quando Printi lì eh
        public TextRenderer(Font f)
        {
            texture = f.texture;
            charH = f.charWidth;
            charW = f.charHeight;
            asciiOffset = f.asciiOffset;

            asciiWidth = f.texture.Width / f.charWidth;
            asciiHeight = f.texture.Height / f.charHeight;
            spriteWidth = 1.0f / asciiWidth;
            spriteHeight = 1.0f / asciiHeight;

            shaderProgram = new Shader(vertexShaderSource, fragmentShaderSource);

            Vector2[] positions = {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1*GetCharResolution()),
                new Vector2(0, 1*GetCharResolution())
            };

            Vector2[] texCoords = {
                new Vector2(0, spriteHeight),
                new Vector2(spriteWidth, spriteHeight),
                new Vector2(spriteWidth, 0),
                new Vector2(0, 0)
            };

            uint[] elements = {
                0, 1, 2,
                2, 3, 0
            };

            vao = new Vao();
            vao.AddData(positions, 2);
            vao.AddData(texCoords, 2);
            vao.SetElements(elements);
        }
        public void Print(string text, float x, float y, float size, Vector4 color, Vector4 backgroundColor)
        {
            GL.Enable(EnableCap.Texture2D);
            shaderProgram.Bind();
            texture.Bind();

            // Set these based on how you want to address the screen (e.g., 0-100 or 0-1)
            float logicalWidth = 48f;
            float logicalHeight = 27f;
            Matrix4 projection = Matrix4.CreateOrthographicOffCenter(0, logicalWidth, logicalHeight, 0f, 0, 1);

            string[] lines = text.Split('\n');

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                for (int i = 0; i < line.Length; i++)
                {
                    int asciiCode = line[i] - asciiOffset;
                    if (asciiCode < 0 || asciiCode >= asciiWidth * asciiHeight) continue;

                    int asciiX = asciiCode % asciiWidth;
                    int asciiY = (asciiHeight - 1) - (asciiCode / asciiWidth); // Stay with the flip fix

                    Vector2 spriteOffset = new Vector2((float)asciiX / asciiWidth, (float)asciiY / asciiHeight);

                    // 1. Scale the quad 
                    // 2. Translate to grid position (i is horizontal offset for chars)
                    // 3. Project to the virtual 100x100 space
                    Matrix4 model = Matrix4.CreateScale(size) * Matrix4.CreateTranslation(new Vector3(x + (i * size * GetCharResolution()), y + (lineIndex * size), 0f)) * projection;

                    shaderProgram.SetUniform("model", model);
                    shaderProgram.SetUniform("textColor", color);
                    shaderProgram.SetUniform("backColor", backgroundColor);
                    shaderProgram.SetUniform("offset", spriteOffset);

                    vao.Draw();
                }
            }
            shaderProgram.Unbind();
            texture.Unbind();
        }
        public void PrintLine(string text, int x, int y, float size, Vector4 color, Vector4 backgroundColor)
        {
            for (int i = 0; i < text.Length; i++)
            {
                PrintChar(text[i], x + i, y, size, color, backgroundColor);
            }
        }
        public void PrintChar(char c, int x, int y, float size, Vector4 color, Vector4 backgroundColor)
        {
            GL.Enable(EnableCap.Texture2D);
            shaderProgram.Bind();
            texture.Bind();

            int asciiCode = c - asciiOffset;
            if (asciiCode < 0 || asciiCode >= asciiWidth * asciiHeight)
                return; // Skip invalid characters

            int asciiX = asciiCode % asciiWidth;
            // int asciiY = asciiCode / asciiWidth; when image was loading flipped on the Y
            int asciiY = (asciiHeight - 1) - (asciiCode / asciiWidth);

            Vector2 sprite = new Vector2((float)asciiX / asciiWidth, (float)asciiY / asciiHeight);
            Matrix4 model = Matrix4.CreateTranslation(new Vector3(x, y * GetCharResolution(), 0f)) *
                Matrix4.CreateScale(size) *
                Matrix4.CreateOrthographicOffCenter(0, screenRes.X, screenRes.Y, 0, 0, 1); ;
            shaderProgram.SetUniform("model", model);
            shaderProgram.SetUniform("textColor", color);
            shaderProgram.SetUniform("backColor", backgroundColor);
            shaderProgram.SetUniform("offset", sprite);

            vao.Draw();

            GL.Disable(EnableCap.Texture2D);
            shaderProgram.Unbind();
            texture.Unbind();
        }

        public void screenResolution(int width, int height)
        {
            screenRes = new Vector2(width, height);
        }
        public float GetCharResolution()
        {
            return (float)charW / charH;
        }
        public void Dispose()
        {
            shaderProgram.Dispose();
            texture.Dispose();
            vao.Dispose();
        }
    }
    public struct Font
    {
        public string filePath;
        public Texture texture;
        public int fontsize;
        public int charWidth;
        public int charHeight;
        public int asciiOffset;
        public float spriteWidth;
        public float spriteHeight;

        public Font(string filePath, int fontsize, int charW, int charH, int asciiOffset = 0)
        {
            this.filePath = filePath;
            this.texture = new Texture(filePath);
            this.fontsize = fontsize;
            this.charWidth = charW;
            this.charHeight = charH;
            this.asciiOffset = asciiOffset;
            spriteWidth = 1.0f / (texture.Width / charWidth);
            spriteHeight = 1.0f / (texture.Height / charHeight);

        }
        public int GetHandle() { return texture.Handle; }
    }
}
