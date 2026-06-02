using VSRO_CONTROL_API.VSRO.DTO.VSRO_CONTROL_API.VSRO.DTO;

namespace VSRO_CONTROL_API.VSRO.DTO
{
    public class SR_Mastery
    {
        public uint ID { get; set; }
        public byte CurLevel { get; set; }
        public byte Mastery { get; set; }
        public List<SR_Skill?>? SkillList { get; set; }
        
    }
}
