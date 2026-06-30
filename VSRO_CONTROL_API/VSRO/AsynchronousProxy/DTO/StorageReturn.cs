using System.Collections.Concurrent;
using VSRO_CONTROL_API.VSRO.DTO;

namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy.DTO
{
    public class StorageReturn
    {
        public ConcurrentDictionary<byte, SR_Item> Storage { get; } = new();
    }
}
