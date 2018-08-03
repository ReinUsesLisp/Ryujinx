using OpenTK.Graphics.Vulkan;

namespace Ryujinx.Graphics.Gal.Vulkan
{
    using static VulkanHelper;

    internal class SwapChainSupportDetails
    {
        public VkSurfaceCapabilitiesKHR Capabilities;

        public VulkanList<VkSurfaceFormatKHR> Formats = new VulkanList<VkSurfaceFormatKHR>();
        public VulkanList<VkPresentModeKHR> PresentModes = new VulkanList<VkPresentModeKHR>();

        public static unsafe SwapChainSupportDetails Query(VkPhysicalDevice PhysicalDevice, VkSurfaceKHR Surface)
        {
            SwapChainSupportDetails Details = new SwapChainSupportDetails();

            Check(VK.GetPhysicalDeviceSurfaceCapabilitiesKHR(PhysicalDevice, Surface, out Details.Capabilities));

            uint FormatCount = 0;
            Check(VK.GetPhysicalDeviceSurfaceFormatsKHR(PhysicalDevice, Surface, ref FormatCount, (VkSurfaceFormatKHR*)null));

            if (FormatCount != 0)
            {
                Details.Formats = new VulkanList<VkSurfaceFormatKHR>(FormatCount, FormatCount);
                Check(VK.GetPhysicalDeviceSurfaceFormatsKHR(PhysicalDevice, Surface, ref FormatCount, (VkSurfaceFormatKHR*)Details.Formats.Data));
            }

            uint PresentModeCount = 0;
            Check(VK.GetPhysicalDeviceSurfacePresentModesKHR(PhysicalDevice, Surface, ref PresentModeCount, (VkPresentModeKHR*)null));

            if (PresentModeCount != 0)
            {
                Details.PresentModes = new VulkanList<VkPresentModeKHR>(PresentModeCount, PresentModeCount);
                Check(VK.GetPhysicalDeviceSurfacePresentModesKHR(PhysicalDevice, Surface, ref PresentModeCount, (VkPresentModeKHR*)Details.PresentModes.Data));
            }

            return Details;
        }
    }
}