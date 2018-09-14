using OpenTK.Graphics.OpenGL;
using Ryujinx.Graphics.Texture;
using System;

namespace Ryujinx.Graphics.Gal.OpenGL
{
    class ImageHandler
    {
        private static int CopyBuffer = 0;
        private static int CopyBufferSize = 0;

        public GalImage Image;

        public int Width  => Image.Width;
        public int Height => Image.Height;

        public GalImageFormat Format => Image.Format;

        public PixelInternalFormat InternalFmt { get; private set; }
        public PixelFormat         PixelFormat    { get; private set; }
        public PixelType           PixelType      { get; private set; }

        public int Handle { get; private set; }

        private bool Initialized;

        public ImageHandler()
        {
            Handle = GL.GenTexture();
        }

        public ImageHandler(int Handle, GalImage Image)
        {
            this.Handle = Handle;

            this.Image = Image;
        }

        public void EnsureSetup(GalImage NewImage)
        {
            if (Width  == NewImage.Width  &&
                Height == NewImage.Height &&
                Format == NewImage.Format &&
                Initialized)
            {
                return;
            }

            if (ImageUtils.IsCompressed(NewImage.Format))
            {
                throw new NotImplementedException();
            }

            (PixelInternalFormat InternalFmt, PixelFormat PixelFormat, PixelType PixelType) =
                OGLEnumConverter.GetImageFormat(NewImage.Format);

            GL.BindTexture(TextureTarget.Texture2D, Handle);

            if (Initialized)
            {
                if (CopyBuffer == 0)
                {
                    CopyBuffer = GL.GenBuffer();
                }

                int CurrentSize = Math.Max(ImageUtils.GetSize(NewImage),
                                           ImageUtils.GetSize(Image));

                GL.BindBuffer(BufferTarget.PixelPackBuffer, CopyBuffer);
                GL.BindBuffer(BufferTarget.PixelUnpackBuffer, CopyBuffer);

                if (CopyBufferSize < CurrentSize)
                {
                    CopyBufferSize = CurrentSize;

                    GL.BufferData(BufferTarget.PixelPackBuffer, CurrentSize, IntPtr.Zero, BufferUsageHint.StreamCopy);
                }

                GL.GetTexImage(TextureTarget.Texture2D, 0, this.PixelFormat, this.PixelType, IntPtr.Zero);
            }

            const int MinFilter = (int)TextureMinFilter.Linear;
            const int MagFilter = (int)TextureMagFilter.Linear;

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, MinFilter);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, MagFilter);

            const int Level = 0;
            const int Border = 0;

            GL.TexImage2D(
                TextureTarget.Texture2D,
                Level,
                InternalFmt,
                NewImage.Width,
                NewImage.Height,
                Border,
                PixelFormat,
                PixelType,
                IntPtr.Zero);

            if (Initialized)
            {
                GL.BindBuffer(BufferTarget.PixelPackBuffer,   0);
                GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);
            }

            Image.Width  = NewImage.Width;
            Image.Height = NewImage.Height;
            Image.Format = NewImage.Format;

            this.InternalFmt = InternalFmt;
            this.PixelFormat = PixelFormat;
            this.PixelType = PixelType;

            Initialized = true;
        }

        public bool HasColor   => ImageUtils.HasColor(Image.Format);
        public bool HasDepth   => ImageUtils.HasDepth(Image.Format);
        public bool HasStencil => ImageUtils.HasStencil(Image.Format);
    }
}
