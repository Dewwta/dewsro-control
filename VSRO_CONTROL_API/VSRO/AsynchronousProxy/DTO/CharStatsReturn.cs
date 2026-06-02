using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Framework;

namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy.DTO
{
    public class CharStatsReturn
    {
        public uint PhyAtkMin { get; set; }
        public uint PhyAtkMax { get; set; }
        public uint MagAtkMin { get; set; }
        public uint MagAtkMax { get; set; }
        public ushort PhyDef { get; set; }
        public ushort MagDef { get; set; }
        public ushort HitRate { get; set; }
        public ushort ParryRate { get; set; }

        public uint MaxHP { get; set; }
        public uint MaxMP { get; set; }
        public ushort STR { get; set; }
        public ushort INT { get; set; }

    }
}
