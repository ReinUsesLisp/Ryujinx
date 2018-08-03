using System.Collections.Generic;

namespace Ryujinx.Graphics.Gal.Vulkan
{
    internal class VulkanShader : IGalShader
    {
        public void Bind(long Key)
        {
            throw new System.NotImplementedException();
        }

        public void BindProgram()
        {
            throw new System.NotImplementedException();
        }

        public void Create(IGalMemory Memory, long Key, GalShaderType Type)
        {
            throw new System.NotImplementedException();
        }

        public void Create(IGalMemory Memory, long VpAPos, long Key, GalShaderType Type)
        {
            throw new System.NotImplementedException();
        }

        public void EnsureTextureBinding(string UniformName, int Value)
        {
            throw new System.NotImplementedException();
        }

        public IEnumerable<ShaderDeclInfo> GetTextureUsage(long Key)
        {
            throw new System.NotImplementedException();
        }

        public void Unbind(GalShaderType Type)
        {
            throw new System.NotImplementedException();
        }
    }
}