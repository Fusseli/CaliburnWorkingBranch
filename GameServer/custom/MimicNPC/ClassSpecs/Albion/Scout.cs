using DOL.GS.PlayerClass;
using static DOL.GS.WarMapMgr;
using System.Collections.Generic;
using DOL.GS.ServerProperties;

namespace DOL.GS.Scripts
{
    //public class MimicScout : MimicNPC
    //{
    //    public MimicScout(byte level) : base(new ClassScout(), level)
    //    { }
    //}

    public class ScoutSpec : MimicSpec
    {
        private static string BowSpec => Properties.ALLOW_OLD_ARCHERY ? Specs.Longbow : Specs.Archery;
        public ScoutSpec()
        {
            SpecName = "ScoutSpec";
            Is2H = false;

            int randBaseWeap = Util.Random(1);
            
            switch (randBaseWeap)
            {
                case 0: WeaponOneType = eObjectType.SlashingWeapon; break;
                case 1: WeaponOneType = eObjectType.ThrustWeapon; break;
            }

            int randVariance = Util.Random(3);

            switch (randVariance)
            {
                case 0:
                SpecType = eSpecType.OneHandAndShield;
                Add(ObjToSpec(WeaponOneType), 29, 0.6f);
                Add(BowSpec, 44, 0.8f);
                Add(Specs.Shields, 42, 0.7f);
                Add(Specs.Stealth, 35, 0.1f);
                break;

                case 1:
                SpecType = eSpecType.OneHandAndShield;
                Add(ObjToSpec(WeaponOneType), 29, 0.7f);
                Add(BowSpec, 50, 0.8f);
                Add(Specs.Shields, 35, 0.6f);
                Add(Specs.Stealth, 35, 0.1f);
                break;

                case 2:
                SpecType = eSpecType.OneHandAndShield;
                Add(ObjToSpec(WeaponOneType), 39, 0.7f);
                Add(BowSpec, 35, 0.8f);
                Add(Specs.Shields, 42, 0.6f);
                Add(Specs.Stealth, 36, 0.1f);
                break;

                case 3:
                SpecType = eSpecType.OneHandAndShield;
                Add(ObjToSpec(WeaponOneType), 44, 0.7f);
                Add(BowSpec, 35, 0.8f);
                Add(Specs.Shields, 35, 0.6f);
                Add(Specs.Stealth, 36, 0.1f);
                break;
            }
        }
    }
}
