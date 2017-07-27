﻿using Lucene.Net.Support;
using Lucene.Net.Util;
using NUnit.Framework;
using System;

namespace Lucene.Net.Analysis.Icu.Segmentation
{
    public class TestCharArrayIterator : LuceneTestCase
    {
        [Test]
        public void TestBasicUsage()
        {
            CharArrayIterator ci = new CharArrayIterator();
            ci.SetText("testing".toCharArray(), 0, "testing".Length);
            assertEquals(0, ci.BeginIndex);
            assertEquals(7, ci.EndIndex);
            assertEquals(0, ci.Index);
            assertEquals('t', ci.Current);
            assertEquals('e', ci.Next());
            assertEquals('g', ci.Last());
            assertEquals('n', ci.Previous());
            assertEquals('t', ci.First());
            assertEquals(CharacterIterator.DONE, ci.Previous());
        }

        [Test]
        public void TestFirst()
        {
            CharArrayIterator ci = new CharArrayIterator();
            ci.SetText("testing".toCharArray(), 0, "testing".Length);
            ci.Next();
            // Sets the position to getBeginIndex() and returns the character at that position. 
            assertEquals('t', ci.First());
            assertEquals(ci.BeginIndex, ci.Index);
            // or DONE if the text is empty
            ci.SetText(new char[] { }, 0, 0);
            assertEquals(CharacterIterator.DONE, ci.First());
        }

        [Test]
        public void TestLast()
        {
            CharArrayIterator ci = new CharArrayIterator();
            ci.SetText("testing".toCharArray(), 0, "testing".Length);
            // Sets the position to getEndIndex()-1 (getEndIndex() if the text is empty) 
            // and returns the character at that position. 
            assertEquals('g', ci.Last());
            assertEquals(ci.Index, ci.EndIndex - 1);
            // or DONE if the text is empty
            ci.SetText(new char[] { }, 0, 0);
            assertEquals(CharacterIterator.DONE, ci.Last());
            assertEquals(ci.EndIndex, ci.Index);
        }

        [Test]
        public void TestCurrent()
        {
            CharArrayIterator ci = new CharArrayIterator();
            // Gets the character at the current position (as returned by getIndex()). 
            ci.SetText("testing".toCharArray(), 0, "testing".Length);
            assertEquals('t', ci.Current);
            ci.Last();
            ci.Next();
            // or DONE if the current position is off the end of the text.
            assertEquals(CharacterIterator.DONE, ci.Current);
        }

        [Test]
        public void TestNext()
        {
            CharArrayIterator ci = new CharArrayIterator();
            ci.SetText("te".toCharArray(), 0, 2);
            // Increments the iterator's index by one and returns the character at the new index.
            assertEquals('e', ci.Next());
            assertEquals(1, ci.Index);
            // or DONE if the new position is off the end of the text range.
            assertEquals(CharacterIterator.DONE, ci.Next());
            assertEquals(ci.EndIndex, ci.Index);
        }

        [Test]
        public void TestSetIndex()
        {
            CharArrayIterator ci = new CharArrayIterator();
            ci.SetText("test".toCharArray(), 0, "test".Length);
            try
            {
                ci.SetIndex(5);
                fail();
            }
            catch (Exception e)
            {
                assertTrue(e is ArgumentException);
            }
        }

        [Test]
        public void TestClone()
        {
            char[] text = "testing".toCharArray();
            CharArrayIterator ci = new CharArrayIterator();
            ci.SetText(text, 0, text.Length);
            ci.Next();
            CharArrayIterator ci2 = (CharArrayIterator)ci.Clone();
            assertEquals(ci.Index, ci2.Index);
            assertEquals(ci.Next(), ci2.Next());
            assertEquals(ci.Last(), ci2.Last());
        }
    }
}
