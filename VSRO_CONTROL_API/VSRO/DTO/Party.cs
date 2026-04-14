using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;

namespace VSRO_CONTROL_API.VSRO.DTO
{
    public class Party
    {
        public uint PartyID { get; set; }
        public List<Proxy> Members = new();
        public Proxy? Leader { get; set; }
        public string? Message;
    
    }
}
