namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy.Framework
{
    class PacketWriter : BinaryWriter
    {
        public PacketWriter()
        {
            this.m_ms = new MemoryStream();
            this.OutStream = this.m_ms;
        }

        public byte[] GetBytes()
        {
            return this.m_ms.ToArray();
        }

        private MemoryStream m_ms;
    }
}
