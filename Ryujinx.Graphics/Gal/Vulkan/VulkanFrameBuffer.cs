using OpenTK.Graphics.Vulkan;
using System;

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

            public FrameBuffer(int Width, int Height)
            {
                this.Width  = Width;
                this.Height = Height;    
            }
        }

        private const int NativeWidth  = 1280;
        private const int NativeHeight = 720;

        private VulkanSemaphores Semaphores;

        private VkSurfaceKHR Surface;
        private VkPhysicalDevice PhysicalDevice;
        private VkDevice Device;
        private VkQueue PresentQueue;

        private bool HasSwapChain;
        private VkSwapchainKHR SwapChain;
        private VulkanList<VkImage> SwapChainImages;
        private VkFormat SwapChainImageFormat;
        private VkExtent2D SwapChainExtent;
        private VulkanList<VkImageView> SwapChainImageViews;
        private VkRenderPass RenderPass;
        private VulkanList<VkFramebuffer> SwapChainFrameBuffers;

        private VkSemaphore PresentComplete;

        private FrameBuffer CurrFb;
        private FrameBuffer CurrReadFb;

        private Rect Window;

        private bool FlipX;
        private bool FlipY;

        private int CropTop;
        private int CropLeft;
        private int CropRight;
        private int CropBottom;

        public VulkanFrameBuffer(
            VulkanSemaphores Semaphores,
            VkSurfaceKHR Surface,
            VkPhysicalDevice PhysicalDevice,
            VkDevice Device,
            VkQueue PresentQueue)
        {
            this.Semaphores     = Semaphores;
            this.Surface        = Surface;
            this.PhysicalDevice = PhysicalDevice;
            this.Device         = Device;
            this.PresentQueue   = PresentQueue;

            VkSemaphoreCreateInfo SemaphoreCI = VkSemaphoreCreateInfo.New();

            unsafe
            {
                Check(VK.CreateSemaphore(Device, &SemaphoreCI, IntPtr.Zero, out PresentComplete));
            }
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
            throw new NotImplementedException();
        }

        public unsafe void SwapBuffers()
        {
            if (!HasSwapChain)
            {
                return;
            }

            VkSwapchainKHR SwapChain = this.SwapChain;

            uint ImageIndex;
            VkResult Result = VK.AcquireNextImageKHR(Device, SwapChain, ulong.MaxValue, PresentComplete, VkFence.Null, &ImageIndex);

            if (Result == VkResult.ErrorOutOfDateKHR || Result == VkResult.SuboptimalKHR)
            {
                RecreateSwapChain();
            }
            else
            {
                Check(Result);
            }

            VkPresentInfoKHR PresentInfo = new VkPresentInfoKHR()
            {
                sType = VkStructureType.PresentInfoKHR,
                swapchainCount = 1,
                pSwapchains = &SwapChain,
                pImageIndices = &ImageIndex
            };

            VkSemaphore WaitRender = Semaphores.Pop();

            if (WaitRender != VkSemaphore.Null)
            {
                PresentInfo.waitSemaphoreCount = 1;
                PresentInfo.pWaitSemaphores = &WaitRender;
            }

            Result = VK.QueuePresentKHR(PresentQueue, &PresentInfo);

            if (Result == VkResult.ErrorOutOfDateKHR)
            {
                RecreateSwapChain();
                return;
            }
            else
            {
                Check(Result);
            }

            //FIXME: Wait am I waiting here?
            Check(VK.QueueWaitIdle(PresentQueue));
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
            Window = new Rect(0, 0, Width, Height);

            RecreateSwapChain();
        }

        private void RecreateSwapChain()
        {
            VK.DeviceWaitIdle(Device);

            CleanupSwapChain();

            if (Window.Width > 0 && Window.Height > 0)
            {
                if (CreateSwapChain())
                {
                    CreateImageViews();
                    CreateFrameBuffers();

                    HasSwapChain = true;
                }
            }
        }

        private void CleanupSwapChain()
        {
            if (!HasSwapChain)
            {
                return;
            }

            foreach (VkFramebuffer Framebuffer in SwapChainFrameBuffers)
            {
                VK.DestroyFramebuffer(Device, Framebuffer, IntPtr.Zero);
            }

            VK.DestroyRenderPass(Device, RenderPass, IntPtr.Zero);

            foreach (VkImageView ImageView in SwapChainImageViews)
            {
                VK.DestroyImageView(Device, ImageView, IntPtr.Zero);
            }

            VK.DestroySwapchainKHR(Device, SwapChain, IntPtr.Zero);

            HasSwapChain = false;
        }

        private unsafe bool CreateSwapChain()
        {
            SwapChainSupportDetails SwapChainSupport = SwapChainSupportDetails.Query(PhysicalDevice, Surface);

            VkSurfaceFormatKHR SurfaceFormat = ChooseSwapSurfaceFormat(SwapChainSupport.Formats);
            VkPresentModeKHR PresentMode     = ChooseSwapPresentMode(SwapChainSupport.PresentModes);
            VkExtent2D Extent                = ChooseSwapExtent(SwapChainSupport.Capabilities);

            uint ImageCount = SwapChainSupport.Capabilities.minImageCount + 1;

            if (SwapChainSupport.Capabilities.maxImageCount > 0 &&
                ImageCount > SwapChainSupport.Capabilities.maxImageCount)
            {
                ImageCount = SwapChainSupport.Capabilities.maxImageCount;
            }

            VkSwapchainCreateInfoKHR CreateInfo = new VkSwapchainCreateInfoKHR()
            {
                sType = VkStructureType.SwapchainCreateInfoKHR,
                surface = Surface,
                minImageCount = ImageCount,
                imageFormat = SurfaceFormat.format,
                imageColorSpace = SurfaceFormat.colorSpace,
                imageExtent = Extent,
                imageArrayLayers = 1,
                imageUsage = VkImageUsageFlags.ColorAttachment,
                preTransform = SwapChainSupport.Capabilities.currentTransform,
                compositeAlpha = VkCompositeAlphaFlagsKHR.OpaqueKHR,
                presentMode = PresentMode,
                clipped = true,
                oldSwapchain = VkSwapchainKHR.Null
            };

            QueueFamilyIndices Indices = QueueFamilyIndices.Find(PhysicalDevice, Surface);

            if (Indices.GraphicsFamily != Indices.PresentFamily)
            {
                VulkanList<uint> QueueFamilyIndices = new VulkanList<uint>()
                {
                    (uint)Indices.GraphicsFamily,
                    (uint)Indices.PresentFamily
                };

                CreateInfo.imageSharingMode = VkSharingMode.Concurrent;
                CreateInfo.queueFamilyIndexCount = 2;
                CreateInfo.pQueueFamilyIndices = (uint*)QueueFamilyIndices.Data;
            }
            else
            {
                CreateInfo.imageSharingMode = VkSharingMode.Exclusive;
            }

            if (VK.CreateSwapchainKHR(Device, &CreateInfo, IntPtr.Zero, out SwapChain) != VkResult.Success)
            {
                return false;
            }

            uint SwapChainImageCount = 0;
            Check(VK.GetSwapchainImagesKHR(Device, SwapChain, ref SwapChainImageCount, (VkImage*)null));

            SwapChainImages = new VulkanList<VkImage>(SwapChainImageCount, SwapChainImageCount);
            Check(VK.GetSwapchainImagesKHR(Device, SwapChain, ref SwapChainImageCount, (VkImage*)SwapChainImages.Data));

            SwapChainImageFormat = SurfaceFormat.format;
            SwapChainExtent = Extent;

            return true;
        }

        private unsafe void CreateImageViews()
        {
            SwapChainImageViews = new VulkanList<VkImageView>(SwapChainImages.Count, SwapChainImages.Count);

            for (int i = 0; i < SwapChainImageViews.Count; i++)
            {
                VkImageViewCreateInfo CreateInfo = new VkImageViewCreateInfo
                {
                    sType = VkStructureType.ImageViewCreateInfo,
                    image = SwapChainImages[i],
                    viewType = VkImageViewType.Image2D,
                    format = SwapChainImageFormat,
                    components = new VkComponentMapping
                    {
                        r = VkComponentSwizzle.Identity,
                        g = VkComponentSwizzle.Identity,
                        b = VkComponentSwizzle.Identity,
                        a = VkComponentSwizzle.Identity
                    },
                    subresourceRange = new VkImageSubresourceRange
                    {
                        aspectMask = VkImageAspectFlags.Color,
                        baseMipLevel = 0,
                        levelCount = 1,
                        baseArrayLayer = 0,
                        layerCount = 1
                    }
                };

                Check(VK.CreateImageView(Device, &CreateInfo, IntPtr.Zero, out VkImageView View));

                SwapChainImageViews[i] = View;
            }
        }

        private unsafe void CreateRenderPass()
        {
            VkAttachmentDescription ColorAttachment = new VkAttachmentDescription
            {
                format = SwapChainImageFormat,
                samples = VkSampleCountFlags.Count1,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                stencilLoadOp = VkAttachmentLoadOp.DontCare,
                stencilStoreOp = VkAttachmentStoreOp.DontCare,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.PresentSrcKHR
            };

            VkAttachmentReference ColorAttachmentRef = new VkAttachmentReference
            {
                attachment = 0,
                layout = VkImageLayout.ColorAttachmentOptimal
            };

            VkSubpassDescription Subpass = new VkSubpassDescription
            {
                pipelineBindPoint = VkPipelineBindPoint.Graphics,
                colorAttachmentCount = 1,
                pColorAttachments = &ColorAttachmentRef
            };

            VkSubpassDependency Dependency = new VkSubpassDependency()
            {
                srcSubpass = VK.SubpassExternal,
                dstSubpass = 0,
                srcStageMask = VkPipelineStageFlags.ColorAttachmentOutput,
                srcAccessMask = 0,
                dstStageMask = VkPipelineStageFlags.ColorAttachmentOutput,
                dstAccessMask = VkAccessFlags.ColorAttachmentRead | VkAccessFlags.ColorAttachmentWrite,
            };

            VkRenderPassCreateInfo RenderPassInfo = new VkRenderPassCreateInfo
            {
                sType = VkStructureType.RenderPassCreateInfo,
                attachmentCount = 1,
                pAttachments = &ColorAttachment,
                subpassCount = 1,
                pSubpasses = &Subpass,
                dependencyCount = 1,
                pDependencies = &Dependency
            };

            Check(VK.CreateRenderPass(Device, &RenderPassInfo, IntPtr.Zero, out RenderPass));
        }

        private unsafe void CreateFrameBuffers()
        {
            SwapChainFrameBuffers = new VulkanList<VkFramebuffer>(SwapChainImageViews.Count);

            for (int i = 0; i < SwapChainImageViews.Count; i++)
            {
                VulkanList<VkImageView> Attachments = new VulkanList<VkImageView>
                {
                    SwapChainImageViews[i]
                };

                VkFramebufferCreateInfo FramebufferInfo = new VkFramebufferCreateInfo
                {
                    sType = VkStructureType.FramebufferCreateInfo,
                    renderPass = RenderPass,
                    attachmentCount = 1,
                    pAttachments = (VkImageView*)Attachments.Data,
                    width = SwapChainExtent.width,
                    height = SwapChainExtent.height,
                    layers = 1
                };

                Check(VK.CreateFramebuffer(Device, &FramebufferInfo, IntPtr.Zero, out VkFramebuffer Framebuffer));

                SwapChainFrameBuffers.Add(Framebuffer);
            }
        }

        private unsafe VkExtent2D ChooseSwapExtent(VkSurfaceCapabilitiesKHR Capabilities)
        {
            if (Capabilities.currentExtent.width != UInt32.MaxValue)
            {
                return Capabilities.currentExtent;
            }
            else
            {
                return new VkExtent2D
                {
                    width  = (uint)Window.Width,
                    height = (uint)Window.Height
                };
            }
        }

        private unsafe VkPresentModeKHR ChooseSwapPresentMode(VulkanList<VkPresentModeKHR> AvailablePresentModes)
        {
            VkPresentModeKHR BestMode = VkPresentModeKHR.FifoKHR;

            foreach (VkPresentModeKHR AvailablePresentMode in AvailablePresentModes)
            {
                if (AvailablePresentMode == VkPresentModeKHR.MailboxKHR)
                {
                    return AvailablePresentMode;
                }
                else if (AvailablePresentMode == VkPresentModeKHR.ImmediateKHR)
                {
                    BestMode = AvailablePresentMode;
                }
            }

            return BestMode;
        }

        private unsafe VkSurfaceFormatKHR ChooseSwapSurfaceFormat(VulkanList<VkSurfaceFormatKHR> AvailableFormats)
        {
            if (AvailableFormats.Count == 1 && AvailableFormats[0].format == VkFormat.Undefined)
            {
                return new VkSurfaceFormatKHR()
                {
                    format = VkFormat.B8g8r8a8Unorm,
                    colorSpace = VkColorSpaceKHR.SrgbNonlinearKHR
                };
            }

            foreach (VkSurfaceFormatKHR AvailableFormat in AvailableFormats)
            {
                if (AvailableFormat.format == VkFormat.B8g8r8a8Unorm &&
                    AvailableFormat.colorSpace == VkColorSpaceKHR.SrgbNonlinearKHR)
                {
                    return AvailableFormat;
                }
            }

            return AvailableFormats[0];
        }
    }
}