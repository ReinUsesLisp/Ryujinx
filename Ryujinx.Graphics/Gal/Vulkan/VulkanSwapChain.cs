using OpenTK.Graphics.Vulkan;
using System;

namespace Ryujinx.Graphics.Gal.Vulkan
{
    using static VulkanHelper;

    internal class VulkanSwapChain
    {
        private readonly VkPhysicalDevice PhysicalDevice;
        private readonly VkDevice         Device;
        private readonly VkSurfaceKHR     Surface;

        public VkFormat                  ColorFormat  { get; private set; }
        public VkColorSpaceKHR           ColorSpace   { get; private set; }
        public VkSwapchainKHR            SwapChain    { get; private set; } = VkSwapchainKHR.Null;
        public uint                      ImageCount   { get; private set; }
        public VulkanList<VkImage>       Images       { get; private set; }
        public VulkanList<VkImageView>   ImageViews   { get; private set; }
        public VulkanList<VkFramebuffer> Framebuffers { get; private set; }
        public VkRenderPass              RenderPass   { get; private set; }

        private int CurrentWidth;
        private int CurrentHeight;

        public VulkanSwapChain(
            VkPhysicalDevice PhysicalDevice,
            VkDevice         Device,
            VkSurfaceKHR     Surface)
        {
            this.PhysicalDevice = PhysicalDevice;
            this.Device         = Device;
            this.Surface        = Surface;
        }

        public void Create()
        {
            Create(CurrentWidth, CurrentHeight);
        }

        public unsafe void Create(int Width, int Height)
        {
            VK.DeviceWaitIdle(Device);

            SwapChainSupportDetails SwapChainSupport = SwapChainSupportDetails.Query(PhysicalDevice, Surface);

            VkSurfaceFormatKHR SurfaceFormat = ChooseSwapSurfaceFormat(SwapChainSupport.Formats); //<-- align me
            VkPresentModeKHR PresentMode = ChooseSwapPresentMode(SwapChainSupport.PresentModes);
            VkExtent2D Extent = ChooseSwapExtent(SwapChainSupport.Capabilities, Width, Height);

            Width  = CurrentWidth  = (int)Extent.width;
            Height = CurrentHeight = (int)Extent.height;

            uint DesiredImageCount = SwapChainSupport.Capabilities.minImageCount + 1;

            if (SwapChainSupport.Capabilities.maxImageCount > 0 &&
                DesiredImageCount > SwapChainSupport.Capabilities.maxImageCount)
            {
                DesiredImageCount = SwapChainSupport.Capabilities.maxImageCount;
            }

            VkSwapchainKHR OldSwapChain = this.SwapChain;

            VkSwapchainCreateInfoKHR SwapChainCI = new VkSwapchainCreateInfoKHR()
            {
                sType = VkStructureType.SwapchainCreateInfoKHR,
                surface = Surface,
                minImageCount = DesiredImageCount,
                imageFormat = SurfaceFormat.format,
                imageColorSpace = SurfaceFormat.colorSpace,
                imageExtent = Extent,
                imageArrayLayers = 1,
                imageUsage = VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferDst,
                preTransform = SwapChainSupport.Capabilities.currentTransform,
                compositeAlpha = VkCompositeAlphaFlagsKHR.OpaqueKHR,
                presentMode = PresentMode,
                clipped = true,
                oldSwapchain = OldSwapChain
            };

            QueueFamilyIndices Indices = QueueFamilyIndices.Find(PhysicalDevice, Surface);

            if (Indices.GraphicsFamily != Indices.PresentFamily)
            {
                VulkanList<uint> QueueFamilyIndices = new VulkanList<uint>()
                {
                    (uint)Indices.GraphicsFamily,
                    (uint)Indices.PresentFamily
                };

                SwapChainCI.imageSharingMode = VkSharingMode.Concurrent;
                SwapChainCI.queueFamilyIndexCount = 2;
                SwapChainCI.pQueueFamilyIndices = (uint*)QueueFamilyIndices.Data;
            }
            else
            {
                SwapChainCI.imageSharingMode = VkSharingMode.Exclusive;
            }

            Check(VK.CreateSwapchainKHR(Device, &SwapChainCI, IntPtr.Zero, out VkSwapchainKHR SwapChain));

            this.SwapChain = SwapChain;

            if (OldSwapChain != VkSwapchainKHR.Null)
            {
                foreach (VkImageView ImageView in ImageViews)
                {
                    VK.DestroyImageView(Device, ImageView, IntPtr.Zero);
                }

                foreach (VkFramebuffer Framebuffer in Framebuffers)
                {
                    VK.DestroyFramebuffer(Device, Framebuffer, IntPtr.Zero);
                }

                VK.DestroySwapchainKHR(Device, OldSwapChain, IntPtr.Zero);
            }

            uint ImageCount = 0;

            Check(VK.GetSwapchainImagesKHR(Device, SwapChain, ref ImageCount, (VkImage*)null));

            Images = VulkanList<VkImage>.New(ImageCount);

            Check(VK.GetSwapchainImagesKHR(Device, SwapChain, ref ImageCount, (VkImage*)Images.Data));

            this.ImageCount = ImageCount;

            ColorFormat = SurfaceFormat.format;

            //Create image views
            ImageViews = VulkanList<VkImageView>.New(ImageCount);

            for (int i = 0; i < ImageCount; i++)
            {
                VkImageViewCreateInfo ImageViewCI = new VkImageViewCreateInfo
                {
                    sType = VkStructureType.ImageViewCreateInfo,
                    image = Images[i],
                    viewType = VkImageViewType.Image2D,
                    format = ColorFormat,
                    components = new VkComponentMapping
                    {
                        r = VkComponentSwizzle.R,
                        g = VkComponentSwizzle.G,
                        b = VkComponentSwizzle.B,
                        a = VkComponentSwizzle.A
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

                Check(VK.CreateImageView(Device, &ImageViewCI, IntPtr.Zero, out ImageViews[i]));
            }

            //Create renderpass
            VkAttachmentDescription ColorAttachment = new VkAttachmentDescription()
            {
                format = ColorFormat,
                samples = VkSampleCountFlags.Count1,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                stencilLoadOp = VkAttachmentLoadOp.DontCare,
                stencilStoreOp = VkAttachmentStoreOp.DontCare,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.PresentSrcKHR
            };

            VkAttachmentReference ColorAttachmentReference = new VkAttachmentReference()
            {
                attachment = 0,
                layout = VkImageLayout.ColorAttachmentOptimal
            };

            VkSubpassDescription SubpassDescription = new VkSubpassDescription()
            {
                pipelineBindPoint = VkPipelineBindPoint.Graphics,
                colorAttachmentCount = 1,
                pColorAttachments = &ColorAttachmentReference
            };

            VkSubpassDependency Dependency = new VkSubpassDependency()
            {
                srcSubpass = VK.SubpassExternal,
                dstSubpass = 0,
                srcStageMask = VkPipelineStageFlags.ColorAttachmentOutput,
                srcAccessMask = 0,
                dstStageMask = VkPipelineStageFlags.ColorAttachmentOutput,
                dstAccessMask = VkAccessFlags.ColorAttachmentRead | VkAccessFlags.ColorAttachmentWrite
            };

            VkRenderPassCreateInfo RenderPassInfo = new VkRenderPassCreateInfo()
            {
                sType = VkStructureType.RenderPassCreateInfo,
                attachmentCount = 1,
                pAttachments = &ColorAttachment,
                subpassCount = 1,
                pSubpasses = &SubpassDescription,
                dependencyCount = 1,
                pDependencies = &Dependency
            };

            Check(VK.CreateRenderPass(Device, ref RenderPassInfo, IntPtr.Zero, out VkRenderPass RenderPass));

            this.RenderPass = RenderPass;

            //Create framebuffers
            Framebuffers = VulkanList<VkFramebuffer>.New(ImageCount);

            for (int i = 0; i < ImageCount; i++)
            {
                VkImageView Attachment = ImageViews[i];

                VkFramebufferCreateInfo FramebufferCI = new VkFramebufferCreateInfo()
                {
                    sType = VkStructureType.FramebufferCreateInfo,
                    renderPass = RenderPass,
                    attachmentCount = 1,
                    pAttachments = &Attachment,
                    width = (uint)Width,
                    height = (uint)Height,
                    layers = 1
                };

                Check(VK.CreateFramebuffer(Device, ref FramebufferCI, IntPtr.Zero, out Framebuffers[i]));
            }
        }

        public uint AcquireNextImage(VkSemaphore PresentCompleteSemaphore)
        {
            uint ImageIndex = 0;

            Check(VK.AcquireNextImageKHR(Device, SwapChain, ulong.MaxValue, PresentCompleteSemaphore, VkFence.Null, ref ImageIndex));

            return ImageIndex;
        }

        public unsafe void QueuePresent(VkQueue Queue, uint ImageIndex, VkSemaphore PresentSemaphore, VkSemaphore RenderSemaphore)
        {
            VkSwapchainKHR SwapChain = this.SwapChain;

            VkSemaphore* Semaphores = stackalloc VkSemaphore[2];
            Semaphores[0] = PresentSemaphore;
            Semaphores[1] = RenderSemaphore;

            VkPresentInfoKHR PresentInfo = new VkPresentInfoKHR()
            {
                sType = VkStructureType.PresentInfoKHR,
                swapchainCount = 1,
                pSwapchains = &SwapChain,
                pImageIndices = &ImageIndex,
                waitSemaphoreCount = RenderSemaphore != VkSemaphore.Null ? 2u : 1u,
                pWaitSemaphores = Semaphores
            };

            VkResult Result = VK.QueuePresentKHR(Queue, ref PresentInfo);

            if (Result == VkResult.ErrorOutOfDateKHR)
            {
                Create();
            }
            else
            {
                Check(Result);
            }
        }

        private unsafe VkExtent2D ChooseSwapExtent(VkSurfaceCapabilitiesKHR Capabilities, int Width, int Height)
        {
            if (Capabilities.currentExtent.width != UInt32.MaxValue)
            {
                return Capabilities.currentExtent;
            }
            else
            {
                return new VkExtent2D
                {
                    width = (uint)Width,
                    height = (uint)Height
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