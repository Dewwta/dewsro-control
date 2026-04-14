using System;
using System.Collections.Generic;
using System.Text;

namespace CoreLib.Models
{
    public class ReturnedAccessObject
    {
        public string? Token { get; set; }
        public User? UserInfo { get; set; }
        public int ExpiresIn { get; set; }
    }
}
