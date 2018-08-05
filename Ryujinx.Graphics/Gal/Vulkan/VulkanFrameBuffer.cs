using OpenTK.Graphics.Vulkan;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Graphics.Gal.Vulkan
{
    using static VulkanHelper;

    internal class VulkanFrameBuffer : IGalFrameBuffer
    {
        private struct Rect
        {
            public int X { get; private set; }
            public int Y { get; private set; }
            public int Width { get; private set; }
            public int Height { get; private set; }

            public Rect(int X, int Y, int Width, int Height)
            {
                this.X = X;
                this.Y = Y;
                this.Width = Width;
                this.Height = Height;
            }
        }

        private class FrameBuffer
        {
            public int Width { get; set; }
            public int Height { get; set; }

            public VkImage Image;
            public VkDeviceMemory Memory;

            public FrameBuffer(int Width, int Height)
            {
                this.Width = Width;
                this.Height = Height;
            }
        }

        private const int NativeWidth = 1280;
        private const int NativeHeight = 720;

        private VulkanDeviceQuery DeviceQuery;
        private VulkanSynchronization Synchronization;

        private readonly VkSurfaceKHR Surface;
        private readonly VkPhysicalDevice PhysicalDevice;
        private readonly VkDevice Device;
        private readonly VkQueue GraphicsQueue;
        private readonly VkQueue PresentQueue;
        private readonly VkCommandPool CommandPool;

        private VulkanSwapChain SwapChain;

        private VkSemaphore PresentSemaphore;
        private uint ImageIndex;

        private Rect Window;

        private FrameBuffer CurrReadFb;
        private FrameBuffer RawFb;

        private bool FlipX;
        private bool FlipY;

        private int CropTop;
        private int CropLeft;
        private int CropRight;
        private int CropBottom;

        public VulkanFrameBuffer(
            VulkanDeviceQuery DeviceQuery,
            VulkanSynchronization Synchronization,
            VkSurfaceKHR Surface,
            VkPhysicalDevice PhysicalDevice,
            VkDevice Device,
            VkQueue GraphicsQueue,
            VkQueue PresentQueue,
            VkCommandPool CommandPool)
        {
            this.DeviceQuery = DeviceQuery;
            this.Synchronization = Synchronization;
            this.Surface = Surface;
            this.PhysicalDevice = PhysicalDevice;
            this.Device = Device;
            this.GraphicsQueue = GraphicsQueue;
            this.PresentQueue = PresentQueue;
            this.CommandPool = CommandPool;

            SwapChain = new VulkanSwapChain(PhysicalDevice, Device, Surface);

            VkSemaphoreCreateInfo SemaphoreCI = VkSemaphoreCreateInfo.New();

            Check(VK.CreateSemaphore(Device, ref SemaphoreCI, IntPtr.Zero, out PresentSemaphore));
        }

        public void Dispose()
        {
            VK.DestroySemaphore(Device, PresentSemaphore, IntPtr.Zero);
        }

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

        public unsafe void Render()
        {
            if (CurrReadFb == null)
            {
                //Always clear the screen, validation layers will ask for a PresentSrcKHR otherwise

                VkClearValue ClearValue = new VkClearValue()
                {
                    color = new VkClearColorValue(0f, 0f, 0f, 1f)
                };

                VkRenderPassBeginInfo RenderPassBI = new VkRenderPassBeginInfo()
                {
                    sType = VkStructureType.RenderPassBeginInfo,
                    renderPass = SwapChain.RenderPass,
                    framebuffer = SwapChain.Framebuffers[ImageIndex],
                    renderArea = new VkRect2D((uint)Window.Width, (uint)Window.Height),
                    clearValueCount = 1,
                    pClearValues = &ClearValue
                };

                VkCommandBuffer ClearCmd = Synchronization.BeginRecord();

                VK.CmdBeginRenderPass(ClearCmd, ref RenderPassBI, VkSubpassContents.Inline);

                VK.CmdEndRenderPass(ClearCmd);

                Check(VK.EndCommandBuffer(ClearCmd));

                Synchronization.Execute(GraphicsQueue);

                return;
            }

            int SrcX0, SrcX1, SrcY0, SrcY1;

            if (CropLeft == 0 && CropRight == 0)
            {
                SrcX0 = 0;
                SrcX1 = CurrReadFb.Width;
            }
            else
            {
                SrcX0 = CropLeft;
                SrcX1 = CropRight;
            }

            if (CropTop == 0 && CropBottom == 0)
            {
                SrcY0 = 0;
                SrcY1 = CurrReadFb.Height;
            }
            else
            {
                SrcY0 = CropTop;
                SrcY1 = CropBottom;
            }

            float RatioX = MathF.Min(1f, (Window.Height * (float)NativeWidth) / ((float)NativeHeight * Window.Width));
            float RatioY = MathF.Min(1f, (Window.Width * (float)NativeHeight) / ((float)NativeWidth * Window.Height));

            int DstWidth = (int)(Window.Width * RatioX);
            int DstHeight = (int)(Window.Height * RatioY);

            int DstPaddingX = (Window.Width - DstWidth) / 2;
            int DstPaddingY = (Window.Height - DstHeight) / 2;

            int DstX0 = FlipX ? Window.Width - DstPaddingX : DstPaddingX;
            int DstX1 = FlipX ? DstPaddingX : Window.Width - DstPaddingX;

            int DstY0 = FlipY ? Window.Height - DstPaddingY : DstPaddingY;
            int DstY1 = FlipY ? DstPaddingY : Window.Height - DstPaddingY;

            //Record
            VkCommandBuffer BlitCmd = Synchronization.BeginRecord();

            VkImageSubresourceLayers Subresource = new VkImageSubresourceLayers()
            {
                aspectMask = VkImageAspectFlags.Color,
                baseArrayLayer = 0,
                layerCount = 1,
                mipLevel = 0
            };

            VkImageBlit Region = new VkImageBlit()
            {
                srcSubresource = Subresource,
                srcOffsets_0 = new VkOffset3D() { x = SrcX0, y = SrcY0, z = 0 },
                srcOffsets_1 = new VkOffset3D() { x = SrcX1, y = SrcY1, z = 1 },

                dstSubresource = Subresource,
                dstOffsets_0 = new VkOffset3D() { x = DstX0, y = DstY0, z = 0 },
                dstOffsets_1 = new VkOffset3D() { x = DstX1, y = DstY1, z = 1 }
            };

            SetImageLayout(
                BlitCmd,
                SwapChain.Images[ImageIndex],
                VkImageAspectFlags.Color,
                VkImageLayout.Undefined,
                VkImageLayout.TransferDstOptimal,
                VkPipelineStageFlags.Transfer,
                VkPipelineStageFlags.Transfer);

            SetImageLayout(
                BlitCmd,
                CurrReadFb.Image,
                VkImageAspectFlags.Color,
                VkImageLayout.Undefined,
                VkImageLayout.TransferSrcOptimal,
                VkPipelineStageFlags.Transfer,
                VkPipelineStageFlags.Transfer);

            VK.CmdBlitImage(
                BlitCmd,
                CurrReadFb.Image,
                VkImageLayout.TransferSrcOptimal,
                SwapChain.Images[ImageIndex],
                VkImageLayout.TransferDstOptimal,
                1, &Region,
                VkFilter.Linear);

            SetImageLayout(
                BlitCmd,
                SwapChain.Images[ImageIndex],
                VkImageAspectFlags.Color,
                VkImageLayout.TransferDstOptimal,
                VkImageLayout.PresentSrcKHR,
                VkPipelineStageFlags.Transfer,
                VkPipelineStageFlags.Transfer);

            Check(VK.EndCommandBuffer(BlitCmd));

            //Send to queue
            Synchronization.Execute(GraphicsQueue);
        }

        public unsafe void SwapBuffers()
        {
            ImageIndex = SwapChain.AcquireNextImage(PresentSemaphore);

            Check(VK.WaitForFences(Device, 1, ref SwapChain.Fences[ImageIndex], true, ulong.MaxValue));

            Check(VK.ResetFences(Device, 1, ref SwapChain.Fences[ImageIndex]));

            Render();

            VkSemaphore RenderSemaphore = Synchronization.QuerySemaphore();

            VkSubmitInfo SubmitInfo = new VkSubmitInfo()
            {
                sType = VkStructureType.SubmitInfo,
                commandBufferCount = 0,
            };

            VkPipelineStageFlags StageMask = VkPipelineStageFlags.AllGraphics;

            if (RenderSemaphore != VkSemaphore.Null)
            {
                SubmitInfo.waitSemaphoreCount = 1;
                SubmitInfo.pWaitSemaphores = &RenderSemaphore;
                SubmitInfo.pWaitDstStageMask = &StageMask;
            }

            Check(VK.QueueSubmit(GraphicsQueue, 1, ref SubmitInfo, SwapChain.Fences[ImageIndex]));

            SwapChain.QueuePresent(PresentQueue, ImageIndex, PresentSemaphore, RenderSemaphore);
        }

        public void Set(long Key)
        {
            throw new NotImplementedException();
        }

        private VkDeviceMemory SetBufferMemory;
        private VkBuffer SetBuffer;
        private int SetBufferSize;

        public unsafe void Set(byte[] Data, int Width, int Height)
        {
            if (RawFb == null || RawFb.Width != Width || RawFb.Height != Height)
            {
                CreateRawFb(Width, Height);
            }

            //FIXME: Query this in constructor
            uint GraphicsFamily = (uint)QueueFamilyIndices.Find(PhysicalDevice, Surface).GraphicsFamily;

            if (Width * Height * 4 > SetBufferSize)
            {
                if (SetBufferMemory != VkDeviceMemory.Null)
                {
                    //TODO: Is it really needed to wait here?
                    VK.DeviceWaitIdle(Device);

                    VK.DestroyBuffer(Device, SetBuffer, IntPtr.Zero);

                    VK.FreeMemory(Device, SetBufferMemory, IntPtr.Zero);
                }

                SetBufferSize = Width * Height * 4;

                VkBufferCreateInfo BufferCI = new VkBufferCreateInfo()
                {
                    sType = VkStructureType.BufferCreateInfo,
                    flags = VkBufferCreateFlags.None,
                    sharingMode = VkSharingMode.Exclusive,
                    queueFamilyIndexCount = 1,
                    pQueueFamilyIndices = &GraphicsFamily,
                    size = (ulong)SetBufferSize,
                    usage = VkBufferUsageFlags.TransferSrc
                };

                Check(VK.CreateBuffer(Device, ref BufferCI, IntPtr.Zero, out SetBuffer));

                VK.GetBufferMemoryRequirements(Device, SetBuffer, out VkMemoryRequirements MemoryRequeriments);

                VkMemoryAllocateInfo MemoryAI = new VkMemoryAllocateInfo()
                {
                    sType = VkStructureType.MemoryAllocateInfo,
                    allocationSize = MemoryRequeriments.size,
                    memoryTypeIndex = DeviceQuery.GetMemoryTypeIndex(MemoryRequeriments.memoryTypeBits, VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent)
                };

                Check(VK.AllocateMemory(Device, ref MemoryAI, IntPtr.Zero, out SetBufferMemory));

                Check(VK.BindBufferMemory(Device, SetBuffer, SetBufferMemory, 0));
            }

            IntPtr MapAddress;

            Check(VK.MapMemory(Device, SetBufferMemory, 0, (ulong)(Width * Height * 4), 0, (void**)&MapAddress));

            Marshal.Copy(Data, 0, MapAddress, Width * Height * 4);

            VK.UnmapMemory(Device, SetBufferMemory);

            //Record
            VkCommandBuffer SetCmd = Synchronization.BeginRecord();

            VkBufferImageCopy Region = new VkBufferImageCopy()
            {
                bufferRowLength = (uint)Width,
                bufferImageHeight = (uint)Height,
                imageExtent = new VkExtent3D()
                {
                    width = (uint)RawFb.Width,
                    height = (uint)RawFb.Height,
                    depth = 1
                },
                imageSubresource = new VkImageSubresourceLayers()
                {
                    mipLevel = 0,
                    baseArrayLayer = 0,
                    layerCount = 1,
                    aspectMask = VkImageAspectFlags.Color
                },
                bufferOffset = 0,
                imageOffset = new VkOffset3D()
            };

            VkImageLayout NewLayout = VkImageLayout.TransferSrcOptimal | VkImageLayout.TransferDstOptimal;

            SetImageLayout(
                SetCmd,
                RawFb.Image,
                VkImageAspectFlags.Color,
                VkImageLayout.Undefined,
                NewLayout,
                VkPipelineStageFlags.Host,
                VkPipelineStageFlags.Transfer);

            VK.CmdCopyBufferToImage(SetCmd, SetBuffer, RawFb.Image, NewLayout, 1, ref Region);

            Check(VK.EndCommandBuffer(SetCmd));

            Synchronization.Execute(GraphicsQueue);

            CurrReadFb = RawFb;
        }

        public void SetBufferData(long Key, int Width, int Height, GalTextureFormat Format, byte[] Buffer)
        {
            throw new NotImplementedException();
        }

        public void SetTransform(bool FlipX, bool FlipY, int Top, int Left, int Right, int Bottom)
        {
            this.FlipX = FlipX;
            this.FlipY = FlipY;

            CropTop = Top;
            CropLeft = Left;
            CropRight = Right;
            CropBottom = Bottom;
        }

        public void SetViewport(int X, int Y, int Width, int Height)
        {
            throw new NotImplementedException();
        }

        public void SetWindowSize(int Width, int Height)
        {
            Window = new Rect(0, 0, Width, Height);

            if (Width > 0 && Height > 0)
            {
                SwapChain.Create(Width, Height);
            }
        }

        private unsafe void CreateRawFb(int Width, int Height)
        {
            if (RawFb != null)
            {
                //TODO: Avoid halting here
                //TODO: Avoid reallocating memory when not needed (e.g. shrinking)

                VK.DeviceWaitIdle(Device);

                VK.FreeMemory(Device, RawFb.Memory, IntPtr.Zero);

                VK.DestroyImage(Device, RawFb.Image, IntPtr.Zero);
            }

            RawFb = new FrameBuffer(Width, Height);

            VkImageCreateInfo ImageCI = new VkImageCreateInfo()
            {
                sType = VkStructureType.ImageCreateInfo,

                flags = VkImageCreateFlags.None,
                imageType = VkImageType.Image2D,
                format = VkFormat.R8g8b8a8Unorm,
                extent = new VkExtent3D()
                {
                    width = (uint)Width,
                    height = (uint)Height,
                    depth = 1
                },
                mipLevels = 1,
                arrayLayers = 1,
                samples = VkSampleCountFlags.Count1,
                tiling = VkImageTiling.Optimal,
                usage = VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferDst | VkImageUsageFlags.TransferSrc,
                sharingMode = VkSharingMode.Exclusive,
                initialLayout = VkImageLayout.Undefined
            };

            Check(VK.CreateImage(Device, &ImageCI, IntPtr.Zero, out RawFb.Image));

            VK.GetImageMemoryRequirements(Device, RawFb.Image, out VkMemoryRequirements MemoryRequirements);

            VkMemoryAllocateInfo MemoryAI = new VkMemoryAllocateInfo()
            {
                sType = VkStructureType.MemoryAllocateInfo,
                allocationSize = MemoryRequirements.size,
                memoryTypeIndex = DeviceQuery.GetMemoryTypeIndex(MemoryRequirements.memoryTypeBits, VkMemoryPropertyFlags.DeviceLocal)
            };

            VK.AllocateMemory(Device, ref MemoryAI, IntPtr.Zero, out RawFb.Memory);

            VK.BindImageMemory(Device, RawFb.Image, RawFb.Memory, 0);
        }
    }
}