using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using RhythmSlashRunner.Presentation;

namespace RhythmSlashRunner.Tests
{
    /// <summary>Round 34: every Japanese string in the game code has its English line (tools/l10n/en.py).</summary>
    public sealed class L10nTests
    {
        [System.Serializable] sealed class Entry { public string ja; public string en; }
        [System.Serializable] sealed class Table { public Entry[] entries; }

        [Test]
        public void EveryJapaneseStringInTheCodeHasAnEnglishLine()
        {
            var table = JsonUtility.FromJson<Table>(Resources.Load<TextAsset>("l10n_en").text);
            var known = new HashSet<string>();
            foreach (Entry e in table.entries) known.Add(e.ja);
            var literal = new Regex("\"((?:[^\"\\\\]|\\\\.)*)\"");
            var missing = new List<string>();
            foreach (string file in Directory.GetFiles(Path.Combine(Application.dataPath, "RhythmRunner/Runtime"), "*.cs", SearchOption.AllDirectories))
                foreach (string line in File.ReadAllLines(file))
                {
                    if (line.TrimStart().StartsWith("//")) continue;
                    foreach (Match m in literal.Matches(line))
                    {
                        string s = m.Groups[1].Value.Replace("\\n", "\n");
                        if (!L10n.HasJapanese(s) || s.StartsWith("0123456789")) continue; // the font warm-up glyph list
                        if (!known.Contains(s)) missing.Add(Path.GetFileName(file) + ": " + s);
                    }
                }
            Assert.IsEmpty(missing, "add these to tools/l10n/en.py:\n" + string.Join("\n", missing));
        }

        [Test]
        public void ConcatenatedLinesAreTranslatedFromTheirFragments()
        {
            string before = L10n.Language;
            L10n.Language = "en"; L10n.Reset();
            try
            {
                Assert.AreEqual("Shop", L10n.T("ショップ"));
                Assert.AreEqual("Best is 46 m away!", L10n.T("ベストまで あと 46 m！"));
                Assert.AreEqual("Plays 3/10  12:00 to +1", L10n.T("プレイ 3/10  あと12:00で+1"));
                Assert.IsFalse(L10n.HasJapanese(L10n.T("今日の目標「1回で300m走る」達成！ +20")));
            }
            finally { L10n.Language = before; L10n.Reset(); }
        }
    }
}
