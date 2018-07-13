namespace Ryujinx.Graphics.Gal
{
    public class GalBufferBindings
    {
        private const int ConstBuffersPerStage = 18;

        private long[][] Keys;

        public GalBufferBindings()
        {
            Keys = new long[5][];

            for (int i = 0; i < Keys.Length; i++)
            {
                Keys[i] = new long[ConstBuffersPerStage];
            }
        }

        public void Bind(GalShaderType ShaderType, int Cbuf, long Key)
        {
            Keys[(int)ShaderType][Cbuf] = Key;
        }

        public long Get(GalShaderType ShaderType, int Cbuf)
        {
            return Keys[(int)ShaderType][Cbuf];
        }
    }
}