namespace VSRO_CONTROL_API.VSRO.DTO
{
    public class UserDTO
    {
        public int JID { get; set; }
        public string? Username { get; set; }
        public string? HashedPassword { get; set; }
        public string? Status { get; set; }
        public string? Nickname { get; set; }
        public string? Email { get; set; }
        public string? Sex { get; set; }
        public int Authority { get; set; }
        public int totalPlaytimeMinutes { get; set; }
        

        public string GetPlaytime()
        {
            if (totalPlaytimeMinutes <= 0)
                return "0 Minutes";

            int days = totalPlaytimeMinutes / 1440;
            int hours = (totalPlaytimeMinutes % 1440) / 60;
            int minutes = totalPlaytimeMinutes % 60;

            var parts = new List<string>(3);

            if (days > 0)
                parts.Add(days == 1 ? "1 Day" : $"{days} Days");

            if (hours > 0)
                parts.Add(hours == 1 ? "1 Hour" : $"{hours} Hours");

            if (minutes > 0)
                parts.Add(minutes == 1 ? "1 Minute" : $"{minutes} Minutes");

            return string.Join(" ", parts);
        }

        public bool IsAuthoritive()
        {
            return Authority != 3;
        }

        public UserDTO ToSafeUser()
        {
            return new UserDTO
            {
                JID = JID,
                Username = Username,
                Nickname = Nickname,
                Email = Email,
                Sex = Sex,
                Authority = Authority,
            };
        }

    }
}
