using System;
using UnityEngine;

namespace Farhad
{
    public enum Lang { Persian, English }

    // The language the game's words are shown in. Persian unless the player picked another on the
    // menu's Language stone; the choice is remembered between runs.
    public static class Language
    {
        const string PrefsKey = "language";

        public static event Action Changed;

        public static Lang Current
        {
            get => (Lang)PlayerPrefs.GetInt(PrefsKey, (int)Lang.Persian);
            set
            {
                if (value == Current) return;
                PlayerPrefs.SetInt(PrefsKey, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static bool RightToLeft => Current == Lang.Persian;

        // One line in the chosen language, ready for Unity to draw: Persian joined and in drawing order.
        public static string Words(string english, string persian) =>
            RightToLeft ? PersianText.Visual(PersianText.Shape(persian)) : english;
    }
}
