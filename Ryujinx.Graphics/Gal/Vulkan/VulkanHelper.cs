using OpenTK.Graphics.Vulkan;
using System;

namespace Ryujinx.Graphics.Gal.Vulkan
{
    internal class VulkanException : Exception
    {
        public VulkanException(VkResult Result)
            : base(Result.ToString())
        {
        }
    }

    internal static class VulkanHelper
    {
        public static void Check(VkResult Result)
        {
            if (Result != VkResult.Success)
            {
                throw new VulkanException(Result);
            }
        }
    }
}
