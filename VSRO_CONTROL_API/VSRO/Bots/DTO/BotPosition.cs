/// <summary>
/// Represents a position within the game world, including region, sector, and offset coordinates.
/// Dellta - 5/6/26
/// </summary>
/// <remarks>The BotPosition struct encapsulates both sector-based and world-based coordinates, allowing
/// conversion between in-game display coordinates and internal region/sector representations. It provides properties
/// for accessing sector indices, region identifiers, and world coordinates suitable for distance
/// calculations.</remarks>
public struct BotPosition
{
    public ushort RegionId { get; set; }
    public float XOffset { get; set; }
    public float YOffset { get; set; }
    public float ZOffset { get; set; }

    public static ushort CurrentDungeonRegionId = 0;
    // World coords for distance math
    public float X => (RegionId & 0x8000) != 0
        ? (128 * 192f) + (XOffset / 10f)
        : (SectorX - 135) * 192f + XOffset / 10f;

    public float Y => (RegionId & 0x8000) != 0
        ? (128 * 192f) + (YOffset / 10f)
        : (SectorY - 92) * 192f + YOffset / 10f;

    public byte SectorX => (byte)(RegionId & 0xFF);
    public byte SectorY => (byte)((RegionId >> 8) & 0xFF);
    public static BotPosition FromSession(VSRO_CONTROL_API.VSRO.DTO.ISession s) => new BotPosition
    {
        RegionId = (ushort)s.RegionId,
        XOffset = s.RawX,
        YOffset = s.RawY,
        ZOffset = s.Z
    };

    public static BotPosition FromSector(byte sectorX, byte sectorY, float xOff, float yOff, float z = 0)
        => new BotPosition
        {
            RegionId = (ushort)((sectorY << 8) | sectorX),
            XOffset = xOff,
            YOffset = yOff,
            ZOffset = z
        };

    /// <summary>
    /// Construct from in-game display coordinates
    /// </summary>
    public static BotPosition FromDisplayWorld(float worldX, float worldY, float z = 0)
    {
        bool isDungeon = CurrentDungeonRegionId != 0;

        if (isDungeon)
        {
            return new BotPosition
            {
                RegionId = CurrentDungeonRegionId,
                XOffset = (worldX - (128 * 192f)) * 10f,
                YOffset = (worldY - (128 * 192f)) * 10f,
                ZOffset = z
            };
        }

        byte sectorX = (byte)(worldX / 192f + 135f);
        byte sectorY = (byte)(worldY / 192f + 92f);

        return new BotPosition
        {
            RegionId = (ushort)((sectorY << 8) | sectorX),
            XOffset = (worldX - (sectorX - 135) * 192f) * 10f,
            YOffset = (worldY - (sectorY - 92f) * 192f) * 10f,
            ZOffset = z
        };
    }
    public static BotPosition FromDisplayWorld(float worldX, float worldY, float z, ushort regionId)
    {
        bool isDungeon = (regionId & 0x8000) != 0;

        if (isDungeon)
        {
            float rawX = (worldX - (128 * 192f)) * 10f;
            float rawY = (worldY - (128 * 192f)) * 10f;

            return new BotPosition
            {
                RegionId = regionId,
                XOffset = rawX,
                YOffset = rawY,
                ZOffset = z
            };
        }

        byte sectorX = (byte)(worldX / 192f + 135f);
        byte sectorY = (byte)(worldY / 192f + 92f);
        float rawX_ow = (worldX - (sectorX - 135) * 192f) * 10f;
        float rawY_ow = (worldY - (sectorY - 92f) * 192f) * 10f;

        return new BotPosition
        {
            RegionId = regionId,
            XOffset = rawX_ow,
            YOffset = rawY_ow,
            ZOffset = z
        };
    }

    public static BotPosition FromDisplayWorldDungeon(float worldX, float worldY, float z, ushort regionId)
    {
        float rawX = (worldX - (128 * 192f)) * 10f;
        float rawY = (worldY - (128 * 192f)) * 10f;

        return new BotPosition
        {
            RegionId = regionId,
            XOffset = rawX,
            YOffset = rawY,
            ZOffset = z
        };
    }

    /// <summary>
    /// Construct from raw sector bytes and offset values (used by network packet parsing).
    /// Auto-detects dungeon status from the high bit of constructed regionId.
    /// For dungeons, also returns recalculated sector indices (differs from input).
    /// For normal world, returned sectors match input sectors.
    /// </summary>
    public static (BotPosition position, byte outSectorX, byte outSectorY) FromRawOffsets(
        byte inputSectorX, byte inputSectorY, float rawX, float rawY, float z = 0)
    {
        ushort regionId = (ushort)((inputSectorY << 8) | inputSectorX);
        bool isDungeon = (regionId & 0x8000) != 0;
        
        byte outSX, outSY;

        if (isDungeon)
        {
            float worldX = (128 * 192f) + (rawX / 10f);
            float worldY = (128 * 192f) + (rawY / 10f);
            outSX = (byte)(worldX / 192);
            outSY = (byte)(worldY / 192);
        }
        else
        {
            outSX = inputSectorX;
            outSY = inputSectorY;
        }

        var pos = new BotPosition
        {
            RegionId = regionId,
            XOffset = rawX,
            YOffset = rawY,
            ZOffset = z
        };

        return (pos, outSX, outSY);
    }

}