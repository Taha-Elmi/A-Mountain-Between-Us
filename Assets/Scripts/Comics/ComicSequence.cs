using System;
using System.Collections.Generic;
using UnityEngine;

namespace Farhad
{
    // One comic: its pages in reading order. Filled automatically from the images in
    // Assets/Art/Comics/<name>/ (see ComicSetup); the per-page settings here survive refills.
    public class ComicSequence : ScriptableObject
    {
        [Serializable]
        public class Page
        {
            public Sprite image;
            [Tooltip("Seconds before the page turns by itself. 0 waits for the player.")]
            [Min(0f)] public float autoAdvance;
        }

        [Tooltip("Fills the screen around pages that aren't the screen's shape.")]
        public Color paper = Color.white;
        public List<Page> pages = new();
    }
}
