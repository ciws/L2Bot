namespace Client.Domain.Entities
{
    public class WeaponItem : BaseItem
    {
        public bool IsEquipped { get; set; }
        public uint EnchantLevel { get; set; }
        public uint PAttack { get; set; }
        public uint MAttack { get; set; }
        public uint Critical { get; set; }
        public int HitModify { get; set; }
        public uint AttackSpeed { get; set; }
        public int WeaponType { get; set; }
        public int CrystalType { get; set; }
        public uint RndDamage { get; set; }
        public uint MpConsume { get; set; }
        public uint SoulshotCount { get; set; }
        public uint SpiritshotCount { get; set; }
        public override string FullDescription => $"{Description}\nEnchant +{EnchantLevel}; P. Atk. {PAttack}; M. Atk. {MAttack}";
    }
}
