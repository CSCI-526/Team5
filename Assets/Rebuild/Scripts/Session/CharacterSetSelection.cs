using UnityEngine;

namespace Team5.Rebuild.Session
{
    // The squad look the player picked on the "Choose your squad" screen. Only presentation reads it;
    // simulation, rosters and economy are identical for every set.
    public enum CharacterSet
    {
        PaintSquad,
        Catgirl
    }

    public static class CharacterSetSelection
    {
        public const string PrefsKey = "Team5.CharacterSet";
        public const CharacterSet Default = CharacterSet.PaintSquad;

        public static CharacterSet Current => Parse(PlayerPrefs.GetString(PrefsKey, ToValue(Default)));

        public static void Set(CharacterSet value)
        {
            PlayerPrefs.SetString(PrefsKey, ToValue(value));
            PlayerPrefs.Save();
        }

        // Stored values are the enum names ("PaintSquad", "Catgirl"). Anything else falls back to the default.
        public static string ToValue(CharacterSet value) => value == CharacterSet.Catgirl ? "Catgirl" : "PaintSquad";

        public static CharacterSet Parse(string value) => value switch
        {
            "PaintSquad" => CharacterSet.PaintSquad,
            "Catgirl" => CharacterSet.Catgirl,
            _ => Default
        };
    }
}
