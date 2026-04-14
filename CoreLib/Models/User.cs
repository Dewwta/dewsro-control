namespace CoreLib.Models
{
    /// <summary>
    /// User DTO for database access.
    /// </summary>
    public class User
    {
        public int Id { get; set; }
        public string? Username { get; set; }
        public string? PasswordHash { get; set; }
        public string? PasswordSalt { get; set; }
        public string? Email { get; set; }
        public string? GoogleSubject { get; set; }
        public bool IsGoogleLinked { get; set; }
        public bool IsAdmin { get; set; }

        public User ToSafeUser()
        {
            return new User
            {
                Id = Id,
                Username = Username,
                IsAdmin = IsAdmin,
                Email = Email,
                GoogleSubject = GoogleSubject,
                IsGoogleLinked = IsGoogleLinked,
            };
        }
    }
}
