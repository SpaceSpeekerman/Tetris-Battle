using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL;
using OpenTK;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace Tetris.OpenGL
{
    public class Vao : IDisposable
    {
        public int vaoId { get; private set; }
        public int eboId { get; private set; }
        public int vertexCount { get; private set; }
        public int elementCount { get; private set; }

        private List<int> Buffers { get; set; }

        public Vao()
        {
            Buffers = new List<int>();
            vaoId = GL.GenVertexArray();
            eboId = GL.GenBuffer();
        }

        public void SetElements(uint[] elements)
        {
            if(eboId == 0) eboId = GL.GenBuffer();
            GL.BindVertexArray(vaoId);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eboId);
            GL.BufferData(BufferTarget.ElementArrayBuffer, elements.Length * sizeof(uint), elements, BufferUsageHint.StaticDraw);
            elementCount = elements.Length;
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
        }
        public Vao(Vector3[] positions)
        {
            Buffers = new List<int>();
            vertexCount = positions.Length;
            vaoId = GL.GenVertexArray();

            AddData(positions, 3);
        }
        public Vao(Vector3[] positions, Vector2[] textcoord)
        {
            Buffers = new List<int>();
            vertexCount = positions.Length;
            vaoId = GL.GenVertexArray();

            AddData(positions, 3);
            AddData(textcoord, 2);
        }
        public Vao(Vector3[] positions, Vector2[] textcoord, uint[] elements)
        {
            Buffers = new List<int>();
            vertexCount = positions.Length;
            vaoId = GL.GenVertexArray();

            AddData(positions, 3);
            AddData(textcoord, 2);
            SetElements(elements);
        }
        public Vao(Vector2[] positions, Vector2[] textcoord)
        {
            Buffers = new List<int>();
            vertexCount = positions.Length;
            vaoId = GL.GenVertexArray();

            AddData(positions, 2);
            AddData(textcoord, 2);
        }
        public Vao(Vector2[] positions)
        {
            Buffers = new List<int>();
            vertexCount = positions.Length;
            vaoId = GL.GenVertexArray();

            AddData(positions, 2);
        }
        public int AddData<T>(T[] data, int vertexSize) where T : struct
        {
            GL.BindVertexArray(vaoId);
            vertexCount = data.Length;
            int typeSize = Marshal.SizeOf(data.GetType().GetElementType());

            //generate buffer add it and enable atribute array
            int bufferID = GL.GenBuffer();
            Buffers.Add(bufferID);
            int nBuffer = Buffers.Count - 1;
            GL.BindBuffer(BufferTarget.ArrayBuffer, bufferID);
            GL.BufferData(BufferTarget.ArrayBuffer, data.Length * typeSize, data, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(nBuffer, vertexSize, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(nBuffer);

            //unbind our buffers
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

            return nBuffer;
        }
        public void SetData<T>(int nBuffer, T[] data, int vertexSize) where T : struct
        {
            GL.BindVertexArray(vaoId);
            vertexCount = data.Length;
            int typeSize = Marshal.SizeOf(data.GetType().GetElementType());

            //generate buffer add it and enable atribute array
            GL.BindBuffer(BufferTarget.ArrayBuffer, Buffers[nBuffer]);
            GL.BufferData(BufferTarget.ArrayBuffer, data.Length * typeSize, data, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(nBuffer, vertexSize, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(nBuffer);

            //unbind our buffers
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }
        public void Draw(PrimitiveType type = PrimitiveType.Triangles)
        {
            Bind();
            if(elementCount > 0) GL.DrawElements(type, elementCount, DrawElementsType.UnsignedInt, 0);
            else GL.DrawArrays(type, 0, vertexCount);
            Unbind();
        }
        public void Bind()
        {
            GL.BindVertexArray(vaoId);
        }
        public void Unbind()
        {
            GL.BindVertexArray(0);
        }
        
        public void Dispose()
        {
            foreach (var bufferID in Buffers)
                GL.DeleteBuffer(bufferID);

            GL.DeleteBuffer(eboId);
            GL.DeleteVertexArray(vaoId);
        }
    }
}
