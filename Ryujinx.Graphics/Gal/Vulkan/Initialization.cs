using OpenTK;
using OpenTK.Graphics.Vulkan;
using System;
using System.Collections.Generic;

namespace Ryujinx.Graphics.Gal.Vulkan
{
    using static VulkanHelper;

    public partial class VulkanRenderer : IGalRenderer
    {
        private static VulkanString ApplicationName = "Ryujinx";
        private static VulkanString EngineName = "Ryujinx";

        private int SurfaceWidth;
        private int SurfaceHeight;

        private VkInstance Instance;
        private VkSurfaceKHR Surface;
        private VkPhysicalDevice PhysicalDevice;
        private VkDevice Device;
        private VkQueue GraphicsQueue;
        private VkQueue PresentQueue;
        private VkSwapchainKHR SwapChain;
        private VulkanList<VkImage> SwapChainImages;
        private VkFormat SwapChainImageFormat;
        private VkExtent2D SwapChainExtent;
        private VulkanList<VkImageView> SwapChainImageViews;

        public unsafe void Initialize(NativeWindow Window)
        {
            SurfaceWidth  = Window.Width;
            SurfaceHeight = Window.Height;

            VK.LoadFunctions();

            CreateInstance(Window);
            CreateSurface(Window);
            PickPhysicalDevice();
            CreateLogicalDevice();
            CreateSwapChain();
            CreateImageViews();

            InitSubmodules();
        }

        public void Dispose()
        {
        }

        public void SwapBuffers()
        {
        }

        public void RecreateSwapchain(int Width, int Height)
        {
        }

        private void InitSubmodules()
        {
            Buffer = new VulkanConstBuffer();

            FrameBuffer = new VulkanFrameBuffer();

            Rasterizer = new VulkanRasterizer();

            Shader = new VulkanShader();

            Pipeline = new VulkanPipeline();

            Texture = new VulkanTexture();
        }

        private unsafe void CreateInstance(NativeWindow Window)
        {
            VkApplicationInfo AppInfo = new VkApplicationInfo()
            {
                sType = VkStructureType.ApplicationInfo,
                apiVersion = new VulkanVersion(1, 0, 0),
                pApplicationName = ApplicationName,
                pEngineName = EngineName
            };

            VulkanList<IntPtr> InstanceExtensions = new VulkanList<IntPtr>(4);

            InstanceExtensions.Add(VulkanStrings.VK_KHR_SURFACE_EXTENSION_NAME);

            foreach (IntPtr RequiredExtensions in Window.RequiredVulkanExtensions())
            {
                InstanceExtensions.Add(RequiredExtensions);
            }

            VkInstanceCreateInfo InstanceCreateInfo = new VkInstanceCreateInfo()
            {
                sType = VkStructureType.InstanceCreateInfo,
                pApplicationInfo = &AppInfo,
                enabledExtensionCount = InstanceExtensions.Count,
                ppEnabledExtensionNames = (byte**)InstanceExtensions.Data
            };

            Check(VK.CreateInstance(&InstanceCreateInfo, null, out Instance));
        }

        private void CreateSurface(NativeWindow Window)
        {
            Check(Window.CreateVulkanSurface(Instance, out Surface));
        }

        private unsafe void PickPhysicalDevice()
        {
            uint DeviceCount = 0;
            Check(VK.EnumeratePhysicalDevices(Instance, ref DeviceCount, IntPtr.Zero));

            if (DeviceCount == 0)
            {
                throw new NotSupportedException("Failed to find GPUs with Vulkan Support!");
            }

            VulkanList<VkPhysicalDevice> Devices = new VulkanList<VkPhysicalDevice>(DeviceCount);
            Check(VK.EnumeratePhysicalDevices(Instance, ref DeviceCount, Devices.Data));

            //Just choose the first device
            PhysicalDevice = Devices[0];
        }

        private unsafe void CreateLogicalDevice()
        {
            QueueFamilyIndices Indices = QueueFamilyIndices.Find(PhysicalDevice, Surface);

            HashSet<int> UniqueQueueFamilies = new HashSet<int>
            {
                Indices.GraphicsFamily,
                Indices.PresentFamily
            };

            VulkanList<VkDeviceQueueCreateInfo> QueueCreateInfos = new VulkanList<VkDeviceQueueCreateInfo>(2);

            foreach (int QueueFamily in UniqueQueueFamilies)
            {
                float QueuePriority = 1f;

                VkDeviceQueueCreateInfo QueueCreateInfo = new VkDeviceQueueCreateInfo
                {
                    sType = VkStructureType.DeviceQueueCreateInfo,
                    queueFamilyIndex = (uint)QueueFamily,
                    queueCount = 1,
                    pQueuePriorities = &QueuePriority
                };

                QueueCreateInfos.Add(QueueCreateInfo);
            }

            VkPhysicalDeviceFeatures DeviceFeatures = new VkPhysicalDeviceFeatures();

            VulkanList<IntPtr> DeviceExtensions = new VulkanList<IntPtr>
            {
                VulkanStrings.VK_KHR_SWAPCHAIN_EXTENSION_NAME
            };

            VkDeviceCreateInfo DeviceCreateInfo = new VkDeviceCreateInfo
            {
                sType = VkStructureType.DeviceCreateInfo,
                queueCreateInfoCount = QueueCreateInfos.Count,
                pQueueCreateInfos = (VkDeviceQueueCreateInfo*)QueueCreateInfos.Data,
                pEnabledFeatures = &DeviceFeatures,
                enabledExtensionCount = DeviceExtensions.Count,
                ppEnabledExtensionNames = (byte**)DeviceExtensions.Data,
            };

            Check(VK.CreateDevice(PhysicalDevice, &DeviceCreateInfo, null, out Device));

            VK.GetDeviceQueue(Device, (uint)Indices.GraphicsFamily, 0, out GraphicsQueue);
            VK.GetDeviceQueue(Device, (uint)Indices.PresentFamily, 0, out PresentQueue);
        }

        private unsafe void CreateSwapChain()
        {
            SwapChainSupportDetails SwapChainSupport = SwapChainSupportDetails.Query(PhysicalDevice, Surface);

            VkSurfaceFormatKHR SurfaceFormat = ChooseSwapSurfaceFormat(SwapChainSupport.Formats);
            VkPresentModeKHR PresentMode     = ChooseSwapPresentMode  (SwapChainSupport.PresentModes);
            VkExtent2D Extent                = ChooseSwapExtent       (SwapChainSupport.Capabilities);

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

            Check(VK.CreateSwapchainKHR(Device, &CreateInfo, IntPtr.Zero, out SwapChain));

            uint SwapChainImageCount = 0;
            Check(VK.GetSwapchainImagesKHR(Device, SwapChain, ref SwapChainImageCount, (VkImage*)null));

            SwapChainImages = new VulkanList<VkImage>(SwapChainImageCount, SwapChainImageCount);
            Check(VK.GetSwapchainImagesKHR(Device, SwapChain, ref SwapChainImageCount, (VkImage*)SwapChainImages.Data));

            SwapChainImageFormat = SurfaceFormat.format;
            SwapChainExtent = Extent;
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
                    width  = (uint)SurfaceWidth,
                    height = (uint)SurfaceHeight
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