namespace VSRO_CONTROL_API.VSRO.DTO
{
    public class LoginResponseObject
    {
        public UserDTO? User { get; set; }
        public string? Jwt { get; set; }
    }
}
