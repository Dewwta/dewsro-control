using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Tracking;

namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy.DTO
{
    public class CosSpawnReturn
    {
        public uint PetUID { get; set; }
        public Pet? Pet { get; set; }
        public bool IsAttackPet { get; set; }
    }
}
