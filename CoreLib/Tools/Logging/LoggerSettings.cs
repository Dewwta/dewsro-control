using System;
using System.Collections.Generic;
using System.Text;

namespace CoreLib.Tools.Logging
{
    public class LoggerSettings
    {
        public int? OldestLogAllowedInDays { get; set; }
        public int? SaveTimerInHours { get; set; } 
        public int? CleanupTimerInHours { get; set; }
        
        public static LoggerSettings GetDefault()
        {
            return new LoggerSettings
            {
                OldestLogAllowedInDays = 7,
                SaveTimerInHours = 24,
                CleanupTimerInHours = 48,
            };
        }


    }
}
