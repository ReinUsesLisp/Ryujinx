namespace Ryujinx.Graphics.Gal.Vulkan
{
    internal class VulkanTexture : IGalTexture
    {
        public void Bind(long Key, int Index)
        {
            throw new System.NotImplementedException();
        }

        public void Create(long Key, byte[] Data, GalTexture Texture)
        {
            throw new System.NotImplementedException();
        }

        public void LockCache()
        {
            throw new System.NotImplementedException();
        }

        public void SetSampler(GalTextureSampler Sampler)
        {
            throw new System.NotImplementedException();
        }

        public bool TryGetCachedTexture(long Key, long DataSize, out GalTexture Texture)
        {
            throw new System.NotImplementedException();
        }

        public void UnlockCache()
        {
            throw new System.NotImplementedException();
        }
    }
}