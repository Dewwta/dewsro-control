using System;
using System.Collections.Generic;
using System.Text;

namespace CoreLib.Models.Requests
{
    /// <summary>
    /// DTO for login requests from client.
    /// </summary>
    public class LoginRequest
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
