using Client.Domain.Entities;
namespace Client.Domain.Events
{
    public class AbnormalEffectDeletedEvent : EventInterface
    {
        public readonly uint Id;
        public AbnormalEffectDeletedEvent(uint id) { Id = id; }
    }
}
