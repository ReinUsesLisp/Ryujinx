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

        public PixelInternalFormat InternalFormat { get; private set; }
        public PixelFormat         PixelFormat    { get; private set; }
        public PixelType           PixelType      { get; private set; }

        public bool HasColor   => ImageUtils.HasColor(Image.Format);
        public bool HasDepth   => ImageUtils.HasDepth(Image.Format);
        public bool HasStencil => ImageUtils.HasStencil(Image.Format);

        public int Handle { get; private set; }

        private void Create(byte[] Data, GalImage Image)
        {
            this.Image = Image;

            Handle = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2D, Handle);

            const int Level  = 0; //TODO: Support mipmap textures.
            const int Border = 0;

            GalImageFormat TypeLess = Image.Format & GalImageFormat.FormatMask;

            bool IsASTC = TypeLess >= GalImageFormat.ASTC_BEGIN && TypeLess <= GalImageFormat.ASTC_END;

            if (ImageUtils.IsCompressed(Image.Format) && !IsASTC)
            {
                InternalFormat InternalFmt = OGLEnumConverter.GetCompressedImageFormat(Image.Format);

                long Length = ImageUtils.GetSize(Image);

                GL.CompressedTexImage2D(
                    TextureTarget.Texture2D,
                    Level,
                    InternalFmt,
                    Image.Width,
                    Image.Height,
                    Border,
                    (int)Length,
                    Data);
            }
            else
            {
                //TODO: Use KHR_texture_compression_astc_hdr when available
                if (IsASTC)
                {
                    int TextureBlockWidth  = GetAstcBlockWidth(Image.Format);
                    int TextureBlockHeight = GetAstcBlockHeight(Image.Format);

                    Data = ASTCDecoder.DecodeToRGBA8888(
                        Data,
                        TextureBlockWidth,
                        TextureBlockHeight, 1,
                        Image.Width,
                        Image.Height, 1);

                    Image.Format = GalImageFormat.A8B8G8R8 | GalImageFormat.Unorm;
                }
                else if (TypeLess == GalImageFormat.G8R8)
                {
                    Data = ImageConverter.G8R8ToR8G8(
                        Data,
                        Image.Width,
                        Image.Height,
                        1);

                    Image.Format = GalImageFormat.R8G8 | (Image.Format & GalImageFormat.FormatMask);
                }

                (PixelInternalFormat InternalFormat, PixelFormat Format, PixelType Type) = OGLEnumConverter.GetImageFormat(Image.Format);

                GL.TexImage2D(
                    TextureTarget.Texture2D,
                    Level,
                    InternalFormat,
                    Image.Width,
                    Image.Height,
                    Border,
                    Format,
                    Type,
                    Data);
            }
        }

        public void CreateTexture(byte[] Data, GalImage Image)
        {
            Create(Data, Image);
        }

        public void EnsureRT(GalImage NewImage)
        {
            if (Handle == 0)
            {
                Create(null, NewImage);
            }
            else
            {
                if (!Image.CacheEquals(NewImage))
                {
                    Reinterpret(NewImage);
                }
            }
        }

        public void Reinterpret(GalImage NewImage)
        {
            PixelInternalFormat InternalFmt;
            PixelFormat         PixelFormat;
            PixelType           PixelType;

            if (ImageUtils.IsCompressed(NewImage.Format))
            {
                InternalFmt = (PixelInternalFormat)OGLEnumConverter.GetCompressedImageFormat(NewImage.Format);

                PixelFormat = default(PixelFormat);
                PixelType   = default(PixelType);
            }
            else
            {
                (InternalFmt, PixelFormat, PixelType) = OGLEnumConverter.GetImageFormat(NewImage.Format);
            }

            if (CopyBuffer == 0)
            {
                CopyBuffer = GL.GenBuffer();
            }

            int CurrentSize = Math.Max(ImageUtils.GetSize(NewImage),
                                       ImageUtils.GetSize(Image));

            GL.BindBuffer(BufferTarget.PixelPackBuffer,   CopyBuffer);
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, CopyBuffer);

            if (CopyBufferSize < CurrentSize)
            {
                CopyBufferSize = CurrentSize;

                GL.BufferData(BufferTarget.PixelPackBuffer, CurrentSize, IntPtr.Zero, BufferUsageHint.StreamCopy);
            }

            GL.BindTexture(TextureTarget.Texture2D, Handle);

            if (ImageUtils.IsCompressed(Image.Format))
            {
                GL.GetCompressedTexImage(TextureTarget.Texture2D, 0, IntPtr.Zero);
            }
            else
            {
                GL.GetTexImage(TextureTarget.Texture2D, 0, this.PixelFormat, this.PixelType, IntPtr.Zero);
            }

            /*GL.DeleteTexture(Handle);

            Handle = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2D, Handle);*/

            const int Level = 0;
            const int Border = 0;

            if (ImageUtils.IsCompressed(NewImage.Format))
            {
                GL.CompressedTexImage2D(
                    TextureTarget.Texture2D,
                    Level,
                    (InternalFormat)InternalFmt,
                    NewImage.Width,
                    NewImage.Height,
                    Border,
                    ImageUtils.GetSize(NewImage),
                    IntPtr.Zero);
            }
            else
            {
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
            }

            GL.BindBuffer(BufferTarget.PixelPackBuffer,   0);
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);

            Image.Width  = NewImage.Width;
            Image.Height = NewImage.Height;
            Image.Format = NewImage.Format;

            this.InternalFormat = InternalFmt;
            this.PixelFormat = PixelFormat;
            this.PixelType = PixelType;
        }

        public void Bind(int Index, GalTextureSampler Sampler, GalImage Swizzle)
        {
            Image.XSource = Swizzle.XSource;
            Image.YSource = Swizzle.YSource;
            Image.ZSource = Swizzle.ZSource;
            Image.WSource = Swizzle.WSource;

            GL.ActiveTexture(TextureUnit.Texture0 + Index);

            GL.BindTexture(TextureTarget.Texture2D, Handle);

            int WrapS = (int)OGLEnumConverter.GetTextureWrapMode(Sampler.AddressU);
            int WrapT = (int)OGLEnumConverter.GetTextureWrapMode(Sampler.AddressV);

            int MinFilter = (int)OGLEnumConverter.GetTextureMinFilter(Sampler.MinFilter, Sampler.MipFilter);
            int MagFilter = (int)OGLEnumConverter.GetTextureMagFilter(Sampler.MagFilter);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, WrapS);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, WrapT);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, MinFilter);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, MagFilter);

            float[] Color = new float[]
            {
                Sampler.BorderColor.Red,
                Sampler.BorderColor.Green,
                Sampler.BorderColor.Blue,
                Sampler.BorderColor.Alpha
            };

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, Color);

            int SwizzleR = (int)OGLEnumConverter.GetTextureSwizzle(Image.XSource);
            int SwizzleG = (int)OGLEnumConverter.GetTextureSwizzle(Image.YSource);
            int SwizzleB = (int)OGLEnumConverter.GetTextureSwizzle(Image.ZSource);
            int SwizzleA = (int)OGLEnumConverter.GetTextureSwizzle(Image.WSource);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleR, SwizzleR);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleG, SwizzleG);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleB, SwizzleB);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleA, SwizzleA);
        }

        private static int GetAstcBlockWidth(GalImageFormat Format)
        {
            switch (Format)
            {
                case GalImageFormat.ASTC_4x4   | GalImageFormat.Unorm: return 4;
                case GalImageFormat.ASTC_5x5   | GalImageFormat.Unorm: return 5;
                case GalImageFormat.ASTC_6x6   | GalImageFormat.Unorm: return 6;
                case GalImageFormat.ASTC_8x8   | GalImageFormat.Unorm: return 8;
                case GalImageFormat.ASTC_10x10 | GalImageFormat.Unorm: return 10;
                case GalImageFormat.ASTC_12x12 | GalImageFormat.Unorm: return 12;
                case GalImageFormat.ASTC_5x4   | GalImageFormat.Unorm: return 5;
                case GalImageFormat.ASTC_6x5   | GalImageFormat.Unorm: return 6;
                case GalImageFormat.ASTC_8x6   | GalImageFormat.Unorm: return 8;
                case GalImageFormat.ASTC_10x8  | GalImageFormat.Unorm: return 10;
                case GalImageFormat.ASTC_12x10 | GalImageFormat.Unorm: return 12;
                case GalImageFormat.ASTC_8x5   | GalImageFormat.Unorm: return 8;
                case GalImageFormat.ASTC_10x5  | GalImageFormat.Unorm: return 10;
                case GalImageFormat.ASTC_10x6  | GalImageFormat.Unorm: return 10;
            }

            throw new ArgumentException(nameof(Format));
        }

        private static int GetAstcBlockHeight(GalImageFormat Format)
        {
            switch (Format)
            {
                case GalImageFormat.ASTC_4x4   | GalImageFormat.Unorm: return 4;
                case GalImageFormat.ASTC_5x5   | GalImageFormat.Unorm: return 5;
                case GalImageFormat.ASTC_6x6   | GalImageFormat.Unorm: return 6;
                case GalImageFormat.ASTC_8x8   | GalImageFormat.Unorm: return 8;
                case GalImageFormat.ASTC_10x10 | GalImageFormat.Unorm: return 10;
                case GalImageFormat.ASTC_12x12 | GalImageFormat.Unorm: return 12;
                case GalImageFormat.ASTC_5x4   | GalImageFormat.Unorm: return 4;
                case GalImageFormat.ASTC_6x5   | GalImageFormat.Unorm: return 5;
                case GalImageFormat.ASTC_8x6   | GalImageFormat.Unorm: return 6;
                case GalImageFormat.ASTC_10x8  | GalImageFormat.Unorm: return 8;
                case GalImageFormat.ASTC_12x10 | GalImageFormat.Unorm: return 10;
                case GalImageFormat.ASTC_8x5   | GalImageFormat.Unorm: return 5;
                case GalImageFormat.ASTC_10x5  | GalImageFormat.Unorm: return 5;
                case GalImageFormat.ASTC_10x6  | GalImageFormat.Unorm: return 6;
            }

            throw new ArgumentException(nameof(Format));
        }
    }
}
