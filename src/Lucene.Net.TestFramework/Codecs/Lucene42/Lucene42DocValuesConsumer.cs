using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Lucene.Net.Codecs.Lucene42
{
    using Lucene.Net.Util.Fst;
    using ArrayUtil = Lucene.Net.Util.ArrayUtil;
    using BlockPackedWriter = Lucene.Net.Util.Packed.BlockPackedWriter;
    using ByteArrayDataOutput = Lucene.Net.Store.ByteArrayDataOutput;
    using BytesRef = Lucene.Net.Util.BytesRef;

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

    using FieldInfo = Lucene.Net.Index.FieldInfo;
    using FormatAndBits = Lucene.Net.Util.Packed.PackedInt32s.FormatAndBits;
    using IndexFileNames = Lucene.Net.Index.IndexFileNames;
    using IndexOutput = Lucene.Net.Store.IndexOutput;
    using INPUT_TYPE = Lucene.Net.Util.Fst.FST.INPUT_TYPE;
    using Int32sRef = Lucene.Net.Util.Int32sRef;
    using IOUtils = Lucene.Net.Util.IOUtils;
    using MathUtil = Lucene.Net.Util.MathUtil;
    using MonotonicBlockPackedWriter = Lucene.Net.Util.Packed.MonotonicBlockPackedWriter;
    using PackedInt32s = Lucene.Net.Util.Packed.PackedInt32s;
    using PositiveInt32Outputs = Lucene.Net.Util.Fst.PositiveInt32Outputs;
    using SegmentWriteState = Lucene.Net.Index.SegmentWriteState;
    using Util = Lucene.Net.Util.Fst.Util;

    //   Constants use Lucene42DocValuesProducer.

    /// <summary>
    /// Writer for <seealso cref="Lucene42DocValuesFormat"/>
    /// </summary>
#pragma warning disable 612, 618
    internal class Lucene42DocValuesConsumer : DocValuesConsumer
    {
        internal readonly IndexOutput Data, Meta;
        internal readonly int MaxDoc;
        internal readonly float AcceptableOverheadRatio;

        internal Lucene42DocValuesConsumer(SegmentWriteState state, string dataCodec, string dataExtension, string metaCodec, string metaExtension, float acceptableOverheadRatio)
        {
            this.AcceptableOverheadRatio = acceptableOverheadRatio;
            MaxDoc = state.SegmentInfo.DocCount;
            bool success = false;
            try
            {
                string dataName = IndexFileNames.SegmentFileName(state.SegmentInfo.Name, state.SegmentSuffix, dataExtension);
                Data = state.Directory.CreateOutput(dataName, state.Context);
                // this writer writes the format 4.2 did!
                CodecUtil.WriteHeader(Data, dataCodec, Lucene42DocValuesProducer.VERSION_GCD_COMPRESSION);
                string metaName = IndexFileNames.SegmentFileName(state.SegmentInfo.Name, state.SegmentSuffix, metaExtension);
                Meta = state.Directory.CreateOutput(metaName, state.Context);
                CodecUtil.WriteHeader(Meta, metaCodec, Lucene42DocValuesProducer.VERSION_GCD_COMPRESSION);
                success = true;
            }
            finally
            {
                if (!success)
                {
                    IOUtils.DisposeWhileHandlingException(this);
                }
            }
        }

        public override void AddNumericField(FieldInfo field, IEnumerable<long?> values)
        {
            AddNumericField(field, values, true);
        }

        internal virtual void AddNumericField(FieldInfo field, IEnumerable<long?> values, bool optimizeStorage)
        {
            Meta.WriteVInt32(field.Number);
            Meta.WriteByte((byte)Lucene42DocValuesProducer.NUMBER);
            Meta.WriteInt64(Data.GetFilePointer());
            long minValue = long.MaxValue;
            long maxValue = long.MinValue;
            long gcd = 0;
            // TODO: more efficient?
            HashSet<long> uniqueValues = null;
            if (optimizeStorage)
            {
                uniqueValues = new HashSet<long>();

                long count = 0;
                foreach (long? nv in values)
                {
                    // TODO: support this as MemoryDVFormat (and be smart about missing maybe)
                    long v = nv.GetValueOrDefault();

                    if (gcd != 1)
                    {
                        if (v < long.MinValue / 2 || v > long.MaxValue / 2)
                        {
                            // in that case v - minValue might overflow and make the GCD computation return
                            // wrong results. Since these extreme values are unlikely, we just discard
                            // GCD computation for them
                            gcd = 1;
                        } // minValue needs to be set first
                        else if (count != 0)
                        {
                            gcd = MathUtil.Gcd(gcd, v - minValue);
                        }
                    }

                    minValue = Math.Min(minValue, v);
                    maxValue = Math.Max(maxValue, v);

                    if (uniqueValues != null)
                    {
                        if (uniqueValues.Add(v))
                        {
                            if (uniqueValues.Count > 256)
                            {
                                uniqueValues = null;
                            }
                        }
                    }

                    ++count;
                }
                Debug.Assert(count == MaxDoc);
            }

            if (uniqueValues != null)
            {
                // small number of unique values
                int bitsPerValue = PackedInt32s.BitsRequired(uniqueValues.Count - 1);
                FormatAndBits formatAndBits = PackedInt32s.FastestFormatAndBits(MaxDoc, bitsPerValue, AcceptableOverheadRatio);
                if (formatAndBits.BitsPerValue == 8 && minValue >= sbyte.MinValue && maxValue <= sbyte.MaxValue)
                {
                    Meta.WriteByte((byte)Lucene42DocValuesProducer.UNCOMPRESSED); // uncompressed
                    foreach (long? nv in values)
                    {
                        Data.WriteByte((byte)nv.GetValueOrDefault());
                    }
                }
                else
                {
                    Meta.WriteByte((byte)Lucene42DocValuesProducer.TABLE_COMPRESSED); // table-compressed
                    long[] decode = uniqueValues.ToArray(/*new long?[uniqueValues.Count]*/);
                    var encode = new Dictionary<long, int>();
                    Data.WriteVInt32(decode.Length);
                    for (int i = 0; i < decode.Length; i++)
                    {
                        Data.WriteInt64(decode[i]);
                        encode[decode[i]] = i;
                    }

                    Meta.WriteVInt32(PackedInt32s.VERSION_CURRENT);
                    Data.WriteVInt32(formatAndBits.Format.Id);
                    Data.WriteVInt32(formatAndBits.BitsPerValue);

                    PackedInt32s.Writer writer = PackedInt32s.GetWriterNoHeader(Data, formatAndBits.Format, MaxDoc, formatAndBits.BitsPerValue, PackedInt32s.DEFAULT_BUFFER_SIZE);
                    foreach (long? nv in values)
                    {
                        writer.Add(encode[nv.GetValueOrDefault()]);
                    }
                    writer.Finish();
                }
            }
            else if (gcd != 0 && gcd != 1)
            {
                Meta.WriteByte((byte)Lucene42DocValuesProducer.GCD_COMPRESSED);
                Meta.WriteVInt32(PackedInt32s.VERSION_CURRENT);
                Data.WriteInt64(minValue);
                Data.WriteInt64(gcd);
                Data.WriteVInt32(Lucene42DocValuesProducer.BLOCK_SIZE);

                BlockPackedWriter writer = new BlockPackedWriter(Data, Lucene42DocValuesProducer.BLOCK_SIZE);
                foreach (long? nv in values)
                {
                    writer.Add((nv.GetValueOrDefault() - minValue) / gcd);
                }
                writer.Finish();
            }
            else
            {
                Meta.WriteByte((byte)Lucene42DocValuesProducer.DELTA_COMPRESSED); // delta-compressed

                Meta.WriteVInt32(PackedInt32s.VERSION_CURRENT);
                Data.WriteVInt32(Lucene42DocValuesProducer.BLOCK_SIZE);

                BlockPackedWriter writer = new BlockPackedWriter(Data, Lucene42DocValuesProducer.BLOCK_SIZE);
                foreach (long? nv in values)
                {
                    writer.Add(nv.GetValueOrDefault());
                }
                writer.Finish();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                bool success = false;
                try
                {
                    if (Meta != null)
                    {
                        Meta.WriteVInt32(-1); // write EOF marker
                    }
                    success = true;
                }
                finally
                {
                    if (success)
                    {
                        IOUtils.Dispose(Data, Meta);
                    }
                    else
                    {
                        IOUtils.DisposeWhileHandlingException(Data, Meta);
                    }
                }
            }
        }

        public override void AddBinaryField(FieldInfo field, IEnumerable<BytesRef> values)
        {
            // write the byte[] data
            Meta.WriteVInt32(field.Number);
            Meta.WriteByte((byte)Lucene42DocValuesProducer.BYTES);
            int minLength = int.MaxValue;
            int maxLength = int.MinValue;
            long startFP = Data.GetFilePointer();
            foreach (BytesRef v in values)
            {
                int length = v == null ? 0 : v.Length;
                if (length > Lucene42DocValuesFormat.MAX_BINARY_FIELD_LENGTH)
                {
                    throw new System.ArgumentException("DocValuesField \"" + field.Name + "\" is too large, must be <= " + Lucene42DocValuesFormat.MAX_BINARY_FIELD_LENGTH);
                }
                minLength = Math.Min(minLength, length);
                maxLength = Math.Max(maxLength, length);
                if (v != null)
                {
                    Data.WriteBytes(v.Bytes, v.Offset, v.Length);
                }
            }
            Meta.WriteInt64(startFP);
            Meta.WriteInt64(Data.GetFilePointer() - startFP);
            Meta.WriteVInt32(minLength);
            Meta.WriteVInt32(maxLength);

            // if minLength == maxLength, its a fixed-length byte[], we are done (the addresses are implicit)
            // otherwise, we need to record the length fields...
            if (minLength != maxLength)
            {
                Meta.WriteVInt32(PackedInt32s.VERSION_CURRENT);
                Meta.WriteVInt32(Lucene42DocValuesProducer.BLOCK_SIZE);

                MonotonicBlockPackedWriter writer = new MonotonicBlockPackedWriter(Data, Lucene42DocValuesProducer.BLOCK_SIZE);
                long addr = 0;
                foreach (BytesRef v in values)
                {
                    if (v != null)
                    {
                        addr += v.Length;
                    }
                    writer.Add(addr);
                }
                writer.Finish();
            }
        }

        private void WriteFST(FieldInfo field, IEnumerable<BytesRef> values)
        {
            Meta.WriteVInt32(field.Number);
            Meta.WriteByte((byte)Lucene42DocValuesProducer.FST);
            Meta.WriteInt64(Data.GetFilePointer());
            PositiveInt32Outputs outputs = PositiveInt32Outputs.Singleton;
            Builder<long?> builder = new Builder<long?>(INPUT_TYPE.BYTE1, outputs);
            Int32sRef scratch = new Int32sRef();
            long ord = 0;
            foreach (BytesRef v in values)
            {
                builder.Add(Util.ToInt32sRef(v, scratch), ord);
                ord++;
            }

            var fst = builder.Finish();
            if (fst != null)
            {
                fst.Save(Data);
            }
            Meta.WriteVInt64(ord);
        }

        public override void AddSortedField(FieldInfo field, IEnumerable<BytesRef> values, IEnumerable<long?> docToOrd)
        {
            // three cases for simulating the old writer:
            // 1. no missing
            // 2. missing (and empty string in use): remap ord=-1 -> ord=0
            // 3. missing (and empty string not in use): remap all ords +1, insert empty string into values
            bool anyMissing = false;
            foreach (long? n in docToOrd)
            {
                if (n.Value == -1)
                {
                    anyMissing = true;
                    break;
                }
            }

            bool hasEmptyString = false;
            foreach (BytesRef b in values)
            {
                hasEmptyString = b.Length == 0;
                break;
            }

            if (!anyMissing)
            {
                // nothing to do
            }
            else if (hasEmptyString)
            {
                docToOrd = MissingOrdRemapper.MapMissingToOrd0(docToOrd);
            }
            else
            {
                docToOrd = MissingOrdRemapper.MapAllOrds(docToOrd);
                values = MissingOrdRemapper.InsertEmptyValue(values);
            }

            // write the ordinals as numerics
            AddNumericField(field, docToOrd, false);

            // write the values as FST
            WriteFST(field, values);
        }

        // note: this might not be the most efficient... but its fairly simple
        public override void AddSortedSetField(FieldInfo field, IEnumerable<BytesRef> values, IEnumerable<long?> docToOrdCount, IEnumerable<long?> ords)
        {
            // write the ordinals as a binary field
            AddBinaryField(field, new IterableAnonymousInnerClassHelper(this, docToOrdCount, ords));

            // write the values as FST
            WriteFST(field, values);
        }

        private class IterableAnonymousInnerClassHelper : IEnumerable<BytesRef>
        {
            private readonly Lucene42DocValuesConsumer OuterInstance;

            private IEnumerable<long?> DocToOrdCount;
            private IEnumerable<long?> Ords;

            public IterableAnonymousInnerClassHelper(Lucene42DocValuesConsumer outerInstance, IEnumerable<long?> docToOrdCount, IEnumerable<long?> ords)
            {
                this.OuterInstance = outerInstance;
                this.DocToOrdCount = docToOrdCount;
                this.Ords = ords;
            }

            public IEnumerator<BytesRef> GetEnumerator()
            {
                return new SortedSetIterator(DocToOrdCount.GetEnumerator(), Ords.GetEnumerator());
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        // per-document vint-encoded byte[]
        internal class SortedSetIterator : IEnumerator<BytesRef>
        {
            internal byte[] Buffer = new byte[10];
            internal ByteArrayDataOutput @out = new ByteArrayDataOutput();
            internal BytesRef @ref = new BytesRef();

            internal readonly IEnumerator<long?> Counts;
            internal readonly IEnumerator<long?> Ords;

            internal SortedSetIterator(IEnumerator<long?> counts, IEnumerator<long?> ords)
            {
                this.Counts = counts;
                this.Ords = ords;
            }

            public bool MoveNext()
            {
                if (!Counts.MoveNext())
                {
                    return false;
                }

                int count = (int)Counts.Current;
                int maxSize = count * 9; //worst case
                if (maxSize > Buffer.Length)
                {
                    Buffer = ArrayUtil.Grow(Buffer, maxSize);
                }

                try
                {
                    EncodeValues(count);
                }
                catch (IOException bogus)
                {
                    throw new Exception(bogus.ToString(), bogus);
                }

                @ref.Bytes = Buffer;
                @ref.Offset = 0;
                @ref.Length = @out.Position;

                return true;
            }

            public BytesRef Current
            {
                get { return @ref; }
            }

            object System.Collections.IEnumerator.Current
            {
                get { return Current; }
            }

            // encodes count values to buffer
            internal virtual void EncodeValues(int count)
            {
                @out.Reset(Buffer);
                long lastOrd = 0;
                for (int i = 0; i < count; i++)
                {
                    Ords.MoveNext();
                    long ord = Ords.Current.Value;
                    @out.WriteVInt64(ord - lastOrd);
                    lastOrd = ord;
                }
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
            }
        }
    }
#pragma warning restore 612, 618
}