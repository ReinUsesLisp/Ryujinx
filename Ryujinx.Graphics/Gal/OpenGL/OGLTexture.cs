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

        public void CreateTexture(long Key, byte[] Data, GalImage Image)
        {
            ImageHandler Handler = new ImageHandler();

            Handler.CreateTexture(Image, Data);

            TextureCache.AddOrUpdate(Key, Handler, (uint)Data.Length);
        }

        public void Reinterpret(long Key, GalImage Image)
        {
            if (!TextureCache.TryGetValue(Key, out ImageHandler Handler))
            {
                throw new InvalidOperationException();
            }

            Handler.Reinterpret(Image);

            Console.WriteLine("Manual Reinterpret");

            TextureCache.Resize(Key, ImageUtils.GetSize(Image));
        }

        public void EnsureRT(long Key, GalImage Image)
        {
            long Size = ImageUtils.GetSize(Image);

            if (TextureCache.TryGetValue(Key, out ImageHandler Handler))
            {
                if (!TextureCache.TryGetSize(Key, out long OldSize))
                {
                    throw new InvalidOperationException();
                }

                if (Size != OldSize || !Image.CacheEquals(Handler.Image))
                {
                    Console.WriteLine("RT Reinterpret");

                    Handler.Reinterpret(Image);

                    TextureCache.Resize(Key, Size);
                }
            }
            else
            {
                Handler = new ImageHandler();

                Handler.CreateRT(Image);

                TextureCache.AddOrUpdate(Key, Handler, Size);
            }
        }

        public bool TryGetHandler(long Key, out ImageHandler Handler)
        {
            if (TextureCache.TryGetValue(Key, out Handler))
            {
                return true;
            }

            Handler = null;

            return false;
        }

        public bool TryGetCachedTexture(long Key, long DataSize, out GalImage Image)
        {
            if (TextureCache.TryGetSize(Key, out long Size) && Size == DataSize)
            {
                if (TextureCache.TryGetValue(Key, out ImageHandler Handler))
                {
                    Image = Handler.Image;

                    return true;
                }
            }

            Image = default(GalImage);

            return false;
        }

        public bool IsCached(long Key)
        {
            //FIXME
            return TextureCache.TryGetSize(Key, out long Size);
        }

        public void Bind(long Key, int Index, GalImage Swizzle, GalTextureSampler Sampler)
        {
            if (!TextureCache.TryGetValue(Key, out ImageHandler CachedImage))
            {
                return;
            }

            CachedImage.Bind(Index, Swizzle, Sampler);
        }
    }
}
