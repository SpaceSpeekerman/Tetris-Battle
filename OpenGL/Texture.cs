using OpenTK.Graphics.OpenGL;
using System.Drawing;
using System.Drawing.Imaging;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;
using StbImageSharp;
using System.IO;

namespace Tetris.OpenGL
{
    public class Texture
    {
        public readonly int Handle;

        public int Width { get; private set; }
        public int Height { get; private set; }

        // Activate texture
        public Texture(int glHandle)
        {
            Handle = glHandle;
        }
        public Texture(string path)
        {
            // Generate handle
            Handle = GL.GenTexture();

            // Bind the handle
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, Handle);

            // OpenGL has it's texture origin in the lower left corner instead of the top left corner,
            // so we tell StbImageSharp to flip the image when loading.

            StbImage.stbi_set_flip_vertically_on_load(1);

            using (Stream stream = File.OpenRead(path))
            {
                ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
                Width = image.Width;
                Height = image.Height;
                // Now that our pixels are prepared, it's time to generate a texture. We do this with GL.TexImage2D.
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);
            }

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            // Next, generate mipmaps.
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

        }

        public void Bind(TextureUnit unit = TextureUnit.Texture0)
        {
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D, Handle);
        }
        public void Unbind()
        {
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
        public void SetParam(TextureWrapMode wrap)
        {
            GL.BindTexture(TextureTarget.Texture2D, Handle);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
                (int)wrap);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
                (int)wrap);
        }
        public void SetParam(TextureMagFilter filter)
        {
            GL.BindTexture(TextureTarget.Texture2D, Handle);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)filter);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)filter);
        }
        public void Dispose()
        {
            GL.DeleteTexture(Handle);
        }
    }
}
