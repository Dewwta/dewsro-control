using System;
using System.Collections.Generic;
using System.Text;

namespace CoreLib.Models.Requests
{
    /// <summary>
    /// DTO for registration requests from client.
    /// It is identical to LoginRequest in structure,
    /// but seperated for clarity.
    /// </summary>
    public class RegisterRequest
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? OTAC { get; set; }
    }
}
