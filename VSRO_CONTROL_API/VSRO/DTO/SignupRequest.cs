namespace VSRO_CONTROL_API.VSRO.DTO
{
    public class SignupRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Nickname { get; set; }
        public string? Sex { get; set; }
        public string? InviteCode { get; set; }
    }
}
