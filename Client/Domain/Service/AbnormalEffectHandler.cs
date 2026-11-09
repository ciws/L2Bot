using Client.Domain.Entities;
using Client.Domain.Events;
using Client.Domain.Factories;

namespace Client.Domain.Service
{
    public class AbnormalEffectHandler : EntityHandler<AbnormalEffect>
    {
        private readonly EventBusInterface eventBus;
        public AbnormalEffectHandler(EntityFactoryInterface<AbnormalEffect> factory,
            EntityFactoryInterface<Entity> entityFactory, EventBusInterface eventBus) : base(factory, entityFactory)
        { this.eventBus = eventBus; }
        public override void OnCreate(AbnormalEffect entity) => eventBus.Publish(new AbnormalEffectCreatedEvent(entity));
        public override void OnUpdate(AbnormalEffect entity) => entity.OnPropertyChanged("");
        public override void OnDelete(AbnormalEffect entity) => eventBus.Publish(new AbnormalEffectDeletedEvent(entity.Id));
    }
}
