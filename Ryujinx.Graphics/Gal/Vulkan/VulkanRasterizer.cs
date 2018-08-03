using System;

namespace Ryujinx.Graphics.Gal.Vulkan
{
    internal class VulkanRasterizer : IGalRasterizer
    {
        public void ClearBuffers(GalClearBufferFlags Flags)
        {
            throw new NotImplementedException();
        }

        public void CreateIbo(long Key, int DataSize, IntPtr HostAddress)
        {
            throw new NotImplementedException();
        }

        public void CreateVbo(long Key, int DataSize, IntPtr HostAddress)
        {
            throw new NotImplementedException();
        }

        public void DrawArrays(int First, int PrimCount, GalPrimitiveType PrimType)
        {
            throw new NotImplementedException();
        }

        public void DrawElements(long IboKey, int First, int VertexBase, GalPrimitiveType PrimType)
        {
            throw new NotImplementedException();
        }

        public bool IsIboCached(long Key, long DataSize)
        {
            throw new NotImplementedException();
        }

        public bool IsVboCached(long Key, long DataSize)
        {
            throw new NotImplementedException();
        }

        public void LockCaches()
        {
            throw new NotImplementedException();
        }

        public void SetIndexArray(int Size, GalIndexFormat Format)
        {
            throw new NotImplementedException();
        }

        public void UnlockCaches()
        {
            throw new NotImplementedException();
        }
    }
}