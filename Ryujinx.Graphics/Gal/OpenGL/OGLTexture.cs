using OpenTK.Graphics.OpenGL;
using Ryujinx.Graphics.Texture;
using System;

namespace Ryujinx.Graphics.Gal.OpenGL
{
    class OGLTexture : IGalTexture
    {
        private OGLCachedResource<ImageHandler> TextureCache;

        public OGLTexture()
        {
            TextureCache = new OGLCachedResource<ImageHandler>(DeleteTexture);
        }

        public void LockCache()
        {
            TextureCache.Lock();
        }

        public void UnlockCache()
        {
            TextureCache.Unlock();
        }

        private static void DeleteTexture(ImageHandler CachedImage)
        {
            GL.DeleteTexture(CachedImage.Handle);
        }

        public void Create(long Key, byte[] Data, GalImage Image)
        {
            ImageHandler Handler = new ImageHandler();

            TextureCache.AddOrUpdate(Key, Handler, ImageUtils.GetSize(Image, true));

            Handler.CreateTexture(Data, Image);
        }

        public void EnsureRT(long Key, GalImage Image)
        {
            if (!TryGetImage(Key, out ImageHandler CachedImage))
            {
                CachedImage = new ImageHandler();

                TextureCache.AddOrUpdate(Key, CachedImage, ImageUtils.GetSize(Image, true));
            }

            CachedImage.EnsureRT(Image);
        }

        public void Reinterpret(long Key, GalImage Image)
        {
            if (!TryGetImage(Key, out ImageHandler CachedImage))
            {
                throw new InvalidOperationException("Can't reinterpret an unexistant image");
            }

            CachedImage.Reinterpret(Image);
        }

        public bool TryGetImage(long Key, out ImageHandler CachedImage)
        {
            if (TextureCache.TryGetValue(Key, out CachedImage))
            {
                return true;
            }

            CachedImage = null;

            return false;
        }

        public bool TryGetCachedTexture(long Key, out GalImage Image)
        {
            if (TextureCache.TryGetSize(Key, out long Size) &&
                TextureCache.TryGetValue(Key, out ImageHandler CachedImage))
            {
                if (Size == ImageUtils.GetSize(CachedImage.Image, true))
                {
                    Image = CachedImage.Image;

                    return true;
                }
            }

            Image = default(GalImage);

            return false;
        }

        public bool IsCached(long Key)
        {
            return TextureCache.Contains(Key);
        }

        public void Bind(long Key, int Index, GalTextureSampler Sampler, GalImage Swizzle)
        {
            if (TextureCache.TryGetValue(Key, out ImageHandler CachedImage))
            {
                CachedImage.Bind(Index, Sampler, Swizzle);
            }
        }
    }
}
