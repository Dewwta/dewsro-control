using System;
using System.Collections.Generic;
using System.Text;

namespace CoreLib.Models
{
    /// <summary>
    /// DTO for task data. Used particularly in reading/writing from the database API.
    /// </summary>
    public class VTask 
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? DateOfActivity { get; set; }
        public string? TimeStarted { get; set; }
        public string? TimeEnded { get; set; }
        public string? Description { get; set; }
        public uint TotalHours { get; set; }
        public int LinkedUserId { get; set; }
        public string? TaskEntryEnterd { get; set; }
    }
}
