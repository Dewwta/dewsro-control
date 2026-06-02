namespace VSRO_CONTROL_API.VSRO.DTO
{
    namespace VSRO_CONTROL_API.VSRO.DTO
    {
        public class SR_Skill
        {
            // Identity
            public uint ID { get; set; }
            public string CodeName { get; set; } = "";
            public string Group { get; set; } = "";
            public byte Level { get; set; }
            public string ReadableName { get; set; } = "";

            // Chardata state
            public bool Enabled { get; set; }

            // Action timing
            public int PreparingTime { get; set; }
            public int CastingTime { get; set; }
            public int ActionDuration { get; set; }
            public int ReuseDelay { get; set; }
            public int CoolTime { get; set; }

            // Targeting
            public bool RequiresTarget { get; set; }
            public int Range { get; set; }
            public bool CanUseInTown { get; set; }
            public int AutoAttackType { get; set; }

            // Costs
            public int ConsumeHP { get; set; }
            public int ConsumeMP { get; set; }

            // AI
            public int AIAttackChance { get; set; }
            public int AISkillType { get; set; }

            // Full params
            public int[] Params { get; set; } = new int[50];


            public int MoveSpeedPercent { get; set; }

            public uint TimedJobId { get; set; }
        }
    }


}
