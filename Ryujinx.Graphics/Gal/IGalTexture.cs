namespace Ryujinx.Graphics.Gal
{
    public interface IGalTexture
    {
        void LockCache();
        void UnlockCache();

        void CreateTexture(long Key, byte[] Data, GalImage Image);

        void Reinterpret(long Key, GalImage Image);

        void EnsureRT(long Key, GalImage Image);

        bool TryGetCachedTexture(long Key, long DataSize, out GalImage Image);

        bool IsCached(long Key);

        void Bind(long Key, int Index, GalImage Swizzle, GalTextureSampler Sampler);
    }
}