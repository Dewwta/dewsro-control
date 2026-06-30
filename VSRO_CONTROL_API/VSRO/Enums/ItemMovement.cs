namespace VSRO_CONTROL_API.VSRO.Enums
{
    public enum ItemMovement : byte
    {
        InventoryToInventory = byte.MinValue,
        StorageToStorage = 0x01,
        InventoryToStorage = 0x02,
        StorageToInventory = 0x03,
        InventoryToExchange = 0x04,
        ExchangeToInventory = 0x05,
        GroundToInventory = 0x06,
        InventoryToGround = 0x07,
        ShopToInventory = 0x08,
        InventoryToShop = 0x09,
        InventoryGoldToGround = 0x0A,
        StorageGoldToInventory = 0x0B,
        InventoryGoldToStorage = 0x0C,
        InventoryGoldToExchange = 0x0D,
        GameServerToInventory = 0x0E,
        InventoryToGameServer = 0x0F,
        PetToPet = 0x10,
        GroundToPet = 0x11,
        ShopToTransport = 0x13,
        TransportToShop = 0x14,
        ItemMallToInventory = 0x18,
        PetToInventory = 0x1A,
        InventoryToPet = 0x1B,
        GroundToPetToInventory = 0x1C,
        GuildToGuild = 0x1D,
        InventoryToGuild = 0x1E,
        GuildToInventory = 0x1F,
        InventoryGoldToGuild = 0x20,
        GuildGoldToInventory = 0x21,
        ShopBuyBack = 0x22,
        AvatarToInventory = 0x23,
        InventoryToAvatar = 0x24,
        OpenMagicCube = 0x2A,
        ShopToInventoryCoin = 0x2B,
        InventoryToCube = 0x27,
        MagicCubeConsumed = 0x29
    }

}
