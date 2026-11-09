using Client.Domain.Common;

namespace Client.Domain.Entities
{
    public class AbnormalEffect : ObservableObject, EntityInterface
    {
        public uint Id { get; set; }
        public uint Level { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string IconName { get; set; } = "";
    }
}
