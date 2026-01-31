using OpenTK.Graphics.OpenGL;
using OpenTK;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Tetris.OpenGL
{
    public class Shader : IDisposable
    {
        private int programId;
        private Dictionary<string, int> uniformLocations;

        public Shader()
        {
            programId = GL.CreateProgram();
            uniformLocations = new Dictionary<string, int>();
        }

        public Shader(string vertexShaderCode, string fragmentShaderCode)
        {
            programId = GL.CreateProgram();
            uniformLocations = new Dictionary<string, int>();
            Initialize(vertexShaderCode, fragmentShaderCode);
        }

        public Shader(Stream vertexShaderStream, Stream fragmentShaderStream)
        {
            programId = GL.CreateProgram();
            uniformLocations = new Dictionary<string, int>();
            string vertexShaderCode = ReadShaderCode(vertexShaderStream);
            string fragmentShaderCode = ReadShaderCode(fragmentShaderStream);
            Initialize(vertexShaderCode, fragmentShaderCode);
        }

        private string ReadShaderCode(Stream stream)
        {
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        public void Initialize(string vertexShaderCode, string fragmentShaderCode)
        {
            int vertexShaderId = CompileShader(ShaderType.VertexShader, vertexShaderCode);
            int fragmentShaderId = CompileShader(ShaderType.FragmentShader, fragmentShaderCode);

            GL.AttachShader(programId, vertexShaderId);
            GL.AttachShader(programId, fragmentShaderId);
            Link();
            GL.DetachShader(programId, vertexShaderId);
            GL.DetachShader(programId, fragmentShaderId);
            GL.DeleteShader(vertexShaderId);
            GL.DeleteShader(fragmentShaderId);
        }

        private int CompileShader(ShaderType type, string code)
        {
            int shaderId = GL.CreateShader(type);
            GL.ShaderSource(shaderId, code);
            GL.CompileShader(shaderId);

            string infoLog = GL.GetShaderInfoLog(shaderId);
            if (!string.IsNullOrEmpty(infoLog))
            {
                
                Console.WriteLine($"Shader compilation failed: {infoLog}");
            }

            return shaderId;
        }
        private void Link()
        {
            GL.LinkProgram(programId);
            GL.ValidateProgram(programId);
        }

        public void Bind()
        {
            GL.UseProgram(programId);
        }
        public void Unbind()
        {
            GL.UseProgram(0);
        }
        public void Dispose()
        {
            GL.DeleteProgram(programId);
        }
        public void SetUniform(string name, int value)
        {
            int location = GetUniformLocation(name);
            GL.Uniform1(location, value);
        }

        public void SetUniform(string name, float value)
        {
            int location = GetUniformLocation(name);
            GL.Uniform1(location, value);
        }

        public void SetUniform(string name, Vector4 value)
        {
            int location = GetUniformLocation(name);
            GL.Uniform4(location, value);
        }
        public void SetUniform(string name, Vector3 value)
        {
            int location = GetUniformLocation(name);
            GL.Uniform3(location, value);
        }
        public void SetUniform(string name, Vector2 value)
        {
            int location = GetUniformLocation(name);
            GL.Uniform2(location, value);
        }

        public void SetUniform(string name, Matrix4 value)
        {
            int location = GetUniformLocation(name);
            GL.UniformMatrix4(location, false, ref value);
        }

        private int GetUniformLocation(string name)
        {
            if (uniformLocations.ContainsKey(name))
            {
                return uniformLocations[name];
            }
            int location = GL.GetUniformLocation(programId, name);
            uniformLocations[name] = location;
            return location;
        }

        #region glsl standard
        public const string vertex3Color = @"
#version 330 core

layout (location = 0) in vec3 aPosition;

void main()
{
    gl_Position = vec4(aPosition, 1.0);
}
";
        public const string vertex2Color = @"
#version 330 core

layout (location = 0) in vec2 aPosition;

void main()
{
    gl_Position = vec4(aPosition,0.0, 1.0);
}
";
        public const string vertex2ColorMatrix = @"
#version 330 core

layout (location = 0) in vec2 aPosition;

uniform mat4 matrix;

void main()
{
    gl_Position = matrix * vec4(aPosition, 0.0, 1.0);
}
";
        public const string vertex3Matrix = @"
#version 330 core

layout (location = 0) in vec3 aPosition;

uniform mat4 matrix;

void main()
{
    gl_Position = matrix * vec4(aPosition, 1.0);
}
";
        public const string vertex3Texture = @"
#version 330 core

layout (location = 0) in vec3 vertexPosition;
layout (location = 1) in vec2 vertexTexCoord;

out vec2 texCoord;

void main()
{
    gl_Position = vec4(vertexPosition, 1.0);
    texCoord = vertexTexCoord;
}
";
        public const string vertex2TextureMatrix = @"
#version 330 core

layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec2 aTexCoord;

out vec2 texCoord;

uniform mat4 matrix;

void main()
{
    gl_Position = matrix * vec4(aPosition, 0.0, 1.0);
    texCoord = aTexCoord;
}
";
        public const string vertex3TextureMatrix = @"
#version 330 core

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec2 aTexCoord;

out vec2 texCoord;

uniform mat4 matrix;

void main()
{
    gl_Position = matrix * vec4(aPosition, 1.0);
    texCoord = aTexCoord;
}
";
        public const string fragmentColor = @"
#version 330 core

uniform vec4 color;

out vec4 fragColor;

void main()
{
    fragColor = color;
}
";
        public const string fragment = @"
#version 330 core

uniform float time;

out vec4 fragColor;

void main()
{
    fragColor = vec4(0,0,sin(time),1);
}
";
        public const string fragmentTexture = @"
#version 330 core

in vec2 texCoord;
uniform sampler2D textureSampler;

out vec4 fragColor;

void main()
{
    fragColor = texture(textureSampler, texCoord);
}
";
        public const string fragmentTextureColor = @"
#version 330 core

in vec2 texCoord;
uniform sampler2D textureSampler;
uniform vec4 color;

out vec4 fragColor;

void main()
{
    fragColor = texture(textureSampler, texCoord) * color;
}
";
        public const string fragmentMultiTexture = @"
#version 330 core

in vec2 texCoord;

uniform sampler2D textureSampler1;
uniform sampler2D textureSampler2;

out vec4 fragColor;

void main()
{
    vec4 color1 = texture(textureSampler1, texCoord);
    vec4 color2 = texture(textureSampler2, texCoord);
    
    fragColor = mix(color1, color2, 0.5);
}
";
        #endregion

    }
}
