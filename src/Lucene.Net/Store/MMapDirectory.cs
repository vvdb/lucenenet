using Lucene.Net.Support;
using Lucene.Net.Support.IO;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace Lucene.Net.Store
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

    using Constants = Lucene.Net.Util.Constants;

    /// <summary>
    /// File-based <see cref="Directory"/> implementation that uses
    /// <see cref="MemoryMappedFile"/> for reading, and
    /// <see cref="FSDirectory.FSIndexOutput"/> for writing.
    ///
    /// <para/><b>NOTE</b>: memory mapping uses up a portion of the
    /// virtual memory address space in your process equal to the
    /// size of the file being mapped.  Before using this class,
    /// be sure your have plenty of virtual address space, e.g. by
    /// using a 64 bit runtime, or a 32 bit runtime with indexes that are
    /// guaranteed to fit within the address space.
    /// On 32 bit platforms also consult <see cref="MMapDirectory(DirectoryInfo, LockFactory, int)"/>
    /// if you have problems with mmap failing because of fragmented
    /// address space. If you get an <see cref="OutOfMemoryException"/>, it is recommended
    /// to reduce the chunk size, until it works.
    /// <para>
    /// <b>NOTE:</b> Accessing this class either directly or
    /// indirectly from a thread while it's interrupted can close the
    /// underlying channel immediately if at the same time the thread is
    /// blocked on IO. The channel will remain closed and subsequent access
    /// to <see cref="MMapDirectory"/> will throw a <see cref="ObjectDisposedException"/>.
    /// </para>
    /// </summary>
    public class MMapDirectory : FSDirectory
    {
        private bool useUnmapHack = UNMAP_SUPPORTED;

        /// <summary>
        /// Default max chunk size. </summary>
        /// <seealso cref="MMapDirectory(DirectoryInfo, LockFactory, int)"/>
        public static readonly int DEFAULT_MAX_BUFF = Constants.RUNTIME_IS_64BIT ? (1 << 30) : (1 << 28);

        private readonly int chunkSizePower;

        /// <summary>
        /// Create a new <see cref="MMapDirectory"/> for the named location.
        /// </summary>
        /// <param name="path"> the path of the directory </param>
        /// <param name="lockFactory"> the lock factory to use, or null for the default
        /// (<see cref="NativeFSLockFactory"/>); </param>
        /// <exception cref="IOException"> if there is a low-level I/O error </exception>
        public MMapDirectory(DirectoryInfo path, LockFactory lockFactory)
            : this(path, lockFactory, DEFAULT_MAX_BUFF)
        {
        }

        /// <summary>
        /// Create a new <see cref="MMapDirectory"/> for the named location and <see cref="NativeFSLockFactory"/>.
        /// </summary>
        /// <param name="path"> the path of the directory </param>
        /// <exception cref="IOException"> if there is a low-level I/O error </exception>
        public MMapDirectory(DirectoryInfo path)
            : this(path, null)
        {
        }

        /// <summary>
        /// Create a new <see cref="MMapDirectory"/> for the named location, specifying the
        /// maximum chunk size used for memory mapping.
        /// </summary>
        /// <param name="path"> the path of the directory </param>
        /// <param name="lockFactory"> the lock factory to use, or <c>null</c> for the default
        /// (<see cref="NativeFSLockFactory"/>); </param>
        /// <param name="maxChunkSize"> maximum chunk size (default is 1 GiBytes for
        /// 64 bit runtimes and 256 MiBytes for 32 bit runtimes) used for memory mapping.
        /// <para/>
        /// Especially on 32 bit platform, the address space can be very fragmented,
        /// so large index files cannot be mapped. Using a lower chunk size makes
        /// the directory implementation a little bit slower (as the correct chunk
        /// may be resolved on lots of seeks) but the chance is higher that mmap
        /// does not fail. On 64 bit platforms, this parameter should always
        /// be <c>1 &lt;&lt; 30</c>, as the address space is big enough.
        /// <para/>
        /// <b>Please note:</b> The chunk size is always rounded down to a power of 2.
        /// </param>
        /// <exception cref="IOException"> if there is a low-level I/O error </exception>
        public MMapDirectory(DirectoryInfo path, LockFactory lockFactory, int maxChunkSize)
            : base(path, lockFactory)
        {
            if (maxChunkSize <= 0)
            {
                throw new System.ArgumentException("Maximum chunk size for mmap must be >0");
            }
            this.chunkSizePower = 31 - Number.NumberOfLeadingZeros(maxChunkSize);
            Debug.Assert(this.chunkSizePower >= 0 && this.chunkSizePower <= 30);
        }

        /// <summary>
        /// Create a new <see cref="MMapDirectory"/> for the named location.
        /// <para/>
        /// LUCENENET specific overload for convenience using string instead of <see cref="DirectoryInfo"/>.
        /// </summary>
        /// <param name="path"> the path of the directory </param>
        /// <param name="lockFactory"> the lock factory to use, or null for the default
        /// (<see cref="NativeFSLockFactory"/>); </param>
        /// <exception cref="IOException"> if there is a low-level I/O error </exception>
        public MMapDirectory(string path, LockFactory lockFactory)
            : this(path, lockFactory, DEFAULT_MAX_BUFF)
        {
        }

        /// <summary>
        /// Create a new <see cref="MMapDirectory"/> for the named location and <see cref="NativeFSLockFactory"/>.
        /// <para/>
        /// LUCENENET specific overload for convenience using string instead of <see cref="DirectoryInfo"/>.
        /// </summary>
        /// <param name="path"> the path of the directory </param>
        /// <exception cref="IOException"> if there is a low-level I/O error </exception>
        public MMapDirectory(string path)
            : this(path, null)
        {
        }

        /// <summary>
        /// Create a new <see cref="MMapDirectory"/> for the named location, specifying the
        /// maximum chunk size used for memory mapping.
        /// <para/>
        /// LUCENENET specific overload for convenience using string instead of <see cref="DirectoryInfo"/>.
        /// </summary>
        /// <param name="path"> the path of the directory </param>
        /// <param name="lockFactory"> the lock factory to use, or <c>null</c> for the default
        /// (<see cref="NativeFSLockFactory"/>); </param>
        /// <param name="maxChunkSize"> maximum chunk size (default is 1 GiBytes for
        /// 64 bit runtimes and 256 MiBytes for 32 bit runtimes) used for memory mapping.
        /// <para/>
        /// Especially on 32 bit platform, the address space can be very fragmented,
        /// so large index files cannot be mapped. Using a lower chunk size makes
        /// the directory implementation a little bit slower (as the correct chunk
        /// may be resolved on lots of seeks) but the chance is higher that mmap
        /// does not fail. On 64 bit platforms, this parameter should always
        /// be <c>1 &lt;&lt; 30</c>, as the address space is big enough.
        /// <para/>
        /// <b>Please note:</b> The chunk size is always rounded down to a power of 2.
        /// </param>
        /// <exception cref="IOException"> if there is a low-level I/O error </exception>
        public MMapDirectory(string path, LockFactory lockFactory, int maxChunkSize)
            : this(new DirectoryInfo(path), lockFactory, maxChunkSize)
        {
        }

        /// <summary>
        /// <c>true</c>, if this platform supports unmapping mmapped files.
        /// </summary>
        // LUCENENET NOTE: Some JREs had a bug that didn't allow them to unmap.
        // But according to MSDN, the MemoryMappedFile.Dispose() method will
        // indeed "release all resources".
        public static readonly bool UNMAP_SUPPORTED = true;

        // LUCENENET specific: No need for static constructor, since unmap is always supported

        /// <summary>
        /// This property enables the workaround for unmapping the buffers
        /// from address space after closing <see cref="IndexInput"/>, that is
        /// mentioned in the bug report. This hack may fail on non-Sun JVMs.
        /// It forcefully unmaps the buffer on close by using
        /// an undocumented internal cleanup functionality.
        /// <para/><b>NOTE:</b> Enabling this is completely unsupported
        /// by Java and may lead to JVM crashes if <see cref="IndexInput"/>
        /// is closed while another thread is still accessing it (SIGSEGV). </summary>
        /// <exception cref="ArgumentException"> if <see cref="UNMAP_SUPPORTED"/>
        /// is <c>false</c> and the workaround cannot be enabled. </exception>
        public virtual bool UseUnmap
        {
            set
            {
                if (value && !UNMAP_SUPPORTED)
                {
                    throw new System.ArgumentException("Unmap hack not supported on this platform!");
                }
                this.useUnmapHack = value;
            }
            get
            {
                return useUnmapHack;
            }
        }

        /// <summary>
        /// Returns the current mmap chunk size. </summary>
        /// <seealso cref="MMapDirectory(DirectoryInfo, LockFactory, int)"/>
        public int MaxChunkSize
        {
            get
            {
                return 1 << chunkSizePower;
            }
        }

        /// <summary>
        /// Creates an <see cref="IndexInput"/> for the file with the given name. </summary>
        public override IndexInput OpenInput(string name, IOContext context)
        {
            EnsureOpen();
            var file = new FileInfo(Path.Combine(Directory.FullName, name));

            var c = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            return new MMapIndexInput(this, "MMapIndexInput(path=\"" + file + "\")", c);
        }

        public override IndexInputSlicer CreateSlicer(string name, IOContext context)
        {
            var full = (MMapIndexInput)OpenInput(name, context);

            return new IndexInputSlicerAnonymousInnerClassHelper(this, full);
        }

        private class IndexInputSlicerAnonymousInnerClassHelper : IndexInputSlicer
        {
            private readonly MMapDirectory outerInstance;

            private MMapIndexInput full;

            public IndexInputSlicerAnonymousInnerClassHelper(MMapDirectory outerInstance, MMapIndexInput full)
            {
                this.outerInstance = outerInstance;
                this.full = full;
            }

            public override IndexInput OpenSlice(string sliceDescription, long offset, long length)
            {
                outerInstance.EnsureOpen();
                return full.Slice(sliceDescription, offset, length);
            }

            [Obsolete("Only for reading CFS files from 3.x indexes.")]
            public override IndexInput OpenFullSlice()
            {
                outerInstance.EnsureOpen();
                return (IndexInput)full.Clone();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    full.Dispose();
                }
            }
        }

        public sealed class MMapIndexInput : ByteBufferIndexInput
        {
            private readonly bool useUnmapHack;
            //private string mapName; // LUCENENET NOTE: Not used
            internal MemoryMappedFile memoryMappedFile; // .NET port: this is equivalent to FileChannel.map
            private readonly MMapDirectory outerInstance;

            internal MMapIndexInput(MMapDirectory outerInstance, string resourceDescription, FileStream fc)
                : base(resourceDescription, null, fc.Length, outerInstance.chunkSizePower, outerInstance.UseUnmap)
            {
                this.outerInstance = outerInstance;
                this.useUnmapHack = outerInstance.UseUnmap;
                this.SetBuffers(outerInstance.Map(this, fc, 0, fc.Length));
            }

            protected override /*sealed*/ void Dispose(bool disposing) // LUCENENET specific - not marked sealed in case subclass needs to dispose
            {
                if (disposing)
                {
                    if (null != this.memoryMappedFile)
                    {
                        this.memoryMappedFile.Dispose();
                        this.memoryMappedFile = null;
                    }
                }
                base.Dispose(disposing);
            }

            /// <summary>
            /// Try to unmap the buffer, this method silently fails if no support
            /// for that in the runtime. On Windows, this leads to the fact,
            /// that mmapped files cannot be modified or deleted.
            /// </summary>
            protected override void FreeBuffer(ByteBuffer buffer)
            {
                // LUCENENET specific: this should free the memory mapped view accessor
                var mmfbb = buffer as MemoryMappedFileByteBuffer;

                if (mmfbb != null)
                    mmfbb.Dispose();

                // LUCENENET specific: no need for UnmapHack
            }
        }

        /// <summary>
        /// Maps a file into a set of buffers </summary>
        internal virtual ByteBuffer[] Map(MMapIndexInput input, FileStream fc, long offset, long length)
        {
            if (Number.URShift(length, chunkSizePower) >= int.MaxValue)
                throw new ArgumentException("RandomAccessFile too big for chunk size: " + fc.ToString());

            // LUCENENET specific: Return empty buffer if length is 0, rather than attempting to create a MemoryMappedFile.
            // Part of a solution provided by Vincent Van Den Berghe: http://apache.markmail.org/message/hafnuhq2ydhfjmi2
            if (length == 0)
            {
                return new[] { ByteBuffer.Allocate(0).AsReadOnlyBuffer() };
            }

            long chunkSize = 1L << chunkSizePower;

            // we always allocate one more buffer, the last one may be a 0 byte one
            int nrBuffers = (int)((long)((ulong)length >> chunkSizePower)) + 1;

            ByteBuffer[] buffers = new ByteBuffer[nrBuffers];

            if (input.memoryMappedFile == null)
            {
                input.memoryMappedFile = MemoryMappedFile.CreateFromFile(
                    fileStream: fc, 
                    mapName: null, 
                    capacity: length, 
                    access: MemoryMappedFileAccess.Read,
#if !NETSTANDARD
                    memoryMappedFileSecurity: null,
#endif
                    inheritability: HandleInheritability.Inheritable, 
                    leaveOpen: false);
            }

            long bufferStart = 0L;
            for (int bufNr = 0; bufNr < nrBuffers; bufNr++)
            {
                int bufSize = (int)((length > (bufferStart + chunkSize)) ? chunkSize : (length - bufferStart));

                // LUCENENET: We get an UnauthorizedAccessException if we create a 0 byte file at the end of the range.
                // See: https://stackoverflow.com/a/5501331
                // We can fix this by moving back 1 byte on the offset if the bufSize is 0.
                int adjust = 0;
                if (bufSize == 0 && bufNr == (nrBuffers - 1) && (offset + bufferStart) > 0)
                {
                    adjust = 1;
                }

                buffers[bufNr] = new MemoryMappedFileByteBuffer(
                    input.memoryMappedFile.CreateViewAccessor(
                        offset: (offset + bufferStart) - adjust, 
                        size: bufSize, 
                        access: MemoryMappedFileAccess.Read), 
                    bufSize);
                bufferStart += bufSize;
            }

            return buffers;
        }
    }
}