using Lucene.Net.Attributes;
using Lucene.Net.Randomized.Generators;
using Lucene.Net.Support;
using Lucene.Net.Support.Threading;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lucene.Net.Search
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

    using Analyzer = Lucene.Net.Analysis.Analyzer;
    using Directory = Lucene.Net.Store.Directory;
    using DirectoryReader = Lucene.Net.Index.DirectoryReader;
    using Document = Lucene.Net.Documents.Document;
    using Field = Lucene.Net.Documents.Field;
    using IIndexableField = Lucene.Net.Index.IIndexableField;
    using IndexCommit = Lucene.Net.Index.IndexCommit;
    using IndexReader = Lucene.Net.Index.IndexReader;
    using IndexWriter = Lucene.Net.Index.IndexWriter;
    using IndexWriterConfig = Lucene.Net.Index.IndexWriterConfig;
    using IOUtils = Lucene.Net.Util.IOUtils;
    using KeepOnlyLastCommitDeletionPolicy = Lucene.Net.Index.KeepOnlyLastCommitDeletionPolicy;
    using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
    using MockAnalyzer = Lucene.Net.Analysis.MockAnalyzer;
    using NoMergePolicy = Lucene.Net.Index.NoMergePolicy;
    using NRTCachingDirectory = Lucene.Net.Store.NRTCachingDirectory;
    using OpenMode = Lucene.Net.Index.OpenMode;
    using RandomIndexWriter = Lucene.Net.Index.RandomIndexWriter;
    using SnapshotDeletionPolicy = Lucene.Net.Index.SnapshotDeletionPolicy;
    using Term = Lucene.Net.Index.Term;
    using TestUtil = Lucene.Net.Util.TestUtil;
    using TextField = Lucene.Net.Documents.TextField;
    using ThreadedIndexingAndSearchingTestCase = Lucene.Net.Index.ThreadedIndexingAndSearchingTestCase;
    using TrackingIndexWriter = Lucene.Net.Index.TrackingIndexWriter;
    //using ThreadInterruptedException = Lucene.Net.Util.ThreadInterruptedException;
    using Version = Lucene.Net.Util.LuceneVersion;

    [SuppressCodecs("SimpleText", "Memory", "Direct")]
    [TestFixture]
    public class TestControlledRealTimeReopenThread : ThreadedIndexingAndSearchingTestCase
    {

        // Not guaranteed to reflect deletes:
        private SearcherManager nrtNoDeletes;

        // Is guaranteed to reflect deletes:
        private SearcherManager nrtDeletes;

        private TrackingIndexWriter genWriter;

        private ControlledRealTimeReopenThread<IndexSearcher> nrtDeletesThread;
        private ControlledRealTimeReopenThread<IndexSearcher> nrtNoDeletesThread;

        private readonly ThreadLocal<long?> lastGens = new ThreadLocal<long?>();
        private bool warmCalled;

        [Test]
        public virtual void TestControlledRealTimeReopenThread_Mem()
        {
            RunTest("TestControlledRealTimeReopenThread");
        }

        protected internal override IndexSearcher FinalSearcher
        {
            get
            {
                if (VERBOSE)
                {
                    Console.WriteLine("TEST: finalSearcher maxGen=" + MaxGen);
                }
                nrtDeletesThread.WaitForGeneration(MaxGen);
                return nrtDeletes.Acquire();
            }
        }

        protected internal override Directory GetDirectory(Directory @in)
        {
            // Randomly swap in NRTCachingDir
            if (Random().NextBoolean())
            {
                if (VERBOSE)
                {
                    Console.WriteLine("TEST: wrap NRTCachingDir");
                }

                return new NRTCachingDirectory(@in, 5.0, 60.0);
            }
            else
            {
                return @in;
            }
        }

        protected internal override void UpdateDocuments(Term id, IEnumerable<IEnumerable<IIndexableField>> docs)
        {
            long gen = genWriter.UpdateDocuments(id, docs);

            // Randomly verify the update "took":
            if (Random().Next(20) == 2)
            {
                if (VERBOSE)
                {
                    Console.WriteLine(Thread.CurrentThread.Name + ": nrt: verify " + id);
                }
                nrtDeletesThread.WaitForGeneration(gen);
                IndexSearcher s = nrtDeletes.Acquire();
                if (VERBOSE)
                {
                    Console.WriteLine(Thread.CurrentThread.Name + ": nrt: got searcher=" + s);
                }
                try
                {
                    assertEquals(docs.Count(), s.Search(new TermQuery(id), 10).TotalHits);
                }
                finally
                {
                    nrtDeletes.Release(s);
                }
            }

            lastGens.Value = gen;

        }

        protected internal override void AddDocuments(Term id, IEnumerable<IEnumerable<IIndexableField>> docs)
        {
            long gen = genWriter.AddDocuments(docs);
            // Randomly verify the add "took":
            if (Random().Next(20) == 2)
            {
                if (VERBOSE)
                {
                    Console.WriteLine(Thread.CurrentThread.Name + ": nrt: verify " + id);
                }
                nrtNoDeletesThread.WaitForGeneration(gen);
                IndexSearcher s = nrtNoDeletes.Acquire();
                if (VERBOSE)
                {
                    Console.WriteLine(Thread.CurrentThread.Name + ": nrt: got searcher=" + s);
                }
                try
                {
                    assertEquals(docs.Count(), s.Search(new TermQuery(id), 10).TotalHits);
                }
                finally
                {
                    nrtNoDeletes.Release(s);
                }
            }
            lastGens.Value = gen;
        }

        protected internal override void AddDocument(Term id, IEnumerable<IIndexableField> doc)
        {
            long gen = genWriter.AddDocument(doc);

            // Randomly verify the add "took":
            if (Random().Next(20) == 2)
            {
                if (VERBOSE)
                {
                    Console.WriteLine(Thread.CurrentThread.Name + ": nrt: verify " + id);
                }
                nrtNoDeletesThread.WaitForGeneration(gen);
                IndexSearcher s = nrtNoDeletes.Acquire();
                if (VERBOSE)
                {
                    Console.WriteLine(Thread.CurrentThread.Name + ": nrt: got searcher=" + s);
                }
                try
                {
                    assertEquals(1, s.Search(new TermQuery(id), 10).TotalHits);
                }
                finally
                {
                    nrtNoDeletes.Release(s);
                }
            }
            lastGens.Value = gen;
        }

        protected internal override void UpdateDocument(Term id, IEnumerable<IIndexableField> doc)
        {
            long gen = genWriter.UpdateDocument(id, doc);
            // Randomly verify the udpate "took":
            if (Random().Next(20) == 2)
            {
                if (VERBOSE)
                {
                    Console.WriteLine(Thread.CurrentThread.Name + ": nrt: verify " + id);
                }
                nrtDeletesThread.WaitForGeneration(gen);
                IndexSearcher s = nrtDeletes.Acquire();
                if (VERBOSE)
                {
                    Console.WriteLine(Thread.CurrentThread.Name + ": nrt: got searcher=" + s);
                }
                try
                {
                    assertEquals(1, s.Search(new TermQuery(id), 10).TotalHits);
                }
                finally
                {
                    nrtDeletes.Release(s);
                }
            }
            lastGens.Value = gen;
        }

        protected internal override void DeleteDocuments(Term id)
        {
            long gen = genWriter.DeleteDocuments(id);
            // randomly verify the delete "took":
            if (Random().Next(20) == 7)
            {
                if (VERBOSE)
                {
                    Console.WriteLine(Thread.CurrentThread.Name + ": nrt: verify del " + id);
                }
                nrtDeletesThread.WaitForGeneration(gen);
                IndexSearcher s = nrtDeletes.Acquire();
                if (VERBOSE)
                {
                    Console.WriteLine(Thread.CurrentThread.Name + ": nrt: got searcher=" + s);
                }
                try
                {
                    assertEquals(0, s.Search(new TermQuery(id), 10).TotalHits);
                }
                finally
                {
                    nrtDeletes.Release(s);
                }
            }
            lastGens.Value = gen;
        }

        protected internal override void DoAfterWriter(TaskScheduler es)
        {
            double minReopenSec = 0.01 + 0.05 * Random().NextDouble();
            double maxReopenSec = minReopenSec * (1.0 + 10 * Random().NextDouble());

            if (VERBOSE)
            {
                Console.WriteLine("TEST: make SearcherManager maxReopenSec=" + maxReopenSec + " minReopenSec=" + minReopenSec);
            }

            genWriter = new TrackingIndexWriter(writer);

            SearcherFactory sf = new SearcherFactoryAnonymousInnerClassHelper(this, es);

            nrtNoDeletes = new SearcherManager(writer, false, sf);
            nrtDeletes = new SearcherManager(writer, true, sf);

            nrtDeletesThread = new ControlledRealTimeReopenThread<IndexSearcher>(genWriter, nrtDeletes, maxReopenSec, minReopenSec);
            nrtDeletesThread.Name = "NRTDeletes Reopen Thread";
#if !NETSTANDARD
            nrtDeletesThread.Priority = (ThreadPriority)Math.Min((int)Thread.CurrentThread.Priority + 2, (int)ThreadPriority.Highest);
#endif
            nrtDeletesThread.SetDaemon(true);
            nrtDeletesThread.Start();

            nrtNoDeletesThread = new ControlledRealTimeReopenThread<IndexSearcher>(genWriter, nrtNoDeletes, maxReopenSec, minReopenSec);
            nrtNoDeletesThread.Name = "NRTNoDeletes Reopen Thread";
#if !NETSTANDARD
            nrtNoDeletesThread.Priority = (ThreadPriority)Math.Min((int)Thread.CurrentThread.Priority + 2, (int)ThreadPriority.Highest);
#endif
            nrtNoDeletesThread.SetDaemon(true);
            nrtNoDeletesThread.Start();
        }

        private class SearcherFactoryAnonymousInnerClassHelper : SearcherFactory
        {
            private readonly TestControlledRealTimeReopenThread outerInstance;

            private TaskScheduler es;

            public SearcherFactoryAnonymousInnerClassHelper(TestControlledRealTimeReopenThread outerInstance, TaskScheduler es)
            {
                this.outerInstance = outerInstance;
                this.es = es;
            }

            public override IndexSearcher NewSearcher(IndexReader r)
            {
                outerInstance.warmCalled = true;
                IndexSearcher s = new IndexSearcher(r, es);
                s.Search(new TermQuery(new Term("body", "united")), 10);
                return s;
            }
        }

        protected internal override void DoAfterIndexingThreadDone()
        {
            long? gen = lastGens.Value;
            if (gen != null)
            {
                AddMaxGen((long)gen);
            }
        }

        private long MaxGen = -1;

        private void AddMaxGen(long gen)
        {
            lock (this)
            {
                MaxGen = Math.Max(gen, MaxGen);
            }
        }

        protected internal override void DoSearching(TaskScheduler es, long stopTime)
        {
            RunSearchThreads(stopTime);
        }

        protected internal override IndexSearcher CurrentSearcher
        {
            get
            {
                // Test doesn't assert deletions until the end, so we
                // can randomize whether dels must be applied
                SearcherManager nrt;
                if (Random().NextBoolean())
                {
                    nrt = nrtDeletes;
                }
                else
                {
                    nrt = nrtNoDeletes;
                }

                return nrt.Acquire();
            }
        }

        protected internal override void ReleaseSearcher(IndexSearcher s)
        {
            // NOTE: a bit iffy... technically you should release
            // against the same SearcherManager you acquired from... but
            // both impls just decRef the underlying reader so we
            // can get away w/ cheating:
            nrtNoDeletes.Release(s);
        }

        protected internal override void DoClose()
        {
            Assert.IsTrue(warmCalled);
            if (VERBOSE)
            {
                Console.WriteLine("TEST: now close SearcherManagers");
            }
            nrtDeletesThread.Dispose();
            nrtDeletes.Dispose();
            nrtNoDeletesThread.Dispose();
            nrtNoDeletes.Dispose();
        }

        /*
         * LUCENE-3528 - NRTManager hangs in certain situations 
         */
#if !NETSTANDARD
        // LUCENENET: There is no Timeout on NUnit for .NET Core.
        [Timeout(60000)]
#endif
        [Test, HasTimeout]
        public virtual void TestThreadStarvationNoDeleteNRTReader()
        {
            IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random()));
            conf.SetMergePolicy(Random().NextBoolean() ? NoMergePolicy.COMPOUND_FILES : NoMergePolicy.NO_COMPOUND_FILES);
            Directory d = NewDirectory();
            CountdownEvent latch = new CountdownEvent(1);
            CountdownEvent signal = new CountdownEvent(1);

            LatchedIndexWriter _writer = new LatchedIndexWriter(d, conf, latch, signal);
            TrackingIndexWriter writer = new TrackingIndexWriter(_writer);
            SearcherManager manager = new SearcherManager(_writer, false, null);
            Document doc = new Document();
            doc.Add(NewTextField("test", "test", Field.Store.YES));
            writer.AddDocument(doc);
            manager.MaybeRefresh();
            ThreadClass t = new ThreadAnonymousInnerClassHelper(this, latch, signal, writer, manager);
            t.Start();
            _writer.waitAfterUpdate = true; // wait in addDocument to let some reopens go through
            long lastGen = writer.UpdateDocument(new Term("foo", "bar"), doc); // once this returns the doc is already reflected in the last reopen

            assertFalse(manager.IsSearcherCurrent()); // false since there is a delete in the queue

            IndexSearcher searcher = manager.Acquire();
            try
            {
                assertEquals(2, searcher.IndexReader.NumDocs);
            }
            finally
            {
                manager.Release(searcher);
            }
            ControlledRealTimeReopenThread<IndexSearcher> thread = new ControlledRealTimeReopenThread<IndexSearcher>(writer, manager, 0.01, 0.01);
            thread.Start(); // start reopening
            if (VERBOSE)
            {
                Console.WriteLine("waiting now for generation " + lastGen);
            }

            AtomicBoolean finished = new AtomicBoolean(false);
            ThreadClass waiter = new ThreadAnonymousInnerClassHelper2(this, lastGen, thread, finished);
            waiter.Start();
            manager.MaybeRefresh();
            waiter.Join(1000);
            if (!finished.Get())
            {
                waiter.Interrupt();
                fail("thread deadlocked on waitForGeneration");
            }
            thread.Dispose();
            thread.Join();
            IOUtils.Dispose(manager, _writer, d);
        }

        private class ThreadAnonymousInnerClassHelper : ThreadClass
        {
            private readonly TestControlledRealTimeReopenThread outerInstance;

            private CountdownEvent latch;
            private CountdownEvent signal;
            private TrackingIndexWriter writer;
            private SearcherManager manager;

            public ThreadAnonymousInnerClassHelper(TestControlledRealTimeReopenThread outerInstance, CountdownEvent latch, CountdownEvent signal, TrackingIndexWriter writer, SearcherManager manager)
            {
                this.outerInstance = outerInstance;
                this.latch = latch;
                this.signal = signal;
                this.writer = writer;
                this.manager = manager;
            }

            public override void Run()
            {
                try
                {
                    signal.Wait();
                    manager.MaybeRefresh();
                    writer.DeleteDocuments(new TermQuery(new Term("foo", "barista")));
                    manager.MaybeRefresh(); // kick off another reopen so we inc. the internal gen
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    Console.Write(e.StackTrace);
                }
                finally
                {
                    latch.Reset(latch.CurrentCount == 0 ? 0 : latch.CurrentCount - 1); // let the add below finish
                }
            }
        }

        private class ThreadAnonymousInnerClassHelper2 : ThreadClass
        {
            private readonly TestControlledRealTimeReopenThread outerInstance;

            private long lastGen;
            private ControlledRealTimeReopenThread<IndexSearcher> thread;
            private AtomicBoolean finished;

            public ThreadAnonymousInnerClassHelper2(TestControlledRealTimeReopenThread outerInstance, long lastGen, ControlledRealTimeReopenThread<IndexSearcher> thread, AtomicBoolean finished)
            {
                this.outerInstance = outerInstance;
                this.lastGen = lastGen;
                this.thread = thread;
                this.finished = finished;
            }

            public override void Run()
            {
#if !NETSTANDARD
                try
                {
#endif
                    thread.WaitForGeneration(lastGen);
#if !NETSTANDARD
                }
                catch (ThreadInterruptedException ie)
                {
                    Thread.CurrentThread.Interrupt();
                    throw new Exception(ie.Message, ie);
                }
#endif
                finished.Set(true);
            }
        }

        public class LatchedIndexWriter : IndexWriter
        {

            internal CountdownEvent latch;
            internal bool waitAfterUpdate = false;
            internal CountdownEvent signal;

            public LatchedIndexWriter(Directory d, IndexWriterConfig conf, CountdownEvent latch, CountdownEvent signal)
                : base(d, conf)
            {
                this.latch = latch;
                this.signal = signal;

            }

            public override void UpdateDocument(Term term, IEnumerable<IIndexableField> doc, Analyzer analyzer)
            {
                base.UpdateDocument(term, doc, analyzer);
#if !NETSTANDARD
                try
                {
#endif
                    if (waitAfterUpdate)
                    {
                        signal.Reset(signal.CurrentCount == 0 ? 0 : signal.CurrentCount - 1);
                        latch.Wait();
                    }
#if !NETSTANDARD
                }
#pragma warning disable 168
                catch (ThreadInterruptedException e)
#pragma warning restore 168
                {
                    throw;
                }
#endif
            }
        }

        [Test]
        public virtual void TestEvilSearcherFactory()
        {
            Directory dir = NewDirectory();
            RandomIndexWriter w = new RandomIndexWriter(Random(), dir, Similarity, TimeZone);
            w.Commit();

            IndexReader other = DirectoryReader.Open(dir);

            SearcherFactory theEvilOne = new SearcherFactoryAnonymousInnerClassHelper2(this, other);

            try
            {
                new SearcherManager(w.w, false, theEvilOne);
                fail("didn't hit expected exception");
            }
#pragma warning disable 168
            catch (InvalidOperationException ise)
#pragma warning restore 168
            {
                // expected
            }
            w.Dispose();
            other.Dispose();
            dir.Dispose();
        }

        private class SearcherFactoryAnonymousInnerClassHelper2 : SearcherFactory
        {
            private readonly TestControlledRealTimeReopenThread OuterInstance;

            private IndexReader Other;

            public SearcherFactoryAnonymousInnerClassHelper2(TestControlledRealTimeReopenThread outerInstance, IndexReader other)
            {
                this.OuterInstance = outerInstance;
                this.Other = other;
            }

            public override IndexSearcher NewSearcher(IndexReader ignored)
            {
                return OuterInstance.NewSearcher(Other);
            }
        }

        [Test]
        public virtual void TestListenerCalled()
        {
            Directory dir = NewDirectory();
            IndexWriter iw = new IndexWriter(dir, new IndexWriterConfig(TEST_VERSION_CURRENT, null));
            AtomicBoolean afterRefreshCalled = new AtomicBoolean(false);
            SearcherManager sm = new SearcherManager(iw, true, new SearcherFactory());
            sm.AddListener(new RefreshListenerAnonymousInnerClassHelper(this, afterRefreshCalled));
            iw.AddDocument(new Document());
            iw.Commit();
            assertFalse(afterRefreshCalled.Get());
            sm.MaybeRefreshBlocking();
            assertTrue(afterRefreshCalled.Get());
            sm.Dispose();
            iw.Dispose();
            dir.Dispose();
        }

        private class RefreshListenerAnonymousInnerClassHelper : ReferenceManager.IRefreshListener
        {
            private readonly TestControlledRealTimeReopenThread outerInstance;

            private AtomicBoolean afterRefreshCalled;

            public RefreshListenerAnonymousInnerClassHelper(TestControlledRealTimeReopenThread outerInstance, AtomicBoolean afterRefreshCalled)
            {
                this.outerInstance = outerInstance;
                this.afterRefreshCalled = afterRefreshCalled;
            }

            public void BeforeRefresh()
            {
            }
            public void AfterRefresh(bool didRefresh)
            {
                if (didRefresh)
                {
                    afterRefreshCalled.Set(true);
                }
            }
        }

#if !NETSTANDARD
        // LUCENENET: There is no Timeout on NUnit for .NET Core.
        [Timeout(40000)]
#endif
        // LUCENE-5461
        [Test, HasTimeout]
        public virtual void TestCRTReopen()
        {
            //test behaving badly

            //should be high enough
            int maxStaleSecs = 20;

            //build crap data just to store it.
            string s = "        abcdefghijklmnopqrstuvwxyz     ";
            char[] chars = s.ToCharArray();
            StringBuilder builder = new StringBuilder(2048);
            for (int i = 0; i < 2048; i++)
            {
                builder.Append(chars[Random().Next(chars.Length)]);
            }
            string content = builder.ToString();

            SnapshotDeletionPolicy sdp = new SnapshotDeletionPolicy(new KeepOnlyLastCommitDeletionPolicy());
            Directory dir = new NRTCachingDirectory(NewFSDirectory(CreateTempDir("nrt")), 5, 128);
            IndexWriterConfig config = new IndexWriterConfig(
#pragma warning disable 612, 618
                Version.LUCENE_46,
#pragma warning restore 612, 618
                new MockAnalyzer(Random()));
            config.SetIndexDeletionPolicy(sdp);
            config.SetOpenMode(OpenMode.CREATE_OR_APPEND);
            IndexWriter iw = new IndexWriter(dir, config);
            SearcherManager sm = new SearcherManager(iw, true, new SearcherFactory());
            TrackingIndexWriter tiw = new TrackingIndexWriter(iw);
            ControlledRealTimeReopenThread<IndexSearcher> controlledRealTimeReopenThread = 
                new ControlledRealTimeReopenThread<IndexSearcher>(tiw, sm, maxStaleSecs, 0);

            controlledRealTimeReopenThread.SetDaemon(true);
            controlledRealTimeReopenThread.Start();

            IList<ThreadClass> commitThreads = new List<ThreadClass>();

            for (int i = 0; i < 500; i++)
            {
                if (i > 0 && i % 50 == 0)
                {
                    ThreadClass commitThread = new RunnableAnonymousInnerClassHelper(this, sdp, dir, iw);
                    commitThread.Start();
                    commitThreads.Add(commitThread);
                }
                Document d = new Document();
                d.Add(new TextField("count", i + "", Field.Store.NO));
                d.Add(new TextField("content", content, Field.Store.YES));
                long start = Environment.TickCount;
                long l = tiw.AddDocument(d);
                controlledRealTimeReopenThread.WaitForGeneration(l);
                long wait = Environment.TickCount - start;
                assertTrue("waited too long for generation " + wait, wait < (maxStaleSecs * 1000));
                IndexSearcher searcher = sm.Acquire();
                TopDocs td = searcher.Search(new TermQuery(new Term("count", i + "")), 10);
                sm.Release(searcher);
                assertEquals(1, td.TotalHits);
            }

            foreach (ThreadClass commitThread in commitThreads)
            {
                commitThread.Join();
            }

            controlledRealTimeReopenThread.Dispose();
            sm.Dispose();
            iw.Dispose();
            dir.Dispose();
        }

        private class RunnableAnonymousInnerClassHelper : ThreadClass
        {
            private readonly TestControlledRealTimeReopenThread outerInstance;

            private SnapshotDeletionPolicy sdp;
            private Directory dir;
            private IndexWriter iw;

            public RunnableAnonymousInnerClassHelper(TestControlledRealTimeReopenThread outerInstance, SnapshotDeletionPolicy sdp, Directory dir, IndexWriter iw)
            {
                this.outerInstance = outerInstance;
                this.sdp = sdp;
                this.dir = dir;
                this.iw = iw;
            }

            public override void Run()
            {
                try
                {
                    iw.Commit();
                    IndexCommit ic = sdp.Snapshot();
                    foreach (string name in ic.FileNames)
                    {
                        //distribute, and backup
                        //System.out.println(names);
                        assertTrue(SlowFileExists(dir, name));
                    }
                }
                catch (Exception e)
                {
                    throw new Exception(e.toString(), e);
                }
            }
        }
    }
}