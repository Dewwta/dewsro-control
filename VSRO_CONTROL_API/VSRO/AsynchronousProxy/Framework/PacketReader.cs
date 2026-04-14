namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy.Framework
{
    class PacketReader : BinaryReader
    {
        public PacketReader(byte[] input) : base(new MemoryStream(input, false))
        {
            this.m_input = input;
        }

        public PacketReader(byte[] input, int index, int count) : base(new MemoryStream(input, index, count, false))
        {
            this.m_input = input;
        }

        private byte[] m_input;
    }
}
