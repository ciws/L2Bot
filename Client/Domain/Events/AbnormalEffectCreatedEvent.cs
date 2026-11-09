using Client.Domain.Entities;
namespace Client.Domain.Events
{
    public class AbnormalEffectCreatedEvent : EventInterface
    {
        public readonly AbnormalEffect Effect;
        public AbnormalEffectCreatedEvent(AbnormalEffect effect) { Effect = effect; }
    }
}
