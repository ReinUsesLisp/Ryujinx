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

        private VulkanSemaphores Semaphores;

        private VkInstance Instance;
        private VkSurfaceKHR Surface;
        private VkPhysicalDevice PhysicalDevice;
        private VkDevice Device;
        private VkQueue GraphicsQueue;
        private VkQueue PresentQueue;

        public unsafe void Initialize(NativeWindow Window)
        {
            VK.LoadFunctions();

            CreateInstance(Window);
            CreateSurface(Window);
            PickPhysicalDevice();
            CreateLogicalDevice();

            InitSubmodules();
        }

        public void Dispose()
        {
        }

        public void SwapBuffers()
        {
            (FrameBuffer as VulkanFrameBuffer).SwapBuffers();
        }

        private void InitSubmodules()
        {
            Semaphores = new VulkanSemaphores(Device);

            Buffer = new VulkanConstBuffer();

            FrameBuffer = new VulkanFrameBuffer(Semaphores, Surface, PhysicalDevice, Device, PresentQueue);

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

        private unsafe void CreateSurface(NativeWindow Window)
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
    }
}