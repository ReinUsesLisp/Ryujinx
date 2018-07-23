using System;
using System.Collections.Generic;

namespace Ryujinx.Graphics.Gal
{
    public interface IGalShader
    {
        void Create(IGalMemory Memory, long Key, GalShaderType Type);

        void Create(IGalMemory Memory, long VpAPos, long Key, GalShaderType Type);

        IEnumerable<ShaderDeclInfo> GetTextureUsage(long Key);

        void BindConstBuffers(GalBufferBindings BufferBindings);

        void EnsureTextureBinding(string UniformName, int Value);

        void SetFlip(float X, float Y);

        void Bind(long Key);

        void Unbind(GalShaderType Type);

        void BindProgram();

        void CreateBuffer(long Key, long DataSize);

        bool BufferCached(long Key, long DataSize);

        void SetData(long Key, long DataSize, IntPtr HostAddress);
    }
}