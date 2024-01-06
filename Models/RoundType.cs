using CounterStrikeSharp.API.Modules.Entities.Constants;

namespace CS2Multi1v1.Models;

// Weapons and various config for a single round-type

public struct RoundType
{
    public string Name;
    public CsItem? PrimaryWeapon;
    public CsItem? SecondaryWeapon;
    public bool UsePreferredPrimary;
    public bool UsePreferredSecondary;
    public bool Armour;
    public bool Helmet;

    public RoundType(string name, CsItem? primary, CsItem? secondary, bool usePreferredPrimary = false, bool usePreferredSecondary = false, bool armour = true, bool helmet = true)
    {
        Name = name;
        PrimaryWeapon = primary;
        SecondaryWeapon = secondary;
        UsePreferredPrimary = usePreferredPrimary;
        UsePreferredSecondary = usePreferredSecondary;
        Armour = armour;
        Helmet = helmet;
    }

    public static readonly RoundType RifleAk = new RoundType("AK47", CsItem.AK47, CsItem.Glock, true, false, true, true);

    public static readonly RoundType RifleM4 = new RoundType("M4A4", CsItem.M4A4, CsItem.USPS, true, false, true, true);

    public static readonly RoundType Pistol = new RoundType("USP", null, CsItem.USPS, false, true, true, false);

    public static readonly RoundType Scout = new RoundType("鸟狙", CsItem.Scout, CsItem.USPS, true, false, true, true);

    public static readonly RoundType Awp = new RoundType("大狙", CsItem.AWP, CsItem.Deagle, true, false, true, true);

    public static readonly RoundType Deagle = new RoundType("沙鹰", null, CsItem.Deagle, false, true, true, true);

    public static readonly RoundType GalilAR = new RoundType("咖喱", CsItem.GalilAR, CsItem.P250, true, false, true, true);

    public static readonly RoundType Revolver = new RoundType("R8", null, CsItem.Revolver, false, true, true, true);

    public static readonly RoundType Knife = new RoundType("刀", null, null, false, false, true, true);

    public static readonly RoundType Famas = new RoundType("法玛斯", CsItem.Famas, CsItem.Elite, true, false, true, true);

    public static readonly RoundType SG = new RoundType("553", CsItem.SG556, CsItem.Tec9, true, false, true, true);

    public static readonly RoundType AUG = new RoundType("AUG", CsItem.AUG, CsItem.CZ, true, false, true, true);
}
