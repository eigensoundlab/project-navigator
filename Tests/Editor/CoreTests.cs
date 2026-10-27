// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

namespace ProjectNavigator.Tests
{
    public class HistoryStackTests
    {
        [Test]
        public void Push_Back_Forward()
        {
            var h = new HistoryStack();
            Assert.IsTrue(h.Push("a", 10));
            Assert.IsTrue(h.Push("b", 10));
            Assert.IsTrue(h.Push("c", 10));
            Assert.AreEqual("c", h.Current);
            Assert.AreEqual("b", h.Back());
            Assert.AreEqual("a", h.Back());
            Assert.IsNull(h.Back());
            Assert.AreEqual("b", h.Forward());
            Assert.AreEqual("c", h.Forward());
            Assert.IsNull(h.Forward());
        }

        [Test]
        public void Push_SameAsCurrent_IsIgnored()
        {
            var h = new HistoryStack();
            h.Push("a", 10);
            Assert.IsFalse(h.Push("a", 10));
            Assert.AreEqual(1, h.Count);
        }

        [Test]
        public void Push_AfterBack_DropsForwardEntries()
        {
            var h = new HistoryStack();
            h.Push("a", 10);
            h.Push("b", 10);
            h.Push("c", 10);
            h.Back();
            h.Push("d", 10);
            Assert.AreEqual(3, h.Count);
            Assert.AreEqual("d", h.Current);
            Assert.IsFalse(h.CanGoForward);
            Assert.AreEqual("b", h.Back());
        }

        [Test]
        public void Trim_KeepsNewestEntriesAndValidCursor()
        {
            var h = new HistoryStack();
            for (int i = 0; i < 10; i++) h.Push("g" + i, 3);
            Assert.AreEqual(3, h.Count);
            Assert.AreEqual("g9", h.Current);
            Assert.AreEqual("g8", h.Back());
            Assert.AreEqual("g7", h.Back());
            Assert.IsNull(h.Back());
        }

        [Test]
        public void RemoveAll_KeepsCurrentAndCollapsesDuplicates()
        {
            var h = new HistoryStack();
            h.Push("a", 10);
            h.Push("x", 10);
            h.Push("a", 10);
            h.Push("c", 10);
            h.Back();                         // current = a
            h.RemoveAll(g => g == "x");       // a, a, c -> a, c
            Assert.AreEqual(2, h.Count);
            Assert.AreEqual("a", h.Current);
            Assert.AreEqual("c", h.Forward());
        }

        [Test]
        public void RemoveAll_CurrentRemoved_FallsBackToNewest()
        {
            var h = new HistoryStack();
            h.Push("a", 10);
            h.Push("b", 10);
            h.RemoveAll(g => g == "b");
            Assert.AreEqual("a", h.Current);
            Assert.IsFalse(h.CanGoForward);
        }
    }

    public class FrecencyTests
    {
        [Test]
        public void Decay_HalvesAfterOneHalfLife()
        {
            long now = DateTime.UtcNow.Ticks;
            long then = now - TimeSpan.TicksPerDay * 7;
            Assert.AreEqual(2.0, Frecency.Decayed(4.0, then, now, 7.0), 1e-6);
        }

        [Test]
        public void Bump_AddsOneToDecayedScore()
        {
            long now = DateTime.UtcNow.Ticks;
            Assert.AreEqual(1.0, Frecency.Bumped(0, 0, now, 7.0), 1e-9);
            Assert.AreEqual(3.0, Frecency.Bumped(2.0, now, now, 7.0), 1e-9);
        }

        [Test]
        public void RecentVisits_OutrankOldOnes()
        {
            long now = DateTime.UtcNow.Ticks;
            long monthAgo = now - TimeSpan.TicksPerDay * 30;
            double old = Frecency.Decayed(20.0, monthAgo, now, 7.0);
            double fresh = Frecency.Decayed(5.0, now, now, 7.0);
            Assert.Greater(fresh, old);
        }
    }

    public class FuzzyMatcherTests
    {
        [Test]
        public void Matches_Subsequence()
        {
            Assert.GreaterOrEqual(FuzzyMatcher.Score("crd", "Cards"), 0);
        }

        [Test]
        public void Rejects_WrongOrder()
        {
            Assert.AreEqual(-1, FuzzyMatcher.Score("dc", "Cards"));
        }

        [Test]
        public void WordStart_ScoresHigher()
        {
            Assert.Greater(FuzzyMatcher.Score("lev", "Levels"), FuzzyMatcher.Score("lev", "Sleeves"));
        }

        [Test]
        public void EmptyPattern_MatchesEverything()
        {
            Assert.AreEqual(0, FuzzyMatcher.Score("", "Anything"));
        }
    }

    public class LegacyAssetImporterTests
    {
        const string k_Sample = @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &1
MonoBehaviour:
  m_ObjectHideFlags: 61
  m_Script: {fileID: 11500000, guid: 1dd7b4a2a7b8e3f4c9d0e1f2a3b4c5d6, type: 3}
  m_Name: 
  version: 2
  defaultsCreated: 1
  tags:
  - id: yellow
    name: 'Cards: campaign'
    color: {r: 0.5, g: 0.25, b: 1, a: 1}
    inheritToChildren: 0
    tintFiles: 1
  pins:
  - guid: 11111111222222223333333344444444
    label: 'It''s'
    group: 
  notes: []
  rulesEnabled: 0
";

        [Test]
        public void ConvertsUnityYamlToJson()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pn_legacy_test.asset");
            System.IO.File.WriteAllText(path, k_Sample);
            try
            {
                var json = LegacyAssetImporter.ToJson(path);
                StringAssert.Contains("\"defaultsCreated\":true", json);
                StringAssert.Contains("\"rulesEnabled\":false", json);
                StringAssert.Contains("\"name\":\"Cards: campaign\"", json);
                StringAssert.Contains("\"inheritToChildren\":false", json);
                StringAssert.Contains("\"tintFiles\":true", json);
                StringAssert.Contains("\"r\":0.5", json);
                StringAssert.Contains("\"guid\":\"11111111222222223333333344444444\"", json); // all-digit GUID stays a string
                StringAssert.Contains("\"label\":\"It's\"", json);
                StringAssert.Contains("\"group\":\"\"", json);
                StringAssert.Contains("\"notes\":[]", json);
                StringAssert.DoesNotContain("m_Script", json);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }

    public class GlobMatcherTests
    {
        [Test]
        public void DoubleStarSlash_MatchesAnyDepth()
        {
            Assert.IsTrue(GlobMatcher.IsMatch("**/Resources", "Assets/Resources"));
            Assert.IsTrue(GlobMatcher.IsMatch("**/Resources", "Assets/TcgEngine/Resources"));
            Assert.IsFalse(GlobMatcher.IsMatch("**/Resources", "Assets/Resources/Cards"));
        }

        [Test]
        public void TrailingDoubleStar_MatchesSubtree()
        {
            Assert.IsTrue(GlobMatcher.IsMatch("Assets/Art/**", "Assets/Art/Props/Rocks"));
            Assert.IsFalse(GlobMatcher.IsMatch("Assets/Art/**", "Assets/Audio"));
        }

        [Test]
        public void SingleStar_DoesNotCrossSlash()
        {
            Assert.IsTrue(GlobMatcher.IsMatch("Assets/*/Scenes", "Assets/TcgEngine/Scenes"));
            Assert.IsFalse(GlobMatcher.IsMatch("Assets/*/Scenes", "Assets/A/B/Scenes"));
        }

        [Test]
        public void Matching_IsCaseInsensitive()
        {
            Assert.IsTrue(GlobMatcher.IsMatch("**/resources", "Assets/Resources"));
        }
    }
}
