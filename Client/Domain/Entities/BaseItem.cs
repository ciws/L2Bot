using Client.Domain.Common;
using Client.Domain.Enums;

namespace Client.Domain.Entities
{
    public abstract class BaseItem : ObservableObject, ItemInterface
    {
        public uint Id { get; set; }
        public uint ItemId { get; set; }
        public ItemTypeEnum Type { get; set; }
        public string Name { get; set; } = "";
        public string IconName { get; set; } = "";
        public string Description { get; set; } = "";
        public int Mana { get; set; }
        public uint Weight { get; set; }
        public uint Amount { get; set; } = 1;
        public bool IsQuest { get; set; }
        public bool IsAutoused { get; set; }
        public virtual string FullDescription => Description;
        protected BaseItem() { }
        protected BaseItem(uint id, uint itemId, ItemTypeEnum type, string name, string iconName,
            string description, int mana, uint weight)
        { Id = id; ItemId = itemId; Type = type; Name = name; IconName = iconName; Description = description; Mana = mana; Weight = weight; }
        public void NotifyUpdated() => OnPropertyChanged("");
    }
}
