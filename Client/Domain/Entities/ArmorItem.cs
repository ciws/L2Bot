namespace Client.Domain.Entities
{
    public class ArmorItem : BaseItem
    {
        public bool IsEquipped { get; set; }
        public uint EnchantLevel { get; set; }
        public uint PDefense { get; set; }
        public uint MDefense { get; set; }
        public int ArmorType { get; set; }
        public int CrystalType { get; set; }
        public string SetEffect { get; set; } = "";
        public string AddSetEffect { get; set; } = "";
        public string EnchantEffect { get; set; } = "";
        public override string FullDescription => $"{Description}\nEnchant +{EnchantLevel}; P. Def. {PDefense}; M. Def. {MDefense}";
    }
}
