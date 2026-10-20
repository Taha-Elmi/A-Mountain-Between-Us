using System;
using System.Collections.Generic;
using UnityEngine;

namespace Farhad
{
    [Serializable]
    public struct Record
    {
        public int rockfalls;
        public float seconds;
    }

    // The player's best runs through the mountain, kept between runs: fewest rockfalls first, and
    // the quicker of two runs with as many.
    public static class Records
    {
        const string PrefsKey = "records";
        public const int Kept = 10;

        [Serializable]
        class Saved
        {
            public List<Record> records = new();
        }

        // Best first; empty if no run has reached the far side yet.
        public static List<Record> Best()
        {
            var json = PlayerPrefs.GetString(PrefsKey, "");
            var saved = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Saved>(json);
            var records = saved?.records ?? new List<Record>();
            records.Sort(Compare);
            return records;
        }

        public static void Add(int rockfalls, float seconds)
        {
            var records = Best();
            records.Add(new Record { rockfalls = rockfalls, seconds = seconds });
            records.Sort(Compare);
            if (records.Count > Kept) records.RemoveRange(Kept, records.Count - Kept);
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(new Saved { records = records }));
            PlayerPrefs.Save();
        }

        static int Compare(Record a, Record b) =>
            a.rockfalls != b.rockfalls ? a.rockfalls.CompareTo(b.rockfalls) : a.seconds.CompareTo(b.seconds);

        // m:ss, or h:mm:ss for a run of an hour or more.
        public static string Duration(float seconds)
        {
            var time = TimeSpan.FromSeconds(Mathf.Floor(seconds));
            return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time:mm\\:ss}" : $"{time.Minutes}:{time:ss}";
        }
    }
}
