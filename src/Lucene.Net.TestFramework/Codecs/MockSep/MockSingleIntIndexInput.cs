﻿using Lucene.Net.Codecs.Sep;
using Lucene.Net.Store;

namespace Lucene.Net.Codecs.MockSep
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
    /// Reads IndexInputs written with 
    /// <see cref="MockSingleIntIndexOutput"/>.  NOTE: this class is just for
    /// demonstration purposes(it is a very slow way to read a
    /// block of ints).
    /// <para/>
    /// @lucene.experimental
    /// </summary>
    public class MockSingleIntIndexInput : Int32IndexInput
    {
        private readonly IndexInput @in;

        public MockSingleIntIndexInput(Directory dir, string fileName, IOContext context)
        {
            @in = dir.OpenInput(fileName, context);
            CodecUtil.CheckHeader(@in, MockSingleIntIndexOutput.CODEC,
                          MockSingleIntIndexOutput.VERSION_START,
                          MockSingleIntIndexOutput.VERSION_START);
        }

        public override Reader GetReader()
        {
            return new MockReader((IndexInput)@in.Clone());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                @in.Dispose();
            }
        }

        /**
         * Just reads a vInt directly from the file.
         */
        public class MockReader : Reader
        {
            // clone:
            internal readonly IndexInput @in;

            public MockReader(IndexInput @in)
            {
                this.@in = @in;
            }

            /** Reads next single int */
            public override int Next()
            {
                //System.out.println("msii.next() fp=" + in.getFilePointer() + " vs " + in.length());
                return @in.ReadVInt32();
            }
        }

        internal class MockSingleIntIndexInputIndex : Index
        {
            private long fp;

            public override void Read(DataInput indexIn, bool absolute)
            {
                if (absolute)
                {
                    fp = indexIn.ReadVInt64();
                }
                else
                {
                    fp += indexIn.ReadVInt64();
                }
            }

            public override void CopyFrom(Index other)
            {
                fp = ((MockSingleIntIndexInputIndex)other).fp;
            }

            public override void Seek(Reader other)
            {
                ((MockReader)other).@in.Seek(fp);
            }

            public override string ToString()
            {
                return fp.ToString();
            }


            public override object Clone()
            {
                MockSingleIntIndexInputIndex other = new MockSingleIntIndexInputIndex();
                other.fp = fp;
                return other;
            }
        }
        public override Index GetIndex()
        {
            return new MockSingleIntIndexInputIndex();
        }
    }
}
