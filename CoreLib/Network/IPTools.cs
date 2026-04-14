using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreLib.Network
{
    public static class IPTools
    {
        public static async Task<string> GetPublicIPAsync()
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            string[] services = {
                "https://api.ipify.org",
                "https://checkip.amazonaws.com",
                "https://icanhazip.com"
            };

            foreach (var url in services)
            {
                try
                {
                    var ip = (await client.GetStringAsync(url)).Trim();
                    if (System.Net.IPAddress.TryParse(ip, out _))
                        return ip;
                }
                catch { }
            }

            return null!;
        }
    }
}
