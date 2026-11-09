using Client.Domain.Enums;
namespace Client.Domain.Entities
{
    public class EtcItem : BaseItem
    {
        public EtcItem(uint id, uint itemId, ItemTypeEnum type, string name, string iconName,
            string description, int mana, uint weight, uint amount, bool isQuest, bool isAutoused)
            : base(id, itemId, type, name, iconName, description, mana, weight)
        { Amount = amount; IsQuest = isQuest; IsAutoused = isAutoused; }
    }
}
