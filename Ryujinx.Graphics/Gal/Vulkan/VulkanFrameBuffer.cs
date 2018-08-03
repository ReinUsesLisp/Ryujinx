using System;

namespace Ryujinx.Graphics.Gal.Vulkan
{
    internal class VulkanFrameBuffer : IGalFrameBuffer
    {
        private bool FlipX;
        private bool FlipY;

        private int CropTop;
        private int CropLeft;
        private int CropRight;
        private int CropBottom;

        public void Bind(long Key)
        {
            throw new NotImplementedException();
        }

        public void BindTexture(long Key, int Index)
        {
            throw new NotImplementedException();
        }

        public void Copy(long SrcKey, long DstKey, int SrcX0, int SrcY0, int SrcX1, int SrcY1, int DstX0, int DstY0, int DstX1, int DstY1)
        {
            throw new NotImplementedException();
        }

        public void Create(long Key, int Width, int Height)
        {
            throw new NotImplementedException();
        }

        public void GetBufferData(long Key, Action<byte[]> Callback)
        {
            throw new NotImplementedException();
        }

        public void Render()
        {
            throw new NotImplementedException();
        }

        public void Set(long Key)
        {
            throw new NotImplementedException();
        }

        public void Set(byte[] Data, int Width, int Height)
        {
            throw new NotImplementedException();
        }

        public void SetBufferData(long Key, int Width, int Height, GalTextureFormat Format, byte[] Buffer)
        {
            throw new NotImplementedException();
        }

        public void SetTransform(bool FlipX, bool FlipY, int Top, int Left, int Right, int Bottom)
        {
            this.FlipX = FlipX;
            this.FlipY = FlipY;

            CropTop    = Top;
            CropLeft   = Left;
            CropRight  = Right;
            CropBottom = Bottom;
        }

        public void SetViewport(int X, int Y, int Width, int Height)
        {
            throw new NotImplementedException();
        }

        public void SetWindowSize(int Width, int Height)
        {
            throw new NotImplementedException();
        }
    }
}