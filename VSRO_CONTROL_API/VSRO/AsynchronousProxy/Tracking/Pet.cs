using System.Collections.Concurrent;
using VSRO_CONTROL_API.VSRO.DTO;

namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy.Tracking
{
    public class Pet
    {
        public uint Uid { get; set; }

        // metadata
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public PetInfo Info { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        // inventory (optional)
        public ConcurrentDictionary<byte, (int ItemID, string CodeName, int Stack, int MaxStack)> Inventory { get; set; } = new();

        public bool IsAttackPet => Info?.IsAttackPet == true;
    }
}
