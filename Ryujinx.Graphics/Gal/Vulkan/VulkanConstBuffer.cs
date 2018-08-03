using System;

namespace Ryujinx.Graphics.Gal.Vulkan
{
    internal class VulkanConstBuffer : IGalConstBuffer
    {
        public void Create(long Key, long Size)
        {
            throw new NotImplementedException();
        }

        public bool IsCached(long Key, long Size)
        {
            throw new NotImplementedException();
        }

        public void LockCache()
        {
            throw new NotImplementedException();
        }

        public void SetData(long Key, long Size, IntPtr HostAddress)
        {
            throw new NotImplementedException();
        }

        public void UnlockCache()
        {
            throw new NotImplementedException();
        }
    }
}