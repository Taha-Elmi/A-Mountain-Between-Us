using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Farhad
{
    // A page as it plays: its picture (none for a black page), its words, and how it comes in.
    public class ComicPage
    {
        public Sprite image;
        public float autoAdvance;
        public float fadeSeconds;
        public List<ComicLine> lines = new();
    }

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

        // The pages in the order they play. The script decides it: each of its pages shows the
        // image it names, black for (black), or else the image after the last one shown. Images
        // the script hasn't reached by its end follow, without words. No script: the images in
        // order.
        public List<ComicPage> ReadPages(List<string> problems = null)
        {
            var played = new List<ComicPage>();
            int next = 0;
            foreach (var written in ComicScript.Parse(script != null ? script.text : null, problems))
            {
                Page page = null;
                if (written.IsBlack) { }
                else if (!string.IsNullOrEmpty(written.image))
                {
                    int found = pages.FindIndex(p => p.image != null && Named(p.image, written.image));
                    if (found >= 0)
                    {
                        page = pages[found];
                        next = found + 1;
                    }
                    else problems?.Add($"Page {played.Count + 1}: there's no image called {written.image} in the comic's folder, so it shows black.");
                }
                else if (next < pages.Count) page = pages[next++];
                else problems?.Add($"Page {played.Count + 1} names no image and the images have run out, so it shows black. Write (black) if that's meant.");

                played.Add(new ComicPage
                {
                    image = page?.image,
                    autoAdvance = page?.autoAdvance ?? 0f,
                    fadeSeconds = written.fadeSeconds,
                    lines = written.lines,
                });
            }
            for (; next < pages.Count; next++)
                played.Add(new ComicPage { image = pages[next].image, autoAdvance = pages[next].autoAdvance });
            return played;
        }

        static bool Named(Sprite image, string name) =>
            string.Equals(image.name, Path.GetFileNameWithoutExtension(name), StringComparison.OrdinalIgnoreCase);
    }
}
