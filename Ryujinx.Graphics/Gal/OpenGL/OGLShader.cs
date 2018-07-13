using OpenTK.Graphics.OpenGL;
using Ryujinx.Graphics.Gal.Shader;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Ryujinx.Graphics.Gal.OpenGL
{
    public class OGLShader : IGalShader
    {
        private class ShaderStage : IDisposable
        {
            public int Handle { get; private set; }

            public bool IsCompiled { get; private set; }

            public GalShaderType Type { get; private set; }

            public string Code { get; private set; }

            public IEnumerable<ShaderDeclInfo> TextureUsage { get; private set; }
            public IEnumerable<ShaderDeclInfo> UniformUsage { get; private set; }

            public ShaderStage(
                GalShaderType               Type,
                string                      Code,
                IEnumerable<ShaderDeclInfo> TextureUsage,
                IEnumerable<ShaderDeclInfo> UniformUsage)
            {
                this.Type         = Type;
                this.Code         = Code;
                this.TextureUsage = TextureUsage;
                this.UniformUsage = UniformUsage;
            }

            public void Compile()
            {
                if (Handle == 0)
                {
                    Handle = GL.CreateShader(OGLEnumConverter.GetShaderType(Type));

                    CompileAndCheck(Handle, Code);
                }
            }

            public void Dispose()
            {
                Dispose(true);
            }

            protected virtual void Dispose(bool Disposing)
            {
                if (Disposing && Handle != 0)
                {
                    GL.DeleteShader(Handle);

                    Handle = 0;
                }
            }
        }

        private struct ShaderProgram
        {
            public ShaderStage Vertex;
            public ShaderStage TessControl;
            public ShaderStage TessEvaluation;
            public ShaderStage Geometry;
            public ShaderStage Fragment;
        }

        const int ConstBuffersPerStage = 18;

        private ShaderProgram Current;

        private ConcurrentDictionary<long, ShaderStage> Stages;

        private Dictionary<ShaderProgram, int> Programs;

        public int CurrentProgramHandle { get; private set; }

        private OGLCachedResource<OGLStreamBuffer> BufferCache;

        public OGLShader()
        {
            Stages = new ConcurrentDictionary<long, ShaderStage>();

            Programs = new Dictionary<ShaderProgram, int>();

            BufferCache = new OGLCachedResource<OGLStreamBuffer>((OGLStreamBuffer Buffer) => Buffer.Dispose());
        }

        public void Create(IGalMemory Memory, long Key, GalShaderType Type)
        {
            Stages.GetOrAdd(Key, (Stage) => ShaderStageFactory(Memory, Key, 0, false, Type));
        }

        public void Create(IGalMemory Memory, long VpAPos, long Key, GalShaderType Type)
        {
            Stages.GetOrAdd(Key, (Stage) => ShaderStageFactory(Memory, VpAPos, Key, true, Type));
        }

        private ShaderStage ShaderStageFactory(
            IGalMemory    Memory,
            long          Position,
            long          PositionB,
            bool          IsDualVp,
            GalShaderType Type)
        {
            GlslProgram Program;

            GlslDecompiler Decompiler = new GlslDecompiler();

            if (IsDualVp)
            {
                ShaderDumper.Dump(Memory, Position  + 0x50, Type, "a");
                ShaderDumper.Dump(Memory, PositionB + 0x50, Type, "b");

                Program = Decompiler.Decompile(
                    Memory,
                    Position  + 0x50,
                    PositionB + 0x50,
                    Type);
            }
            else
            {
                ShaderDumper.Dump(Memory, Position + 0x50, Type);

                Program = Decompiler.Decompile(Memory, Position + 0x50, Type);
            }

            return new ShaderStage(
                Type,
                Program.Code,
                Program.Textures,
                Program.Uniforms);
        }

        public IEnumerable<ShaderDeclInfo> GetTextureUsage(long Key)
        {
            if (Stages.TryGetValue(Key, out ShaderStage Stage))
            {
                return Stage.TextureUsage;
            }

            return Enumerable.Empty<ShaderDeclInfo>();
        }

        public void BindConstBuffers(GalBufferBindings BufferBindings)
        {
            int FreeBinding = 0;

            void BindIfNotNull(ShaderStage Stage)
            {
                if (Stage != null)
                {
                    foreach (ShaderDeclInfo DeclInfo in Stage.UniformUsage)
                    {
                        if (BufferCache.TryGetValue(BufferBindings.Get(Stage.Type, DeclInfo.Cbuf), out OGLStreamBuffer Buffer))
                        {
                            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, FreeBinding, Buffer.Handle);
                        }

                        FreeBinding++;
                    }
                }
            }

            BindIfNotNull(Current.Vertex);
            BindIfNotNull(Current.TessControl);
            BindIfNotNull(Current.TessEvaluation);
            BindIfNotNull(Current.Geometry);
            BindIfNotNull(Current.Fragment);
        }

        public void EnsureTextureBinding(string UniformName, int Value)
        {
            BindProgram();

            int Location = GL.GetUniformLocation(CurrentProgramHandle, UniformName);

            GL.Uniform1(Location, Value);
        }

        public void SetFlip(float X, float Y)
        {
            BindProgram();

            int Location = GL.GetUniformLocation(CurrentProgramHandle, GlslDecl.FlipUniformName);

            GL.Uniform2(Location, X, Y);
        }

        public void Bind(long Key)
        {
            if (Stages.TryGetValue(Key, out ShaderStage Stage))
            {
                Bind(Stage);
            }
        }

        private void Bind(ShaderStage Stage)
        {
            switch (Stage.Type)
            {
                case GalShaderType.Vertex:         Current.Vertex         = Stage; break;
                case GalShaderType.TessControl:    Current.TessControl    = Stage; break;
                case GalShaderType.TessEvaluation: Current.TessEvaluation = Stage; break;
                case GalShaderType.Geometry:       Current.Geometry       = Stage; break;
                case GalShaderType.Fragment:       Current.Fragment       = Stage; break;
            }
        }

        public void Unbind(GalShaderType Type)
        {
            switch (Type)
            {
                case GalShaderType.Vertex:         Current.Vertex         = null; break;
                case GalShaderType.TessControl:    Current.TessControl    = null; break;
                case GalShaderType.TessEvaluation: Current.TessEvaluation = null; break;
                case GalShaderType.Geometry:       Current.Geometry       = null; break;
                case GalShaderType.Fragment:       Current.Fragment       = null; break;
            }
        }

        public void BindProgram()
        {
            if (Current.Vertex   == null ||
                Current.Fragment == null)
            {
                return;
            }

            if (!Programs.TryGetValue(Current, out int Handle))
            {
                Handle = GL.CreateProgram();

                AttachIfNotNull(Handle, Current.Vertex);
                AttachIfNotNull(Handle, Current.TessControl);
                AttachIfNotNull(Handle, Current.TessEvaluation);
                AttachIfNotNull(Handle, Current.Geometry);
                AttachIfNotNull(Handle, Current.Fragment);

                GL.LinkProgram(Handle);

                CheckProgramLink(Handle);

                BindUniformBlocks(Handle);

                Programs.Add(Current, Handle);
            }

            GL.UseProgram(Handle);

            CurrentProgramHandle = Handle;
        }

        public void CreateBuffer(long Key, long DataSize)
        {
            OGLStreamBuffer Buffer = OGLStreamBuffer.Create(BufferTarget.UniformBuffer, DataSize);

            BufferCache.AddOrUpdate(Key, Buffer, DataSize);
        }

        public bool BufferCached(long Key, long DataSize)
        {
            if (BufferCache.TryGetSize(Key, out long Size))
            {
                return Size == DataSize;
            }

            return false;
        }

        public IntPtr MapBuffer(long Key, long DataSize)
        {
            if (!BufferCache.TryGetValue(Key, out OGLStreamBuffer Buffer))
            {
                throw new InvalidOperationException();
            }

            return Buffer.Map(DataSize);
        }

        public void UnmapBuffer(long Key, long DataSize)
        {
            if (!BufferCache.TryGetValue(Key, out OGLStreamBuffer Buffer))
            {
                throw new InvalidOperationException();
            }

            Buffer.Unmap(DataSize);
        }

        private void AttachIfNotNull(int ProgramHandle, ShaderStage Stage)
        {
            if (Stage != null)
            {
                Stage.Compile();

                GL.AttachShader(ProgramHandle, Stage.Handle);
            }
        }

        private void BindUniformBlocks(int ProgramHandle)
        {
            int FreeBinding = 0;

            int BindUniformBlocksIfNotNull(ShaderStage Stage)
            {
                if (Stage != null)
                {
                    foreach (ShaderDeclInfo DeclInfo in Stage.UniformUsage)
                    {
                        int BlockIndex = GL.GetUniformBlockIndex(ProgramHandle, DeclInfo.Name);

                        if (BlockIndex < 0)
                        {
                            //It is expected that its found, if it's not then driver might be in a malfunction
                            throw new InvalidOperationException();
                        }

                        GL.UniformBlockBinding(ProgramHandle, BlockIndex, FreeBinding);

                        FreeBinding++;
                    }
                }

                return FreeBinding;
            }

            BindUniformBlocksIfNotNull(Current.Vertex);
            BindUniformBlocksIfNotNull(Current.TessControl);
            BindUniformBlocksIfNotNull(Current.TessEvaluation);
            BindUniformBlocksIfNotNull(Current.Geometry);
            BindUniformBlocksIfNotNull(Current.Fragment);
        }

        public static void CompileAndCheck(int Handle, string Code)
        {
            GL.ShaderSource(Handle, Code);
            GL.CompileShader(Handle);

            CheckCompilation(Handle);
        }

        private static void CheckCompilation(int Handle)
        {
            int Status = 0;

            GL.GetShader(Handle, ShaderParameter.CompileStatus, out Status);

            if (Status == 0)
            {
                throw new ShaderException(GL.GetShaderInfoLog(Handle));
            }
        }

        private static void CheckProgramLink(int Handle)
        {
            int Status = 0;

            GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out Status);

            if (Status == 0)
            {
                throw new ShaderException(GL.GetProgramInfoLog(Handle));
            }
        }
    }
}