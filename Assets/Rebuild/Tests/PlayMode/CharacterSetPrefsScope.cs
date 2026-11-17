using Team5.Rebuild.Session;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    // Saves the player's stored character set and restores it, so tests never change the editor's choice.
    public sealed class CharacterSetPrefsScope : System.IDisposable
    {
        private readonly bool hadValue;
        private readonly string value;

        public CharacterSetPrefsScope(CharacterSet? pin = null)
        {
            hadValue = PlayerPrefs.HasKey(CharacterSetSelection.PrefsKey);
            value = PlayerPrefs.GetString(CharacterSetSelection.PrefsKey, "");
            if (pin.HasValue) CharacterSetSelection.Set(pin.Value);
        }

        public void Dispose()
        {
            if (hadValue) PlayerPrefs.SetString(CharacterSetSelection.PrefsKey, value);
            else PlayerPrefs.DeleteKey(CharacterSetSelection.PrefsKey);
            PlayerPrefs.Save();
        }
    }
}
