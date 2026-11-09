namespace Client.Domain.Entities
{
    public class ShieldItem : BaseItem
    {
        public bool IsEquipped { get; set; }
        public uint EnchantLevel { get; set; }
        public uint PDefense { get; set; }
        public int Evasion { get; set; }
        public uint DefRate { get; set; }
        public int CrystalType { get; set; }
        public override string FullDescription => $"{Description}\nEnchant +{EnchantLevel}; P. Def. {PDefense}; Block {DefRate}";
    }
}
