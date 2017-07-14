﻿#if FEATURE_BREAKITERATOR
using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Spans;
using Lucene.Net.Store;
using Lucene.Net.Support;
using Lucene.Net.Util;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IndexOptions = Lucene.Net.Index.IndexOptions;

namespace Lucene.Net.Search.PostingsHighlight
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
    /// Some tests that override <see cref="PostingsHighlighter.GetIndexAnalyzer(string)"/> to
    /// highlight wilcard, fuzzy, etc queries.
    /// </summary>
    [SuppressCodecs("MockFixedIntBlock", "MockVariableIntBlock", "MockSep", "MockRandom", "Lucene3x")]
    public class TestMultiTermHighlighting : LuceneTestCase
    {
        internal class PostingsHighlighterAnalyzerHelper : PostingsHighlighter
        {
            private readonly Analyzer analyzer;

            public PostingsHighlighterAnalyzerHelper(Analyzer analyzer)
            {
                this.analyzer = analyzer;
            }

            protected override Analyzer GetIndexAnalyzer(string field)
            {
                return analyzer;
            }
        }

        [Test]
        public void TestWildcards()
        {
            Directory dir = NewDirectory();
            // use simpleanalyzer for more natural tokenization (else "test." is a token)
            Analyzer analyzer = new MockAnalyzer(Random(), MockTokenizer.SIMPLE, true);
            IndexWriterConfig iwc = NewIndexWriterConfig(TEST_VERSION_CURRENT, analyzer);
            iwc.SetMergePolicy(NewLogMergePolicy());
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir, iwc);

            FieldType offsetsType = new FieldType(TextField.TYPE_STORED);
            offsetsType.IndexOptions = (IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS);
            Field body = new Field("body", "", offsetsType);
            Document doc = new Document();
            doc.Add(body);

            body.SetStringValue("This is a test.");
            iw.AddDocument(doc);
            body.SetStringValue("Test a one sentence document.");
            iw.AddDocument(doc);

            IndexReader ir = iw.Reader;
            iw.Dispose();

            IndexSearcher searcher = NewSearcher(ir);
            PostingsHighlighter highlighter = new PostingsHighlighterAnalyzerHelper(analyzer);
            //    PostingsHighlighter highlighter = new PostingsHighlighter() {
            //  @Override
            //  protected Analyzer getIndexAnalyzer(String field)
            //    {
            //        return analyzer;
            //    }
            //};
            Query query = new WildcardQuery(new Term("body", "te*"));
            TopDocs topDocs = searcher.Search(query, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            String[] snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a <b>test</b>.", snippets[0]);
            assertEquals("<b>Test</b> a one sentence document.", snippets[1]);

            // wrong field
            BooleanQuery bq = new BooleanQuery();
            bq.Add(new MatchAllDocsQuery(), Occur.SHOULD);
            bq.Add(new WildcardQuery(new Term("bogus", "te*")), Occur.SHOULD);
            topDocs = searcher.Search(bq, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            snippets = highlighter.Highlight("body", bq, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a test.", snippets[0]);
            assertEquals("Test a one sentence document.", snippets[1]);

            ir.Dispose();
            dir.Dispose();
        }

        [Test]
        public void TestOnePrefix()
        {
            Directory dir = NewDirectory();
            // use simpleanalyzer for more natural tokenization (else "test." is a token)
            Analyzer analyzer = new MockAnalyzer(Random(), MockTokenizer.SIMPLE, true);
            IndexWriterConfig iwc = NewIndexWriterConfig(TEST_VERSION_CURRENT, analyzer);
            iwc.SetMergePolicy(NewLogMergePolicy());
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir, iwc);

            FieldType offsetsType = new FieldType(TextField.TYPE_STORED);
            offsetsType.IndexOptions = (IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS);
            Field body = new Field("body", "", offsetsType);
            Document doc = new Document();
            doc.Add(body);

            body.SetStringValue("This is a test.");
            iw.AddDocument(doc);
            body.SetStringValue("Test a one sentence document.");
            iw.AddDocument(doc);

            IndexReader ir = iw.Reader;
            iw.Dispose();

            IndexSearcher searcher = NewSearcher(ir);
            PostingsHighlighter highlighter = new PostingsHighlighterAnalyzerHelper(analyzer);
            //PostingsHighlighter highlighter = new PostingsHighlighter() {
            //      @Override
            //      protected Analyzer getIndexAnalyzer(String field)
            //{
            //    return analyzer;
            //}
            //    };
            Query query = new PrefixQuery(new Term("body", "te"));
            TopDocs topDocs = searcher.Search(query, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            String[] snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a <b>test</b>.", snippets[0]);
            assertEquals("<b>Test</b> a one sentence document.", snippets[1]);

            // wrong field
            BooleanQuery bq = new BooleanQuery();
            bq.Add(new MatchAllDocsQuery(), Occur.SHOULD);
            bq.Add(new PrefixQuery(new Term("bogus", "te")), Occur.SHOULD);
            topDocs = searcher.Search(bq, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            snippets = highlighter.Highlight("body", bq, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a test.", snippets[0]);
            assertEquals("Test a one sentence document.", snippets[1]);

            ir.Dispose();
            dir.Dispose();
        }

        [Test]
        public void TestOneRegexp()
        {
            Directory dir = NewDirectory();
            // use simpleanalyzer for more natural tokenization (else "test." is a token)
            Analyzer analyzer = new MockAnalyzer(Random(), MockTokenizer.SIMPLE, true);
            IndexWriterConfig iwc = NewIndexWriterConfig(TEST_VERSION_CURRENT, analyzer);
            iwc.SetMergePolicy(NewLogMergePolicy());
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir, iwc);

            FieldType offsetsType = new FieldType(TextField.TYPE_STORED);
            offsetsType.IndexOptions = (IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS);
            Field body = new Field("body", "", offsetsType);
            Document doc = new Document();
            doc.Add(body);

            body.SetStringValue("This is a test.");
            iw.AddDocument(doc);
            body.SetStringValue("Test a one sentence document.");
            iw.AddDocument(doc);

            IndexReader ir = iw.Reader;
            iw.Dispose();

            IndexSearcher searcher = NewSearcher(ir);
            PostingsHighlighter highlighter = new PostingsHighlighterAnalyzerHelper(analyzer);
            //PostingsHighlighter highlighter = new PostingsHighlighter() {
            //      @Override
            //      protected Analyzer getIndexAnalyzer(String field)
            //{
            //    return analyzer;
            //}
            //    };
            Query query = new RegexpQuery(new Term("body", "te.*"));
            TopDocs topDocs = searcher.Search(query, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            String[] snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a <b>test</b>.", snippets[0]);
            assertEquals("<b>Test</b> a one sentence document.", snippets[1]);

            // wrong field
            BooleanQuery bq = new BooleanQuery();
            bq.Add(new MatchAllDocsQuery(), Occur.SHOULD);
            bq.Add(new RegexpQuery(new Term("bogus", "te.*")), Occur.SHOULD);
            topDocs = searcher.Search(bq, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            snippets = highlighter.Highlight("body", bq, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a test.", snippets[0]);
            assertEquals("Test a one sentence document.", snippets[1]);

            ir.Dispose();
            dir.Dispose();
        }

        [Test]
        public void TestOneFuzzy()
        {
            Directory dir = NewDirectory();
            // use simpleanalyzer for more natural tokenization (else "test." is a token)
            Analyzer analyzer = new MockAnalyzer(Random(), MockTokenizer.SIMPLE, true);
            IndexWriterConfig iwc = NewIndexWriterConfig(TEST_VERSION_CURRENT, analyzer);
            iwc.SetMergePolicy(NewLogMergePolicy());
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir, iwc);

            FieldType offsetsType = new FieldType(TextField.TYPE_STORED);
            offsetsType.IndexOptions = (IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS);
            Field body = new Field("body", "", offsetsType);
            Document doc = new Document();
            doc.Add(body);

            body.SetStringValue("This is a test.");
            iw.AddDocument(doc);
            body.SetStringValue("Test a one sentence document.");
            iw.AddDocument(doc);

            IndexReader ir = iw.Reader;
            iw.Dispose();

            IndexSearcher searcher = NewSearcher(ir);
            PostingsHighlighter highlighter = new PostingsHighlighterAnalyzerHelper(analyzer);
            //PostingsHighlighter highlighter = new PostingsHighlighter() {
            //      @Override
            //      protected Analyzer getIndexAnalyzer(String field)
            //{
            //    return analyzer;
            //}
            //    };
            Query query = new FuzzyQuery(new Term("body", "tets"), 1);
            TopDocs topDocs = searcher.Search(query, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            String[] snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a <b>test</b>.", snippets[0]);
            assertEquals("<b>Test</b> a one sentence document.", snippets[1]);

            // with prefix
            query = new FuzzyQuery(new Term("body", "tets"), 1, 2);
            topDocs = searcher.Search(query, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a <b>test</b>.", snippets[0]);
            assertEquals("<b>Test</b> a one sentence document.", snippets[1]);

            // wrong field
            BooleanQuery bq = new BooleanQuery();
            bq.Add(new MatchAllDocsQuery(), Occur.SHOULD);
            bq.Add(new FuzzyQuery(new Term("bogus", "tets"), 1), Occur.SHOULD);
            topDocs = searcher.Search(bq, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            snippets = highlighter.Highlight("body", bq, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a test.", snippets[0]);
            assertEquals("Test a one sentence document.", snippets[1]);

            ir.Dispose();
            dir.Dispose();
        }

        [Test]
        public void TestRanges()
        {
            Directory dir = NewDirectory();
            // use simpleanalyzer for more natural tokenization (else "test." is a token)
            Analyzer analyzer = new MockAnalyzer(Random(), MockTokenizer.SIMPLE, true);
            IndexWriterConfig iwc = NewIndexWriterConfig(TEST_VERSION_CURRENT, analyzer);
            iwc.SetMergePolicy(NewLogMergePolicy());
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir, iwc);

            FieldType offsetsType = new FieldType(TextField.TYPE_STORED);
            offsetsType.IndexOptions = (IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS);
            Field body = new Field("body", "", offsetsType);
            Document doc = new Document();
            doc.Add(body);

            body.SetStringValue("This is a test.");
            iw.AddDocument(doc);
            body.SetStringValue("Test a one sentence document.");
            iw.AddDocument(doc);

            IndexReader ir = iw.Reader;
            iw.Dispose();

            IndexSearcher searcher = NewSearcher(ir);
            PostingsHighlighter highlighter = new PostingsHighlighterAnalyzerHelper(analyzer);
            //PostingsHighlighter highlighter = new PostingsHighlighter() {
            //      @Override
            //      protected Analyzer getIndexAnalyzer(String field)
            //{
            //    return analyzer;
            //}
            //    };
            Query query = TermRangeQuery.NewStringRange("body", "ta", "tf", true, true);
            TopDocs topDocs = searcher.Search(query, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            String[] snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a <b>test</b>.", snippets[0]);
            assertEquals("<b>Test</b> a one sentence document.", snippets[1]);

            // null start
            query = TermRangeQuery.NewStringRange("body", null, "tf", true, true);
            topDocs = searcher.Search(query, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This <b>is</b> <b>a</b> <b>test</b>.", snippets[0]);
            assertEquals("<b>Test</b> <b>a</b> <b>one</b> <b>sentence</b> <b>document</b>.", snippets[1]);

            // null end
            query = TermRangeQuery.NewStringRange("body", "ta", null, true, true);
            topDocs = searcher.Search(query, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("<b>This</b> is a <b>test</b>.", snippets[0]);
            assertEquals("<b>Test</b> a one sentence document.", snippets[1]);

            // exact start inclusive
            query = TermRangeQuery.NewStringRange("body", "test", "tf", true, true);
            topDocs = searcher.Search(query, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a <b>test</b>.", snippets[0]);
            assertEquals("<b>Test</b> a one sentence document.", snippets[1]);

            // exact end inclusive
            query = TermRangeQuery.NewStringRange("body", "ta", "test", true, true);
            topDocs = searcher.Search(query, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a <b>test</b>.", snippets[0]);
            assertEquals("<b>Test</b> a one sentence document.", snippets[1]);

            // exact start exclusive
            BooleanQuery bq = new BooleanQuery();
            bq.Add(new MatchAllDocsQuery(), Occur.SHOULD);
            bq.Add(TermRangeQuery.NewStringRange("body", "test", "tf", false, true), Occur.SHOULD);
            topDocs = searcher.Search(bq, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            snippets = highlighter.Highlight("body", bq, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a test.", snippets[0]);
            assertEquals("Test a one sentence document.", snippets[1]);

            // exact end exclusive
            bq = new BooleanQuery();
            bq.Add(new MatchAllDocsQuery(), Occur.SHOULD);
            bq.Add(TermRangeQuery.NewStringRange("body", "ta", "test", true, false), Occur.SHOULD);
            topDocs = searcher.Search(bq, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            snippets = highlighter.Highlight("body", bq, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a test.", snippets[0]);
            assertEquals("Test a one sentence document.", snippets[1]);

            // wrong field
            bq = new BooleanQuery();
            bq.Add(new MatchAllDocsQuery(), Occur.SHOULD);
            bq.Add(TermRangeQuery.NewStringRange("bogus", "ta", "tf", true, true), Occur.SHOULD);
            topDocs = searcher.Search(bq, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            snippets = highlighter.Highlight("body", bq, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a test.", snippets[0]);
            assertEquals("Test a one sentence document.", snippets[1]);

            ir.Dispose();
            dir.Dispose();
        }

        [Test]
        public void TestWildcardInBoolean()
        {
            Directory dir = NewDirectory();
            // use simpleanalyzer for more natural tokenization (else "test." is a token)
            Analyzer analyzer = new MockAnalyzer(Random(), MockTokenizer.SIMPLE, true);
            IndexWriterConfig iwc = NewIndexWriterConfig(TEST_VERSION_CURRENT, analyzer);
            iwc.SetMergePolicy(NewLogMergePolicy());
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir, iwc);

            FieldType offsetsType = new FieldType(TextField.TYPE_STORED);
            offsetsType.IndexOptions = (IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS);
            Field body = new Field("body", "", offsetsType);
            Document doc = new Document();
            doc.Add(body);

            body.SetStringValue("This is a test.");
            iw.AddDocument(doc);
            body.SetStringValue("Test a one sentence document.");
            iw.AddDocument(doc);

            IndexReader ir = iw.Reader;
            iw.Dispose();

            IndexSearcher searcher = NewSearcher(ir);
            PostingsHighlighter highlighter = new PostingsHighlighterAnalyzerHelper(analyzer);
            //PostingsHighlighter highlighter = new PostingsHighlighter() {
            //      @Override
            //      protected Analyzer getIndexAnalyzer(String field)
            //{
            //    return analyzer;
            //}
            //    };
            BooleanQuery query = new BooleanQuery();
            query.Add(new WildcardQuery(new Term("body", "te*")), Occur.SHOULD);
            TopDocs topDocs = searcher.Search(query, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            String[] snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a <b>test</b>.", snippets[0]);
            assertEquals("<b>Test</b> a one sentence document.", snippets[1]);

            // must not
            query = new BooleanQuery();
            query.Add(new MatchAllDocsQuery(), Occur.SHOULD);
            query.Add(new WildcardQuery(new Term("bogus", "te*")), Occur.MUST_NOT);
            topDocs = searcher.Search(query, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a test.", snippets[0]);
            assertEquals("Test a one sentence document.", snippets[1]);

            ir.Dispose();
            dir.Dispose();
        }

        [Test]
        public void TestWildcardInDisjunctionMax()
        {
            Directory dir = NewDirectory();
            // use simpleanalyzer for more natural tokenization (else "test." is a token)
            Analyzer analyzer = new MockAnalyzer(Random(), MockTokenizer.SIMPLE, true);
            IndexWriterConfig iwc = NewIndexWriterConfig(TEST_VERSION_CURRENT, analyzer);
            iwc.SetMergePolicy(NewLogMergePolicy());
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir, iwc);

            FieldType offsetsType = new FieldType(TextField.TYPE_STORED);
            offsetsType.IndexOptions = (IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS);
            Field body = new Field("body", "", offsetsType);
            Document doc = new Document();
            doc.Add(body);

            body.SetStringValue("This is a test.");
            iw.AddDocument(doc);
            body.SetStringValue("Test a one sentence document.");
            iw.AddDocument(doc);

            IndexReader ir = iw.Reader;
            iw.Dispose();

            IndexSearcher searcher = NewSearcher(ir);
            PostingsHighlighter highlighter = new PostingsHighlighterAnalyzerHelper(analyzer);
            //PostingsHighlighter highlighter = new PostingsHighlighter() {
            //      @Override
            //      protected Analyzer getIndexAnalyzer(String field)
            //{
            //    return analyzer;
            //}
            //    };
            DisjunctionMaxQuery query = new DisjunctionMaxQuery(0);
            query.Add(new WildcardQuery(new Term("body", "te*")));
            TopDocs topDocs = searcher.Search(query, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            String[] snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a <b>test</b>.", snippets[0]);
            assertEquals("<b>Test</b> a one sentence document.", snippets[1]);

            ir.Dispose();
            dir.Dispose();
        }

        [Test]
        public void TestSpanWildcard()
        {
            Directory dir = NewDirectory();
            // use simpleanalyzer for more natural tokenization (else "test." is a token)
            Analyzer analyzer = new MockAnalyzer(Random(), MockTokenizer.SIMPLE, true);
            IndexWriterConfig iwc = NewIndexWriterConfig(TEST_VERSION_CURRENT, analyzer);
            iwc.SetMergePolicy(NewLogMergePolicy());
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir, iwc);

            FieldType offsetsType = new FieldType(TextField.TYPE_STORED);
            offsetsType.IndexOptions = (IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS);
            Field body = new Field("body", "", offsetsType);
            Document doc = new Document();
            doc.Add(body);

            body.SetStringValue("This is a test.");
            iw.AddDocument(doc);
            body.SetStringValue("Test a one sentence document.");
            iw.AddDocument(doc);

            IndexReader ir = iw.Reader;
            iw.Dispose();

            IndexSearcher searcher = NewSearcher(ir);
            PostingsHighlighter highlighter = new PostingsHighlighterAnalyzerHelper(analyzer);
            //PostingsHighlighter highlighter = new PostingsHighlighter() {
            //      @Override
            //      protected Analyzer getIndexAnalyzer(String field)
            //{
            //    return analyzer;
            //}
            //    };
            Query query = new SpanMultiTermQueryWrapper<WildcardQuery>(new WildcardQuery(new Term("body", "te*")));
            TopDocs topDocs = searcher.Search(query, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            String[] snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a <b>test</b>.", snippets[0]);
            assertEquals("<b>Test</b> a one sentence document.", snippets[1]);

            ir.Dispose();
            dir.Dispose();
        }

        [Test]
        public void TestSpanOr()
        {
            Directory dir = NewDirectory();
            // use simpleanalyzer for more natural tokenization (else "test." is a token)
            Analyzer analyzer = new MockAnalyzer(Random(), MockTokenizer.SIMPLE, true);
            IndexWriterConfig iwc = NewIndexWriterConfig(TEST_VERSION_CURRENT, analyzer);
            iwc.SetMergePolicy(NewLogMergePolicy());
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir, iwc);

            FieldType offsetsType = new FieldType(TextField.TYPE_STORED);
            offsetsType.IndexOptions = (IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS);
            Field body = new Field("body", "", offsetsType);
            Document doc = new Document();
            doc.Add(body);

            body.SetStringValue("This is a test.");
            iw.AddDocument(doc);
            body.SetStringValue("Test a one sentence document.");
            iw.AddDocument(doc);

            IndexReader ir = iw.Reader;
            iw.Dispose();

            IndexSearcher searcher = NewSearcher(ir);
            PostingsHighlighter highlighter = new PostingsHighlighterAnalyzerHelper(analyzer);
            //PostingsHighlighter highlighter = new PostingsHighlighter() {
            //      @Override
            //      protected Analyzer getIndexAnalyzer(String field)
            //{
            //    return analyzer;
            //}
            //    };
            SpanQuery childQuery = new SpanMultiTermQueryWrapper<WildcardQuery>(new WildcardQuery(new Term("body", "te*")));
            Query query = new SpanOrQuery(new SpanQuery[] { childQuery });
            TopDocs topDocs = searcher.Search(query, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            String[] snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a <b>test</b>.", snippets[0]);
            assertEquals("<b>Test</b> a one sentence document.", snippets[1]);

            ir.Dispose();
            dir.Dispose();
        }

        [Test]
        public void TestSpanNear()
        {
            Directory dir = NewDirectory();
            // use simpleanalyzer for more natural tokenization (else "test." is a token)
            Analyzer analyzer = new MockAnalyzer(Random(), MockTokenizer.SIMPLE, true);
            IndexWriterConfig iwc = NewIndexWriterConfig(TEST_VERSION_CURRENT, analyzer);
            iwc.SetMergePolicy(NewLogMergePolicy());
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir, iwc);

            FieldType offsetsType = new FieldType(TextField.TYPE_STORED);
            offsetsType.IndexOptions = (IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS);
            Field body = new Field("body", "", offsetsType);
            Document doc = new Document();
            doc.Add(body);

            body.SetStringValue("This is a test.");
            iw.AddDocument(doc);
            body.SetStringValue("Test a one sentence document.");
            iw.AddDocument(doc);

            IndexReader ir = iw.Reader;
            iw.Dispose();

            IndexSearcher searcher = NewSearcher(ir);
            PostingsHighlighter highlighter = new PostingsHighlighterAnalyzerHelper(analyzer);
            //PostingsHighlighter highlighter = new PostingsHighlighter() {
            //      @Override
            //      protected Analyzer getIndexAnalyzer(String field)
            //{
            //    return analyzer;
            //}
            //    };
            SpanQuery childQuery = new SpanMultiTermQueryWrapper<WildcardQuery>(new WildcardQuery(new Term("body", "te*")));
            Query query = new SpanNearQuery(new SpanQuery[] { childQuery }, 0, true);
            TopDocs topDocs = searcher.Search(query, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            String[] snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a <b>test</b>.", snippets[0]);
            assertEquals("<b>Test</b> a one sentence document.", snippets[1]);

            ir.Dispose();
            dir.Dispose();
        }

        [Test]
        public void TestSpanNot()
        {
            Directory dir = NewDirectory();
            // use simpleanalyzer for more natural tokenization (else "test." is a token)
            Analyzer analyzer = new MockAnalyzer(Random(), MockTokenizer.SIMPLE, true);
            IndexWriterConfig iwc = NewIndexWriterConfig(TEST_VERSION_CURRENT, analyzer);
            iwc.SetMergePolicy(NewLogMergePolicy());
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir, iwc);

            FieldType offsetsType = new FieldType(TextField.TYPE_STORED);
            offsetsType.IndexOptions = (IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS);
            Field body = new Field("body", "", offsetsType);
            Document doc = new Document();
            doc.Add(body);

            body.SetStringValue("This is a test.");
            iw.AddDocument(doc);
            body.SetStringValue("Test a one sentence document.");
            iw.AddDocument(doc);

            IndexReader ir = iw.Reader;
            iw.Dispose();

            IndexSearcher searcher = NewSearcher(ir);
            PostingsHighlighter highlighter = new PostingsHighlighterAnalyzerHelper(analyzer);
            //PostingsHighlighter highlighter = new PostingsHighlighter() {
            //      @Override
            //      protected Analyzer getIndexAnalyzer(String field)
            //{
            //    return analyzer;
            //}
            //    };
            SpanQuery include = new SpanMultiTermQueryWrapper<WildcardQuery>(new WildcardQuery(new Term("body", "te*")));
            SpanQuery exclude = new SpanTermQuery(new Term("body", "bogus"));
            Query query = new SpanNotQuery(include, exclude);
            TopDocs topDocs = searcher.Search(query, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            String[] snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a <b>test</b>.", snippets[0]);
            assertEquals("<b>Test</b> a one sentence document.", snippets[1]);

            ir.Dispose();
            dir.Dispose();
        }

        [Test]
        public void TestSpanPositionCheck()
        {
            Directory dir = NewDirectory();
            // use simpleanalyzer for more natural tokenization (else "test." is a token)
            Analyzer analyzer = new MockAnalyzer(Random(), MockTokenizer.SIMPLE, true);
            IndexWriterConfig iwc = NewIndexWriterConfig(TEST_VERSION_CURRENT, analyzer);
            iwc.SetMergePolicy(NewLogMergePolicy());
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir, iwc);

            FieldType offsetsType = new FieldType(TextField.TYPE_STORED);
            offsetsType.IndexOptions = (IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS);
            Field body = new Field("body", "", offsetsType);
            Document doc = new Document();
            doc.Add(body);

            body.SetStringValue("This is a test.");
            iw.AddDocument(doc);
            body.SetStringValue("Test a one sentence document.");
            iw.AddDocument(doc);

            IndexReader ir = iw.Reader;
            iw.Dispose();

            IndexSearcher searcher = NewSearcher(ir);
            PostingsHighlighter highlighter = new PostingsHighlighterAnalyzerHelper(analyzer);
            //PostingsHighlighter highlighter = new PostingsHighlighter() {
            //      @Override
            //      protected Analyzer getIndexAnalyzer(String field)
            //{
            //    return analyzer;
            //}
            //    };
            SpanQuery childQuery = new SpanMultiTermQueryWrapper<WildcardQuery>(new WildcardQuery(new Term("body", "te*")));
            Query query = new SpanFirstQuery(childQuery, 1000000);
            TopDocs topDocs = searcher.Search(query, null, 10, Sort.INDEXORDER);
            assertEquals(2, topDocs.TotalHits);
            String[] snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(2, snippets.Length);
            assertEquals("This is a <b>test</b>.", snippets[0]);
            assertEquals("<b>Test</b> a one sentence document.", snippets[1]);

            ir.Dispose();
            dir.Dispose();
        }

        internal class PostingsHighlighterAnalyzerAndFormatterHelper : PostingsHighlighterAnalyzerHelper
        {
            private readonly PassageFormatter formatter;

            public PostingsHighlighterAnalyzerAndFormatterHelper(Analyzer analyzer, PassageFormatter formatter)
                : base(analyzer)
            {
                this.formatter = formatter;
            }

            protected override PassageFormatter GetFormatter(string field)
            {
                return formatter;
            }
        }

        internal class PassageFormatterHelper : PassageFormatter
        {
            public override object Format(Passage[] passages, string content)
            {
                // Copied from DefaultPassageFormatter, but
                // tweaked to include the matched term:
                StringBuilder sb = new StringBuilder();
                int pos = 0;
                foreach (Passage passage in passages)
                {
                    // don't add ellipsis if its the first one, or if its connected.
                    if (passage.StartOffset > pos && pos > 0)
                    {
                        sb.append("... ");
                    }
                    pos = passage.StartOffset;
                    for (int i = 0; i < passage.NumMatches; i++)
                    {
                        int start = passage.matchStarts[i];
                        int end = passage.matchEnds[i];
                        // its possible to have overlapping terms
                        if (start > pos)
                        {
                            sb.Append(content, pos, start - pos);
                        }
                        if (end > pos)
                        {
                            sb.Append("<b>");
                            int startPos = Math.Max(pos, start);
                            sb.Append(content, startPos, end - startPos);
                            sb.Append('(');
                            sb.Append(passage.MatchTerms[i].Utf8ToString());
                            sb.Append(')');
                            sb.Append("</b>");
                            pos = end;
                        }
                    }
                    // its possible a "term" from the analyzer could span a sentence boundary.
                    sb.Append(content, pos, Math.Max(pos, passage.EndOffset) - pos);
                    pos = passage.EndOffset;
                }
                return sb.toString();
            }
        }

        /** Runs a query with two MTQs and confirms the formatter
         *  can tell which query matched which hit. */
        [Test]
        public void TestWhichMTQMatched()
        {
            Directory dir = NewDirectory();
            // use simpleanalyzer for more natural tokenization (else "test." is a token)
            Analyzer analyzer = new MockAnalyzer(Random(), MockTokenizer.SIMPLE, true);
            IndexWriterConfig iwc = NewIndexWriterConfig(TEST_VERSION_CURRENT, analyzer);
            iwc.SetMergePolicy(NewLogMergePolicy());
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir, iwc);

            FieldType offsetsType = new FieldType(TextField.TYPE_STORED);
            offsetsType.IndexOptions = (IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS);
            Field body = new Field("body", "", offsetsType);
            Document doc = new Document();
            doc.Add(body);

            body.SetStringValue("Test a one sentence document.");
            iw.AddDocument(doc);

            IndexReader ir = iw.Reader;
            iw.Dispose();

            IndexSearcher searcher = NewSearcher(ir);
            PostingsHighlighter highlighter = new PostingsHighlighterAnalyzerHelper(analyzer);
            //PostingsHighlighter highlighter = new PostingsHighlighter() {
            //      @Override
            //      protected Analyzer getIndexAnalyzer(String field)
            //{
            //    return analyzer;
            //}
            //    };
            BooleanQuery query = new BooleanQuery();
            query.Add(new WildcardQuery(new Term("body", "te*")), Occur.SHOULD);
            query.Add(new WildcardQuery(new Term("body", "one")), Occur.SHOULD);
            query.Add(new WildcardQuery(new Term("body", "se*")), Occur.SHOULD);
            TopDocs topDocs = searcher.Search(query, null, 10, Sort.INDEXORDER);
            assertEquals(1, topDocs.TotalHits);
            String[] snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(1, snippets.Length);

            // Default formatter just bolds each hit:
            assertEquals("<b>Test</b> a <b>one</b> <b>sentence</b> document.", snippets[0]);

            // Now use our own formatter, that also stuffs the
            // matching term's text into the result:
            highlighter = new PostingsHighlighterAnalyzerAndFormatterHelper(analyzer, new PassageFormatterHelper());

            //highlighter = new PostingsHighlighter()
            //{
            //    @Override
            //      protected Analyzer getIndexAnalyzer(String field)
            //{
            //    return analyzer;
            //}

            //@Override
            //      protected PassageFormatter getFormatter(String field)
            //{
            //    return new PassageFormatter() {

            //          @Override
            //          public Object format(Passage passages[], String content)
            //{
            //    // Copied from DefaultPassageFormatter, but
            //    // tweaked to include the matched term:
            //    StringBuilder sb = new StringBuilder();
            //    int pos = 0;
            //    for (Passage passage : passages)
            //    {
            //        // don't add ellipsis if its the first one, or if its connected.
            //        if (passage.startOffset > pos && pos > 0)
            //        {
            //            sb.append("... ");
            //        }
            //        pos = passage.startOffset;
            //        for (int i = 0; i < passage.numMatches; i++)
            //        {
            //            int start = passage.matchStarts[i];
            //            int end = passage.matchEnds[i];
            //            // its possible to have overlapping terms
            //            if (start > pos)
            //            {
            //                sb.append(content, pos, start);
            //            }
            //            if (end > pos)
            //            {
            //                sb.append("<b>");
            //                sb.append(content, Math.max(pos, start), end);
            //                sb.append('(');
            //                sb.append(passage.getMatchTerms()[i].utf8ToString());
            //                sb.append(')');
            //                sb.append("</b>");
            //                pos = end;
            //            }
            //        }
            //        // its possible a "term" from the analyzer could span a sentence boundary.
            //        sb.append(content, pos, Math.max(pos, passage.endOffset));
            //        pos = passage.endOffset;
            //    }
            //    return sb.toString();
            //}
            //        };
            //      }
            //    };


            assertEquals(1, topDocs.TotalHits);
            snippets = highlighter.Highlight("body", query, searcher, topDocs);
            assertEquals(1, snippets.Length);

            // Default formatter bolds each hit:
            assertEquals("<b>Test(body:te*)</b> a <b>one(body:one)</b> <b>sentence(body:se*)</b> document.", snippets[0]);

            ir.Dispose();
            dir.Dispose();
        }
    }
}
#endif