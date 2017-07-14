﻿// lucene version compatibility level: 4.8.1
using Lucene.Net.Analysis.Cn.Smart.HHMM;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Analysis.Util;
using Lucene.Net.Support;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Lucene.Net.Analysis.Cn.Smart
{
    /*
     * Licensed to the Apache Software Foundation (ASF) under one or more
     * contributor license agreements.  See the NOTICE file distributed with
     * this work for additional information regarding copyright ownership.
     * The ASF licenses this file to You under the Apache License, Version 2.0
     * (the "License"); you may not use this file except in compliance with
     * the License.  You may obtain a copy of the License at
     *
     *     http://www.apache.org/licenses/LICENSE-2.0
     *
     * Unless required by applicable law or agreed to in writing, software
     * distributed under the License is distributed on an "AS IS" BASIS,
     * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
     * See the License for the specific language governing permissions and
     * limitations under the License.
     */

    /// <summary>
    /// Tokenizer for Chinese or mixed Chinese-English text.
    /// <para/>
    /// The analyzer uses probabilistic knowledge to find the optimal word segmentation for Simplified Chinese text.
    /// The text is first broken into sentences, then each sentence is segmented into words.
    /// </summary>
    public class HMMChineseTokenizer : SegmentingTokenizerBase
    {
        ///** used for breaking the text into sentences */
        //private static readonly BreakIterator sentenceProto = BreakIterator.getSentenceInstance(Locale.ROOT);

        private readonly ICharTermAttribute termAtt;
        private readonly IOffsetAttribute offsetAtt;
        private readonly ITypeAttribute typeAtt;

        private readonly WordSegmenter wordSegmenter = new WordSegmenter();
        private IEnumerator<SegToken> tokens;

        /// <summary>
        /// Creates a new <see cref="HMMChineseTokenizer"/>
        /// </summary>
        public HMMChineseTokenizer(TextReader reader)
            : this(AttributeFactory.DEFAULT_ATTRIBUTE_FACTORY, reader)
        {
        }

        /// <summary>
        /// Creates a new <see cref="HMMChineseTokenizer"/>, supplying the <see cref="Lucene.Net.Util.AttributeSource.AttributeFactory"/>
        /// </summary>
        public HMMChineseTokenizer(AttributeFactory factory, TextReader reader)
            : base(factory, reader, new IcuBreakIterator(Icu.BreakIterator.UBreakIteratorType.SENTENCE, CultureInfo.InvariantCulture) { EnableHacks = false })
        {
            termAtt = AddAttribute<ICharTermAttribute>();
            offsetAtt = AddAttribute<IOffsetAttribute>();
            typeAtt = AddAttribute<ITypeAttribute>();
        }

        protected override void SetNextSentence(int sentenceStart, int sentenceEnd)
        {
            string sentence = new string(m_buffer, sentenceStart, sentenceEnd - sentenceStart);
            tokens = wordSegmenter.SegmentSentence(sentence, m_offset + sentenceStart).GetEnumerator();
        }

        protected override bool IncrementWord()
        {
            if (tokens == null || !tokens.MoveNext())
            {
                return false;
            }
            else
            {
                SegToken token = tokens.Current;
                ClearAttributes();
                termAtt.CopyBuffer(token.CharArray, 0, token.CharArray.Length);
                offsetAtt.SetOffset(CorrectOffset(token.StartOffset), CorrectOffset(token.EndOffset));
                typeAtt.Type = "word";
                return true;
            }
        }

        public override void Reset()
        {
            base.Reset();
            tokens = null;
        }
    }
}
