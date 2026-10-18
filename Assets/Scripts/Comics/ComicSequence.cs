using System;
using System.Collections.Generic;
using UnityEngine;

namespace Farhad
{
    // One comic: its pages in reading order, and the words on them. Filled automatically from the
    // images in Assets/Art/Comics/<name>/ and the script Assets/Art/Comics/<name> Script.txt
    // (see ComicSetup); the per-page settings here survive refills.
    public class ComicSequence : ScriptableObject
    {
        [Serializable]
        public class Page
        {
            public Sprite image;
            [Tooltip("Seconds before the next line shows, or the page turns, by itself. 0 waits for the player.")]
            [Min(0f)] public float autoAdvance;
        }

        [Tooltip("Fills the screen around pages that aren't the screen's shape.")]
        public Color paper = Color.white;
        public List<Page> pages = new();
        [Tooltip("The words on each page, in Persian and English (see ComicScript).")]
        public TextAsset script;

        // The lines of each page, read from the script; pages without words have none.
        public List<List<ComicLine>> ReadLines()
        {
            var lines = ComicScript.Parse(script != null ? script.text : null);
            while (lines.Count < pages.Count) lines.Add(new List<ComicLine>());
            return lines;
        }
    }
}
