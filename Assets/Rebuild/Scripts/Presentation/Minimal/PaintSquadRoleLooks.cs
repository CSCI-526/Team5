using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation
{
    public enum PaintSquadBaseModel { Soldier, SoldierHeavy, Hero }

    // Role look table for the Paint Squad player units. Helmet uses Primary, body uses Dark.
    public static class PaintSquadRoleLooks
    {
        public const float DarkValue = .65f;

        public static Color Primary(UnitRole role) => role switch
        {
            UnitRole.Tank => Hex(0xF59A1A),
            UnitRole.Heal => Hex(0x8DB52B),
            UnitRole.Captain => Hex(0xE8B923),
            UnitRole.Sniper => Hex(0x3F7FD0),
            UnitRole.Vanguard => Hex(0x229A8A),
            UnitRole.Demolisher => Hex(0x8A5A34),
            UnitRole.Frostweaver => Hex(0x7FD6F0),
            UnitRole.Warcaller => Hex(0xDB4793),
            UnitRole.Aegis => Hex(0x8F63B0),
            _ => Hex(0xDA4029), // Attack, and Test uses the Attack look.
        };

        // Same hue and saturation at about 65 percent of the primary's value.
        public static Color Dark(UnitRole role)
        {
            Color.RGBToHSV(Primary(role), out var h, out var s, out var v);
            return Color.HSVToRGB(h, s, v * DarkValue);
        }

        public static PaintSquadBaseModel BaseModel(UnitRole role) => role switch
        {
            UnitRole.Tank => PaintSquadBaseModel.SoldierHeavy,
            UnitRole.Captain => PaintSquadBaseModel.Hero,
            _ => PaintSquadBaseModel.Soldier,
        };

        private static Color Hex(int rgb) =>
            new Color32((byte)(rgb >> 16), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 255);
    }
}
