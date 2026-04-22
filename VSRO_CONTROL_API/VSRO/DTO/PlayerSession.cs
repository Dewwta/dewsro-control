namespace VSRO_CONTROL_API.VSRO.DTO
{
    public class PlayerSession
    {
        public string? CharacterName { get; set; }
        public string? AccountName { get; set; }
        public int JID { get; set; }
        public string? IP { get; set; }
        public DateTime LoginTime { get; set; }
        public DateTime LastActivity { get; set; }
        public TimeSpan AccumulatedPlayTime { get; set; }
        public bool IsAfk { get; set; }
        public int RewardedHours { get; set; }
        public Party? PlayerParty { get; set; } = null;
        public PlayerStats? PlayerStats { get; set; } = null;
        public uint ActivePetUID { get; set; }
        public uint CharacterUID { get; set; }  // spawn UID, changes per session
        public uint CharacterID { get; set; }   // persistent DB ID
        public uint SessionKills = 0; // kills in session
        public uint SessionUniqueKills = 0;
        public ulong CumulativeExp { get; set; } = 0;
        public byte? PendingLevelReward { get; set; } = null;
        public List<byte> UnclaimedRewards { get; set; } = new();
        public SemaphoreSlim AchievementLock { get; } = new SemaphoreSlim(1, 1);
        
    }
}
