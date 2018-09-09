namespace Ryujinx.Graphics.Gal
{
    public interface IGalTexture
    {
        void LockCache();
        void UnlockCache();

        void Create(long Key, byte[] Data, GalImage Image);

        void EnsureRT(long Key, GalImage Image);

        void Reinterpret(long Key, GalImage Image);

        bool TryGetCachedTexture(long Key, out GalImage Image);

        bool IsCached(long Key);

        void Bind(long Key, int Index, GalTextureSampler Sampler, GalImage Swizzle);
    }
}