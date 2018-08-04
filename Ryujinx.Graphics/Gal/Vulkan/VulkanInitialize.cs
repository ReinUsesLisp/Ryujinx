using OpenTK;
using OpenTK.Graphics.Vulkan;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Ryujinx.Graphics.Gal.Vulkan
{
    using static VulkanHelper;

    public partial class VulkanRenderer : IGalRenderer
    {
        private static VulkanString ApplicationName = "Ryujinx";
        private static VulkanString EngineName = "Ryujinx";

        private static VulkanString LunarGValidationLayer = "VK_LAYER_LUNARG_standard_validation";

        private VulkanSynchronization Synchronization;

        private VkInstance Instance;
        private VkSurfaceKHR Surface;
        private VkPhysicalDevice PhysicalDevice;
        private VkDevice Device;
        private VkQueue GraphicsQueue;
        private VkQueue PresentQueue;
        private VkCommandPool CommandPool;

        public unsafe void Initialize(NativeWindow Window)
        {
            VK.LoadFunctions();

            CreateInstance(Window);
            CreateSurface(Window);
            PickPhysicalDevice();
            CreateLogicalDevice();
            CreateCommandPool();

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
            VulkanDeviceQuery DeviceQuery = new VulkanDeviceQuery(PhysicalDevice, Device);

            Synchronization = new VulkanSynchronization(Device, (uint)QueueFamilyIndices.Find(PhysicalDevice, Surface).GraphicsFamily);

            Buffer = new VulkanConstBuffer();

            FrameBuffer = new VulkanFrameBuffer(DeviceQuery, Synchronization, Surface, PhysicalDevice, Device, GraphicsQueue, PresentQueue, CommandPool);

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

#if DEBUG
            InstanceExtensions.Add(VulkanStrings.VK_EXT_DEBUG_REPORT_EXTENSION_NAME);
#endif

            VkInstanceCreateInfo InstanceCreateInfo = new VkInstanceCreateInfo()
            {
                sType = VkStructureType.InstanceCreateInfo,
                pApplicationInfo = &AppInfo,
                enabledExtensionCount = InstanceExtensions.Count,
                ppEnabledExtensionNames = (byte**)InstanceExtensions.Data
            };

#if DEBUG
            VulkanList<IntPtr> InstanceLayers = new VulkanList<IntPtr>()
            {
                LunarGValidationLayer
            };

            InstanceCreateInfo.enabledLayerCount = InstanceLayers.Count;
            InstanceCreateInfo.ppEnabledLayerNames = (byte**)InstanceLayers.Data;
#endif

            Check(VK.CreateInstance(&InstanceCreateInfo, null, out Instance));

#if DEBUG
            ReportCallbackDelegate = ReportCallback;

            CallbackCI = new VkDebugReportCallbackCreateInfoEXT()
            {
                sType = VkStructureType.DebugReportCallbackCreateInfoEXT,
                flags = VkDebugReportFlagsEXT.ErrorEXT | VkDebugReportFlagsEXT.WarningEXT,
                pfnCallback = Marshal.GetFunctionPointerForDelegate(ReportCallbackDelegate)
            };

            VulkanString CreateDebugReportCallbackEXT_str = "vkCreateDebugReportCallbackEXT";

            IntPtr Pointer = VK.GetInstanceProcAddr(Instance, CreateDebugReportCallbackEXT_str);

            DebugReportCallbackEXT DebugReportCallbackEXT = Marshal.GetDelegateForFunctionPointer<DebugReportCallbackEXT>(Pointer);

            VkDebugReportCallbackCreateInfoEXT _CallbackCI = CallbackCI;

            VkDebugReportCallbackEXT CallbackHandle;
            Check(DebugReportCallbackEXT(Instance, (IntPtr)(&_CallbackCI), IntPtr.Zero, &CallbackHandle));
#endif
        }

#if DEBUG
        private PFN_vkDebugReportCallbackEXT ReportCallbackDelegate;

        private VkDebugReportCallbackCreateInfoEXT CallbackCI;

        private unsafe delegate VkResult DebugReportCallbackEXT(
            VkInstance Instance,
            IntPtr CreateInfo,
            IntPtr Allocation,
            VkDebugReportCallbackEXT* CallbackHandle);

        private static unsafe uint ReportCallback(
            uint flags,
            VkDebugReportObjectTypeEXT objectType,
            ulong @object,
            UIntPtr location,
            int messageCode,
            byte* pLayerPrefix,
            byte* pMessage,
            void* pUserData)
        {
            int Length = 0;

            while (pMessage[Length++] != 0) ;

            VkDebugReportFlagsEXT Flags = (VkDebugReportFlagsEXT)flags;

            bool IsError = Flags.HasFlag(VkDebugReportFlagsEXT.ErrorEXT);

            Console.ForegroundColor = IsError ? ConsoleColor.Red : ConsoleColor.Cyan;

            Console.WriteLine(Encoding.UTF8.GetString(pMessage, Length));

            if (!Flags.HasFlag(VkDebugReportFlagsEXT.InformationEXT))
            {
                Console.ForegroundColor = IsError ? ConsoleColor.DarkRed : ConsoleColor.DarkCyan;

                string[] Backtrace = Environment.StackTrace.Split(Environment.NewLine);

                for (int i = 3; i < Backtrace.Length; i++)
                {
                    Console.WriteLine(Backtrace[i]);
                }

                Console.WriteLine();
            }

            Console.ResetColor();

            return 0;
        }
#endif

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

        private unsafe void CreateCommandPool()
        {
            VkCommandPoolCreateInfo PoolInfo = new VkCommandPoolCreateInfo
            {
                sType = VkStructureType.CommandPoolCreateInfo,
                queueFamilyIndex = (uint)QueueFamilyIndices.Find(PhysicalDevice, Surface).GraphicsFamily,
                flags = VkCommandPoolCreateFlags.ResetCommandBuffer
            };

            Check(VK.CreateCommandPool(Device, &PoolInfo, IntPtr.Zero, out CommandPool));
        }
    }
}