﻿using Lucene.Net.Index;
using Lucene.Net.Util.Fst;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Lucene.Net.Codecs.Memory
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

    using ArrayUtil = Util.ArrayUtil;
    using ByteArrayDataInput = Store.ByteArrayDataInput;
    using ByteSequenceOutputs = Util.Fst.ByteSequenceOutputs;
    using BytesRef = Util.BytesRef;
    using ChecksumIndexInput = Store.ChecksumIndexInput;
    using DocsAndPositionsEnum = Index.DocsAndPositionsEnum;
    using DocsEnum = Index.DocsEnum;
    using FieldInfo = Index.FieldInfo;
    using FieldInfos = Index.FieldInfos;
    using FST = Util.Fst.FST;
    using IBits = Util.IBits;
    using IndexFileNames = Index.IndexFileNames;
    using IndexInput = Store.IndexInput;
    using IndexOptions = Index.IndexOptions;
    using IndexOutput = Store.IndexOutput;
    using Int32sRef = Util.Int32sRef;
    using IOContext = Store.IOContext;
    using IOUtils = Util.IOUtils;
    using PackedInt32s = Util.Packed.PackedInt32s;
    using RAMOutputStream = Store.RAMOutputStream;
    using RamUsageEstimator = Util.RamUsageEstimator;
    using SegmentReadState = Index.SegmentReadState;
    using SegmentWriteState = Index.SegmentWriteState;
    using Terms = Index.Terms;
    using TermsEnum = Index.TermsEnum;
    using Util = Util.Fst.Util;

    // TODO: would be nice to somehow allow this to act like
    // InstantiatedIndex, by never writing to disk; ie you write
    // to this Codec in RAM only and then when you open a reader
    // it pulls the FST directly from what you wrote w/o going
    // to disk.

    /// <summary>
    /// Stores terms &amp; postings (docs, positions, payloads) in
    /// RAM, using an FST.
    /// 
    /// <para>Note that this codec implements advance as a linear
    /// scan!  This means if you store large fields in here,
    /// queries that rely on advance will (AND BooleanQuery,
    /// PhraseQuery) will be relatively slow!
    /// </para>
    /// @lucene.experimental 
    /// </summary>

    // TODO: Maybe name this 'Cached' or something to reflect
    // the reality that it is actually written to disk, but
    // loads itself in ram?
    [PostingsFormatName("Memory")] // LUCENENET specific - using PostingsFormatName attribute to ensure the default name passed from subclasses is the same as this class name
    public sealed class MemoryPostingsFormat : PostingsFormat
    {
        private readonly bool doPackFST;
        private readonly float acceptableOverheadRatio;

        public MemoryPostingsFormat() 
            : this(false, PackedInt32s.DEFAULT)
        {
        }

        /// <summary>
        /// Create <see cref="MemoryPostingsFormat"/>, specifying advanced FST options. </summary>
        /// <param name="doPackFST"> <c>true</c> if a packed FST should be built.
        ///        NOTE: packed FSTs are limited to ~2.1 GB of postings. </param>
        /// <param name="acceptableOverheadRatio"> Allowable overhead for packed <see cref="int"/>s
        ///        during FST construction. </param>
        public MemoryPostingsFormat(bool doPackFST, float acceptableOverheadRatio) 
            : base()
        {
            this.doPackFST = doPackFST;
            this.acceptableOverheadRatio = acceptableOverheadRatio;
        }

        public override string ToString()
        {
            return "PostingsFormat(name=" + Name + " doPackFST= " + doPackFST + ")";
        }

        private sealed class TermsWriter : TermsConsumer
        {
            private readonly IndexOutput @out;
            private readonly FieldInfo field;
            private readonly Builder<BytesRef> builder;
            private readonly ByteSequenceOutputs outputs = ByteSequenceOutputs.Singleton;
            private readonly bool doPackFST;
            private readonly float acceptableOverheadRatio;
            private int termCount;

            public TermsWriter(IndexOutput @out, FieldInfo field, bool doPackFST, float acceptableOverheadRatio)
            {
                postingsWriter = new PostingsWriter(this);

                this.@out = @out;
                this.field = field;
                this.doPackFST = doPackFST;
                this.acceptableOverheadRatio = acceptableOverheadRatio;
                builder = new Builder<BytesRef>(FST.INPUT_TYPE.BYTE1, 0, 0, true, true, int.MaxValue, outputs, null, doPackFST, acceptableOverheadRatio, true, 15);
            }

            private class PostingsWriter : PostingsConsumer
            {
                private readonly MemoryPostingsFormat.TermsWriter outerInstance;

                public PostingsWriter(MemoryPostingsFormat.TermsWriter outerInstance)
                {
                    this.outerInstance = outerInstance;
                }

                private int lastDocID;
                private int lastPos;
                private int lastPayloadLen;

                // NOTE: not private so we don't pay access check at runtime:
                internal int docCount;
                internal RAMOutputStream buffer = new RAMOutputStream();

                private int lastOffsetLength;
                private int lastOffset;

                public override void StartDoc(int docID, int termDocFreq)
                {
                    int delta = docID - lastDocID;
                    Debug.Assert(docID == 0 || delta > 0);
                    lastDocID = docID;
                    docCount++;

                    if (outerInstance.field.IndexOptions == IndexOptions.DOCS_ONLY)
                    {
                        buffer.WriteVInt32(delta);
                    }
                    else if (termDocFreq == 1)
                    {
                        buffer.WriteVInt32((delta << 1) | 1);
                    }
                    else
                    {
                        buffer.WriteVInt32(delta << 1);
                        Debug.Assert(termDocFreq > 0);
                        buffer.WriteVInt32(termDocFreq);
                    }

                    lastPos = 0;
                    lastOffset = 0;
                }

                public override void AddPosition(int pos, BytesRef payload, int startOffset, int endOffset)
                {
                    Debug.Assert(payload == null || outerInstance.field.HasPayloads);

                    //System.out.println("      addPos pos=" + pos + " payload=" + payload);

                    int delta = pos - lastPos;
                    Debug.Assert(delta >= 0);
                    lastPos = pos;

                    int payloadLen = 0;

                    if (outerInstance.field.HasPayloads)
                    {
                        payloadLen = payload == null ? 0 : payload.Length;
                        if (payloadLen != lastPayloadLen)
                        {
                            lastPayloadLen = payloadLen;
                            buffer.WriteVInt32((delta << 1) | 1);
                            buffer.WriteVInt32(payloadLen);
                        }
                        else
                        {
                            buffer.WriteVInt32(delta << 1);
                        }
                    }
                    else
                    {
                        buffer.WriteVInt32(delta);
                    }

                    if (outerInstance.field.IndexOptions.CompareTo(IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS) >= 0)
                    {
                        // don't use startOffset - lastEndOffset, because this creates lots of negative vints for synonyms,
                        // and the numbers aren't that much smaller anyways.
                        int offsetDelta = startOffset - lastOffset;
                        int offsetLength = endOffset - startOffset;
                        if (offsetLength != lastOffsetLength)
                        {
                            buffer.WriteVInt32(offsetDelta << 1 | 1);
                            buffer.WriteVInt32(offsetLength);
                        }
                        else
                        {
                            buffer.WriteVInt32(offsetDelta << 1);
                        }
                        lastOffset = startOffset;
                        lastOffsetLength = offsetLength;
                    }

                    if (payloadLen > 0)
                    {
                        buffer.WriteBytes(payload.Bytes, payload.Offset, payloadLen);
                    }
                }

                public override void FinishDoc()
                {
                }

                public virtual PostingsWriter Reset()
                {
                    Debug.Assert(buffer.GetFilePointer() == 0);
                    lastDocID = 0;
                    docCount = 0;
                    lastPayloadLen = 0;
                    lastOffsetLength = -1;
                    return this;
                }
            }

            private readonly PostingsWriter postingsWriter;

            public override PostingsConsumer StartTerm(BytesRef text)
            {
                return postingsWriter.Reset();
            }

            private readonly RAMOutputStream buffer2 = new RAMOutputStream();
            private readonly BytesRef spare = new BytesRef();
            private byte[] finalBuffer = new byte[128];

            private readonly Int32sRef scratchIntsRef = new Int32sRef();

            public override void FinishTerm(BytesRef text, TermStats stats)
            {
                Debug.Assert(postingsWriter.docCount == stats.DocFreq);

                Debug.Assert(buffer2.GetFilePointer() == 0);

                buffer2.WriteVInt32(stats.DocFreq);
                if (field.IndexOptions != IndexOptions.DOCS_ONLY)
                {
                    buffer2.WriteVInt64(stats.TotalTermFreq - stats.DocFreq);
                }
                int pos = (int)buffer2.GetFilePointer();
                buffer2.WriteTo(finalBuffer, 0);
                buffer2.Reset();

                int totalBytes = pos + (int)postingsWriter.buffer.GetFilePointer();
                if (totalBytes > finalBuffer.Length)
                {
                    finalBuffer = ArrayUtil.Grow(finalBuffer, totalBytes);
                }
                postingsWriter.buffer.WriteTo(finalBuffer, pos);
                postingsWriter.buffer.Reset();

                spare.Bytes = finalBuffer;
                spare.Length = totalBytes;

                //System.out.println("    finishTerm term=" + text.utf8ToString() + " " + totalBytes + " bytes totalTF=" + stats.totalTermFreq);
                //for(int i=0;i<totalBytes;i++) {
                //  System.out.println("      " + Integer.toHexString(finalBuffer[i]&0xFF));
                //}

                builder.Add(Util.ToInt32sRef(text, scratchIntsRef), BytesRef.DeepCopyOf(spare));
                termCount++;
            }

            public override void Finish(long sumTotalTermFreq, long sumDocFreq, int docCount)
            {
                if (termCount > 0)
                {
                    @out.WriteVInt32(termCount);
                    @out.WriteVInt32(field.Number);
                    if (field.IndexOptions != IndexOptions.DOCS_ONLY)
                    {
                        @out.WriteVInt64(sumTotalTermFreq);
                    }
                    @out.WriteVInt64(sumDocFreq);
                    @out.WriteVInt32(docCount);
                    FST<BytesRef> fst = builder.Finish();
                    fst.Save(@out);
                    //System.out.println("finish field=" + field.name + " fp=" + out.getFilePointer());
                }
            }

            public override IComparer<BytesRef> Comparer
            {
                get
                {
                    return BytesRef.UTF8SortedAsUnicodeComparer;
                }
            }
        }

        private static string EXTENSION = "ram";
        private const string CODEC_NAME = "MemoryPostings";
        private const int VERSION_START = 0;
        private const int VERSION_CURRENT = VERSION_START;

        public override FieldsConsumer FieldsConsumer(SegmentWriteState state)
        {

            string fileName = IndexFileNames.SegmentFileName(state.SegmentInfo.Name, state.SegmentSuffix, EXTENSION);
            IndexOutput @out = state.Directory.CreateOutput(fileName, state.Context);
            bool success = false;
            try
            {
                CodecUtil.WriteHeader(@out, CODEC_NAME, VERSION_CURRENT);
                success = true;
            }
            finally
            {
                if (!success)
                {
                    IOUtils.DisposeWhileHandlingException(@out);
                }
            }

            return new FieldsConsumerAnonymousInnerClassHelper(this, @out);
        }

        private class FieldsConsumerAnonymousInnerClassHelper : FieldsConsumer
        {
            private readonly MemoryPostingsFormat outerInstance;

            private IndexOutput @out;

            public FieldsConsumerAnonymousInnerClassHelper(MemoryPostingsFormat outerInstance, IndexOutput @out)
            {
                this.outerInstance = outerInstance;
                this.@out = @out;
            }

            public override TermsConsumer AddField(FieldInfo field)
            {
                //System.out.println("\naddField field=" + field.name);
                return new TermsWriter(@out, field, outerInstance.doPackFST, outerInstance.acceptableOverheadRatio);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    // EOF marker:
                    try
                    {
                        @out.WriteVInt32(0);
                        CodecUtil.WriteFooter(@out);
                    }
                    finally
                    {
                        @out.Dispose();
                    }
                }
            }
        }

        private sealed class FSTDocsEnum : DocsEnum
        {
            private readonly IndexOptions indexOptions;
            private readonly bool storePayloads;
            private byte[] buffer = new byte[16];
            private ByteArrayDataInput @in;

            private IBits liveDocs;
            private int docUpto;
            private int docID_Renamed = -1;
            private int accum;
            private int freq_Renamed;
            private int payloadLen;
            private int numDocs;

            public FSTDocsEnum(IndexOptions indexOptions, bool storePayloads)
            {
                @in = new ByteArrayDataInput(buffer);

                this.indexOptions = indexOptions;
                this.storePayloads = storePayloads;
            }

            public bool CanReuse(IndexOptions indexOptions, bool storePayloads)
            {
                return indexOptions == this.indexOptions && storePayloads == this.storePayloads;
            }

            public FSTDocsEnum Reset(BytesRef bufferIn, IBits liveDocs, int numDocs)
            {
                Debug.Assert(numDocs > 0);
                if (buffer.Length < bufferIn.Length)
                {
                    buffer = ArrayUtil.Grow(buffer, bufferIn.Length);
                }
                @in.Reset(buffer, 0, bufferIn.Length);
                Array.Copy(bufferIn.Bytes, bufferIn.Offset, buffer, 0, bufferIn.Length);
                this.liveDocs = liveDocs;
                docID_Renamed = -1;
                accum = 0;
                docUpto = 0;
                freq_Renamed = 1;
                payloadLen = 0;
                this.numDocs = numDocs;
                return this;
            }

            public override int NextDoc()
            {
                while (true)
                {
                    //System.out.println("  nextDoc cycle docUpto=" + docUpto + " numDocs=" + numDocs + " fp=" + in.getPosition() + " this=" + this);
                    if (docUpto == numDocs)
                    {
                        // System.out.println("    END");
                        return docID_Renamed = NO_MORE_DOCS;
                    }
                    docUpto++;
                    if (indexOptions == IndexOptions.DOCS_ONLY)
                    {
                        accum += @in.ReadVInt32();
                    }
                    else
                    {
                        int code = @in.ReadVInt32();
                        accum += (int)((uint)code >> 1);
                        //System.out.println("  docID=" + accum + " code=" + code);
                        if ((code & 1) != 0)
                        {
                            freq_Renamed = 1;
                        }
                        else
                        {
                            freq_Renamed = @in.ReadVInt32();
                            Debug.Assert(freq_Renamed > 0);
                        }

                        if (indexOptions == IndexOptions.DOCS_AND_FREQS_AND_POSITIONS)
                        {
                            // Skip positions/payloads
                            for (int posUpto = 0; posUpto < freq_Renamed; posUpto++)
                            {
                                if (!storePayloads)
                                {
                                    @in.ReadVInt32();
                                }
                                else
                                {
                                    int posCode = @in.ReadVInt32();
                                    if ((posCode & 1) != 0)
                                    {
                                        payloadLen = @in.ReadVInt32();
                                    }
                                    @in.SkipBytes(payloadLen);
                                }
                            }
                        }
                        else if (indexOptions == IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS)
                        {
                            // Skip positions/offsets/payloads
                            for (int posUpto = 0; posUpto < freq_Renamed; posUpto++)
                            {
                                int posCode = @in.ReadVInt32();
                                if (storePayloads && ((posCode & 1) != 0))
                                {
                                    payloadLen = @in.ReadVInt32();
                                }
                                if ((@in.ReadVInt32() & 1) != 0)
                                {
                                    // new offset length
                                    @in.ReadVInt32();
                                }
                                if (storePayloads)
                                {
                                    @in.SkipBytes(payloadLen);
                                }
                            }
                        }
                    }

                    if (liveDocs == null || liveDocs.Get(accum))
                    {
                        //System.out.println("    return docID=" + accum + " freq=" + freq);
                        return (docID_Renamed = accum);
                    }
                }
            }

            public override int DocID
            {
                get { return docID_Renamed; }
            }

            public override int Advance(int target)
            {
                // TODO: we could make more efficient version, but, it
                // should be rare that this will matter in practice
                // since usually apps will not store "big" fields in
                // this codec!
                return SlowAdvance(target);
            }

            public override int Freq
            {
                get { return freq_Renamed; }
            }

            public override long GetCost()
            {
                return numDocs;
            }
        }

        private sealed class FSTDocsAndPositionsEnum : DocsAndPositionsEnum
        {
            private readonly bool storePayloads;
            private byte[] buffer = new byte[16];
            private ByteArrayDataInput @in;

            private IBits liveDocs;
            private int docUpto;
            private int docID_Renamed = -1;
            private int accum;
            private int freq_Renamed;
            private int numDocs;
            private int posPending;
            private int payloadLength;
            private readonly bool storeOffsets;
            private int offsetLength;
            private int startOffset_Renamed;

            private int pos;
            private readonly BytesRef payload = new BytesRef();

            public FSTDocsAndPositionsEnum(bool storePayloads, bool storeOffsets)
            {
                @in = new ByteArrayDataInput(buffer);

                this.storePayloads = storePayloads;
                this.storeOffsets = storeOffsets;
            }

            public bool CanReuse(bool storePayloads, bool storeOffsets)
            {
                return storePayloads == this.storePayloads && storeOffsets == this.storeOffsets;
            }

            public FSTDocsAndPositionsEnum Reset(BytesRef bufferIn, IBits liveDocs, int numDocs)
            {
                Debug.Assert(numDocs > 0);

                // System.out.println("D&P reset bytes this=" + this);
                // for(int i=bufferIn.offset;i<bufferIn.length;i++) {
                //   System.out.println("  " + Integer.toHexString(bufferIn.bytes[i]&0xFF));
                // }

                if (buffer.Length < bufferIn.Length)
                {
                    buffer = ArrayUtil.Grow(buffer, bufferIn.Length);
                }
                @in.Reset(buffer, 0, bufferIn.Length - bufferIn.Offset);
                Array.Copy(bufferIn.Bytes, bufferIn.Offset, buffer, 0, bufferIn.Length);
                this.liveDocs = liveDocs;
                docID_Renamed = -1;
                accum = 0;
                docUpto = 0;
                payload.Bytes = buffer;
                payloadLength = 0;
                this.numDocs = numDocs;
                posPending = 0;
                startOffset_Renamed = storeOffsets ? 0 : -1; // always return -1 if no offsets are stored
                offsetLength = 0;
                return this;
            }

            public override int NextDoc()
            {
                while (posPending > 0)
                {
                    NextPosition();
                }
                while (true)
                {
                    //System.out.println("  nextDoc cycle docUpto=" + docUpto + " numDocs=" + numDocs + " fp=" + in.getPosition() + " this=" + this);
                    if (docUpto == numDocs)
                    {
                        //System.out.println("    END");
                        return docID_Renamed = NO_MORE_DOCS;
                    }
                    docUpto++;

                    int code = @in.ReadVInt32();
                    accum += (int)((uint)code >> 1);
                    if ((code & 1) != 0)
                    {
                        freq_Renamed = 1;
                    }
                    else
                    {
                        freq_Renamed = @in.ReadVInt32();
                        Debug.Assert(freq_Renamed > 0);
                    }

                    if (liveDocs == null || liveDocs.Get(accum))
                    {
                        pos = 0;
                        startOffset_Renamed = storeOffsets ? 0 : -1;
                        posPending = freq_Renamed;
                        //System.out.println("    return docID=" + accum + " freq=" + freq);
                        return (docID_Renamed = accum);
                    }

                    // Skip positions
                    for (int posUpto = 0; posUpto < freq_Renamed; posUpto++)
                    {
                        if (!storePayloads)
                        {
                            @in.ReadVInt32();
                        }
                        else
                        {
                            int skipCode = @in.ReadVInt32();
                            if ((skipCode & 1) != 0)
                            {
                                payloadLength = @in.ReadVInt32();
                                //System.out.println("    new payloadLen=" + payloadLength);
                            }
                        }

                        if (storeOffsets)
                        {
                            if ((@in.ReadVInt32() & 1) != 0)
                            {
                                // new offset length
                                offsetLength = @in.ReadVInt32();
                            }
                        }

                        if (storePayloads)
                        {
                            @in.SkipBytes(payloadLength);
                        }
                    }
                }
            }

            public override int NextPosition()
            {
                //System.out.println("    nextPos storePayloads=" + storePayloads + " this=" + this);
                Debug.Assert(posPending > 0);
                posPending--;
                if (!storePayloads)
                {
                    pos += @in.ReadVInt32();
                }
                else
                {
                    int code = @in.ReadVInt32();
                    pos += (int)((uint)code >> 1);
                    if ((code & 1) != 0)
                    {
                        payloadLength = @in.ReadVInt32();
                        //System.out.println("      new payloadLen=" + payloadLength);
                        //} else {
                        //System.out.println("      same payloadLen=" + payloadLength);
                    }
                }

                if (storeOffsets)
                {
                    int offsetCode = @in.ReadVInt32();
                    if ((offsetCode & 1) != 0)
                    {
                        // new offset length
                        offsetLength = @in.ReadVInt32();
                    }
                    startOffset_Renamed += (int)((uint)offsetCode >> 1);
                }

                if (storePayloads)
                {
                    payload.Offset = @in.Position;
                    @in.SkipBytes(payloadLength);
                    payload.Length = payloadLength;
                }

                //System.out.println("      pos=" + pos + " payload=" + payload + " fp=" + in.getPosition());
                return pos;
            }

            public override int StartOffset
            {
                get { return startOffset_Renamed; }
            }

            public override int EndOffset
            {
                get { return startOffset_Renamed + offsetLength; }
            }

            public override BytesRef GetPayload()
            {
                return payload.Length > 0 ? payload : null;
            }

            public override int DocID
            {
                get { return docID_Renamed; }
            }

            public override int Advance(int target)
            {
                // TODO: we could make more efficient version, but, it
                // should be rare that this will matter in practice
                // since usually apps will not store "big" fields in
                // this codec!
                return SlowAdvance(target);
            }

            public override int Freq
            {
                get { return freq_Renamed; }
            }

            public override long GetCost()
            {
                return numDocs;
            }
        }

        private sealed class FSTTermsEnum : TermsEnum
        {
            private readonly FieldInfo field;
            private readonly BytesRefFSTEnum<BytesRef> fstEnum;
            private readonly ByteArrayDataInput buffer = new ByteArrayDataInput();
            private bool didDecode;

            private int docFreq_Renamed;
            private long totalTermFreq_Renamed;
            private BytesRefFSTEnum.InputOutput<BytesRef> current;
            private BytesRef postingsSpare = new BytesRef();

            public FSTTermsEnum(FieldInfo field, FST<BytesRef> fst)
            {
                this.field = field;
                fstEnum = new BytesRefFSTEnum<BytesRef>(fst);
            }

            private void DecodeMetaData()
            {
                if (!didDecode)
                {
                    buffer.Reset(current.Output.Bytes, current.Output.Offset, current.Output.Length);
                    docFreq_Renamed = buffer.ReadVInt32();
                    if (field.IndexOptions != IndexOptions.DOCS_ONLY)
                    {
                        totalTermFreq_Renamed = docFreq_Renamed + buffer.ReadVInt64();
                    }
                    else
                    {
                        totalTermFreq_Renamed = -1;
                    }
                    postingsSpare.Bytes = current.Output.Bytes;
                    postingsSpare.Offset = buffer.Position;
                    postingsSpare.Length = current.Output.Length - (buffer.Position - current.Output.Offset);
                    //System.out.println("  df=" + docFreq + " totTF=" + totalTermFreq + " offset=" + buffer.getPosition() + " len=" + current.output.length);
                    didDecode = true;
                }
            }

            public override bool SeekExact(BytesRef text)
            {
                //System.out.println("te.seekExact text=" + field.name + ":" + text.utf8ToString() + " this=" + this);
                current = fstEnum.SeekExact(text);
                didDecode = false;
                return current != null;
            }

            public override SeekStatus SeekCeil(BytesRef text)
            {
                //System.out.println("te.seek text=" + field.name + ":" + text.utf8ToString() + " this=" + this);
                current = fstEnum.SeekCeil(text);
                if (current == null)
                {
                    return SeekStatus.END;
                }
                else
                {

                    // System.out.println("  got term=" + current.input.utf8ToString());
                    // for(int i=0;i<current.output.length;i++) {
                    //   System.out.println("    " + Integer.toHexString(current.output.bytes[i]&0xFF));
                    // }

                    didDecode = false;

                    if (text.Equals(current.Input))
                    {
                        //System.out.println("  found!");
                        return SeekStatus.FOUND;
                    }
                    else
                    {
                        //System.out.println("  not found: " + current.input.utf8ToString());
                        return SeekStatus.NOT_FOUND;
                    }
                }
            }

            public override DocsEnum Docs(IBits liveDocs, DocsEnum reuse, DocsFlags flags)
            {
                DecodeMetaData();
                FSTDocsEnum docsEnum;

                if (reuse == null || !(reuse is FSTDocsEnum))
                {
                    docsEnum = new FSTDocsEnum(field.IndexOptions, field.HasPayloads);
                }
                else
                {
                    docsEnum = (FSTDocsEnum)reuse;
                    if (!docsEnum.CanReuse(field.IndexOptions, field.HasPayloads))
                    {
                        docsEnum = new FSTDocsEnum(field.IndexOptions, field.HasPayloads);
                    }
                }
                return docsEnum.Reset(this.postingsSpare, liveDocs, docFreq_Renamed);
            }

            public override DocsAndPositionsEnum DocsAndPositions(IBits liveDocs, DocsAndPositionsEnum reuse, DocsAndPositionsFlags flags)
            {
                bool hasOffsets = field.IndexOptions.CompareTo(IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS) >= 0;
                if (field.IndexOptions.CompareTo(IndexOptions.DOCS_AND_FREQS_AND_POSITIONS) < 0)
                {
                    return null;
                }
                DecodeMetaData();
                FSTDocsAndPositionsEnum docsAndPositionsEnum;
                if (reuse == null || !(reuse is FSTDocsAndPositionsEnum))
                {
                    docsAndPositionsEnum = new FSTDocsAndPositionsEnum(field.HasPayloads, hasOffsets);
                }
                else
                {
                    docsAndPositionsEnum = (FSTDocsAndPositionsEnum)reuse;
                    if (!docsAndPositionsEnum.CanReuse(field.HasPayloads, hasOffsets))
                    {
                        docsAndPositionsEnum = new FSTDocsAndPositionsEnum(field.HasPayloads, hasOffsets);
                    }
                }
                //System.out.println("D&P reset this=" + this);
                return docsAndPositionsEnum.Reset(postingsSpare, liveDocs, docFreq_Renamed);
            }

            public override BytesRef Term
            {
                get { return current.Input; }
            }

            public override BytesRef Next()
            {
                //System.out.println("te.next");
                current = fstEnum.Next();
                if (current == null)
                {
                    //System.out.println("  END");
                    return null;
                }
                didDecode = false;
                //System.out.println("  term=" + field.name + ":" + current.input.utf8ToString());
                return current.Input;
            }

            public override int DocFreq
            {
                get
                {
                    DecodeMetaData();
                    return docFreq_Renamed;
                }
            }

            public override long TotalTermFreq
            {
                get
                {
                    DecodeMetaData();
                    return totalTermFreq_Renamed;
                }
            }

            public override IComparer<BytesRef> Comparer
            {
                get
                {
                    return BytesRef.UTF8SortedAsUnicodeComparer;
                }
            }

            public override void SeekExact(long ord)
            {
                // NOTE: we could add this...
                throw new System.NotSupportedException();
            }

            public override long Ord
            {
                get
                {
                    // NOTE: we could add this...
                    throw new System.NotSupportedException();
                }
            }
        }

        private sealed class TermsReader : Terms
        {
            private readonly long sumTotalTermFreq;
            private readonly long sumDocFreq;
            private readonly int docCount;
            private readonly int termCount;
            internal FST<BytesRef> fst;
            private readonly ByteSequenceOutputs outputs = ByteSequenceOutputs.Singleton;
            internal readonly FieldInfo field;

            public TermsReader(FieldInfos fieldInfos, IndexInput @in, int termCount)
            {
                this.termCount = termCount;
                int fieldNumber = @in.ReadVInt32();
                field = fieldInfos.FieldInfo(fieldNumber);
                if (field.IndexOptions != IndexOptions.DOCS_ONLY)
                {
                    sumTotalTermFreq = @in.ReadVInt64();
                }
                else
                {
                    sumTotalTermFreq = -1;
                }
                sumDocFreq = @in.ReadVInt64();
                docCount = @in.ReadVInt32();

                fst = new FST<BytesRef>(@in, outputs);
            }

            public override long SumTotalTermFreq
            {
                get
                {
                    return sumTotalTermFreq;
                }
            }

            public override long SumDocFreq
            {
                get
                {
                    return sumDocFreq;
                }
            }

            public override int DocCount
            {
                get
                {
                    return docCount;
                }
            }

            public override long Count
            {
                get { return termCount; }
            }

            public override TermsEnum GetIterator(TermsEnum reuse)
            {
                return new FSTTermsEnum(field, fst);
            }

            public override IComparer<BytesRef> Comparer
            {
                get
                {
                    return BytesRef.UTF8SortedAsUnicodeComparer;
                }
            }

            public override bool HasFreqs
            {
                get { return field.IndexOptions.CompareTo(IndexOptions.DOCS_AND_FREQS) >= 0; }
            }

            public override bool HasOffsets
            {
                get { return field.IndexOptions.CompareTo(IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS) >= 0; }
            }

            public override bool HasPositions
            {
                get { return field.IndexOptions.CompareTo(IndexOptions.DOCS_AND_FREQS_AND_POSITIONS) >= 0; }
            }

            public override bool HasPayloads
            {
                get { return field.HasPayloads; }
            }

            public long RamBytesUsed()
            {
                return ((fst != null) ? fst.GetSizeInBytes() : 0);
            }
        }

        public override FieldsProducer FieldsProducer(SegmentReadState state)
        {
            string fileName = IndexFileNames.SegmentFileName(state.SegmentInfo.Name, state.SegmentSuffix, EXTENSION);
            ChecksumIndexInput @in = state.Directory.OpenChecksumInput(fileName, IOContext.READ_ONCE);

            // LUCENENET specific: Use StringComparer.Ordinal to get the same ordering as Java
            var fields = new SortedDictionary<string, TermsReader>(StringComparer.Ordinal);

            try
            {
                CodecUtil.CheckHeader(@in, CODEC_NAME, VERSION_START, VERSION_CURRENT);
                while (true)
                {
                    int termCount = @in.ReadVInt32();
                    if (termCount == 0)
                    {
                        break;
                    }

                    TermsReader termsReader = new TermsReader(state.FieldInfos, @in, termCount);
                    // System.out.println("load field=" + termsReader.field.name);
                    fields.Add(termsReader.field.Name, termsReader);
                }
                CodecUtil.CheckFooter(@in);
            }
            finally
            {
                @in.Dispose();
            }

            return new FieldsProducerAnonymousInnerClassHelper(this, fields);
        }

        private class FieldsProducerAnonymousInnerClassHelper : FieldsProducer
        {
            private readonly SortedDictionary<string, TermsReader> _fields;

            public FieldsProducerAnonymousInnerClassHelper(MemoryPostingsFormat outerInstance, SortedDictionary<string, TermsReader> fields)
            {
                _fields = fields;
            }

            public override IEnumerator<string> GetEnumerator()
            {
                return _fields.Keys.GetEnumerator(); // LUCENENET NOTE: enumerators are not writable in .NET
            }

            public override Terms GetTerms(string field)
            {
                TermsReader result;
                _fields.TryGetValue(field, out result);
                return result;
            }

            public override int Count
            {
                get
                {
                    return _fields.Count;
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    // Drop ref to FST:
                    foreach (var field in _fields)
                    {
                        field.Value.fst = null;
                    }
                }
            }

            public override long RamBytesUsed()
            {
                long sizeInBytes = 0;
                foreach (var entry in _fields)
                {
                    sizeInBytes += (entry.Key.Length * RamUsageEstimator.NUM_BYTES_CHAR);
                    sizeInBytes += entry.Value.RamBytesUsed();
                }
                return sizeInBytes;
            }

            public override void CheckIntegrity()
            {
            }
        }
    }
}