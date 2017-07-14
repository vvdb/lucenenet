using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Index;
using Lucene.Net.Util;
using System;
using System.IO;
using System.Text;

namespace Lucene.Net.Documents
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
    /// Expert: directly create a field for a document.  Most
    /// users should use one of the sugar subclasses: <see cref="Int32Field"/>, 
    /// <see cref="Int64Field"/>, <see cref="SingleField"/>, <see cref="DoubleField"/>, 
    /// <see cref="BinaryDocValuesField"/>, <see cref="NumericDocValuesField"/>,
    /// <see cref="SortedDocValuesField"/>, <see cref="StringField"/>,
    /// <see cref="TextField"/>, <see cref="StoredField"/>.
    ///
    /// <para/> A field is a section of a <see cref="Document"/>. Each field has three
    /// parts: name, type and value. Values may be text
    /// (<see cref="string"/>, <see cref="TextReader"/> or pre-analyzed <see cref="TokenStream"/>), binary
    /// (<see cref="T:byte[]"/>), or numeric (<see cref="int"/>, <see cref="long"/>, <see cref="float"/>, or <see cref="double"/>). 
    /// Fields are optionally stored in the
    /// index, so that they may be returned with hits on the document.
    ///
    /// <para/>
    /// NOTE: the field type is an <see cref="IIndexableFieldType"/>.  Making changes
    /// to the state of the <see cref="IIndexableFieldType"/> will impact any
    /// Field it is used in.  It is strongly recommended that no
    /// changes be made after <see cref="Field"/> instantiation.
    /// </summary>
#if FEATURE_SERIALIZABLE
    [Serializable]
#endif
    public class Field : IIndexableField
    {
        /// <summary>
        /// Field's type
        /// </summary>
        protected readonly FieldType m_type;

        /// <summary>
        /// Field's name
        /// </summary>
        protected readonly string m_name;

        /// <summary>
        /// Field's value </summary>
        protected object m_fieldsData;

        /// <summary>
        /// Pre-analyzed <see cref="TokenStream"/> for indexed fields; this is
        /// separate from <see cref="m_fieldsData"/> because you are allowed to
        /// have both; eg maybe field has a <see cref="string"/> value but you
        /// customize how it's tokenized
        /// </summary>
        protected TokenStream m_tokenStream;

        private TokenStream internalTokenStream;

        /// <summary>
        /// Field's boost </summary>
        /// <seealso cref="Boost"/>
        protected float m_boost = 1.0f;

        /// <summary>
        /// Expert: creates a field with no initial value.
        /// Intended only for custom <see cref="Field"/> subclasses.
        /// </summary>
        /// <param name="name"> field name </param>
        /// <param name="type"> field type </param>
        /// <exception cref="ArgumentNullException"> if either the <paramref name="name"/> or <paramref name="type"/>
        ///         is <c>null</c>. </exception>
        protected internal Field(string name, FieldType type)
        {
            if (name == null)
            {
                throw new System.ArgumentNullException("name", "name cannot be null");
            }
            if (type == null)
            {
                throw new System.ArgumentNullException("type", "type cannot be null");
            }
            this.m_name = name;
            this.m_type = type;
        }

        /// <summary>
        /// Create field with <see cref="TextReader"/> value. </summary>
        /// <param name="name"> field name </param>
        /// <param name="reader"> reader value </param>
        /// <param name="type"> field type </param>
        /// <exception cref="ArgumentException"> if <see cref="FieldType.IsStored"/> is true, or
        ///         if <see cref="FieldType.IsTokenized"/> is false. </exception>
        /// <exception cref="ArgumentNullException"> if either the <paramref name="name"/> or <paramref name="type"/>
        ///         is <c>null</c>, or if the reader is <c>null</c> </exception>
        public Field(string name, TextReader reader, FieldType type)
        {
            if (name == null)
            {
                throw new System.ArgumentNullException("name", "name cannot be null");
            }
            if (type == null)
            {
                throw new System.ArgumentNullException("type", "type cannot be null");
            }
            if (reader == null)
            {
                throw new System.ArgumentNullException("reader", "reader cannot be null");
            }
            if (type.IsStored)
            {
                throw new System.ArgumentException("fields with a Reader value cannot be stored");
            }
            if (type.IsIndexed && !type.IsTokenized)
            {
                throw new System.ArgumentException("non-tokenized fields must use String values");
            }

            this.m_name = name;
            this.m_fieldsData = reader;
            this.m_type = type;
        }

        /// <summary>
        /// Create field with <see cref="TokenStream"/> value. </summary>
        /// <param name="name"> field name </param>
        /// <param name="tokenStream"> TokenStream value </param>
        /// <param name="type"> field type </param>
        /// <exception cref="ArgumentException"> if <see cref="FieldType.IsStored"/> is true, or
        ///         if <see cref="FieldType.IsTokenized"/> is false, or if <see cref="FieldType.IsIndexed"/> is false. </exception>
        /// <exception cref="ArgumentNullException"> if either the <paramref name="name"/> or <paramref name="type"/>
        ///         is <c>null</c>, or if the <paramref name="tokenStream"/> is <c>null</c> </exception>
        public Field(string name, TokenStream tokenStream, FieldType type)
        {
            if (name == null)
            {
                throw new System.ArgumentNullException("name", "name cannot be null");
            }
            if (tokenStream == null)
            {
                throw new System.ArgumentNullException("tokenStream", "tokenStream cannot be null");
            }
            if (type == null)
            {
                throw new System.ArgumentNullException("type", "type cannot be null");
            }
            if (!type.IsIndexed || !type.IsTokenized)
            {
                throw new System.ArgumentException("TokenStream fields must be indexed and tokenized");
            }
            if (type.IsStored)
            {
                throw new System.ArgumentException("TokenStream fields cannot be stored");
            }

            this.m_name = name;
            this.m_fieldsData = null;
            this.m_tokenStream = tokenStream;
            this.m_type = type;
        }

        /// <summary>
        /// Create field with binary value.
        ///
        /// <para/>NOTE: the provided <see cref="T:byte[]"/> is not copied so be sure
        /// not to change it until you're done with this field.
        /// </summary>
        /// <param name="name"> field name </param>
        /// <param name="value"> byte array pointing to binary content (not copied) </param>
        /// <param name="type"> field type </param>
        /// <exception cref="ArgumentException"> if the <see cref="FieldType.IsIndexed"/> is true </exception>
        /// <exception cref="ArgumentNullException"> the field <paramref name="name"/> is <c>null</c>,
        ///         or if the <paramref name="type"/> is <c>null</c> </exception>
        public Field(string name, byte[] value, FieldType type)
            : this(name, value, 0, value.Length, type)
        {
        }

        /// <summary>
        /// Create field with binary value.
        ///
        /// <para/>NOTE: the provided <see cref="T:byte[]"/> is not copied so be sure
        /// not to change it until you're done with this field.
        /// </summary>
        /// <param name="name"> field name </param>
        /// <param name="value"> byte array pointing to binary content (not copied) </param>
        /// <param name="offset"> starting position of the byte array </param>
        /// <param name="length"> valid length of the byte array </param>
        /// <param name="type"> field type </param>
        /// <exception cref="ArgumentException"> if the <see cref="FieldType.IsIndexed"/> is true </exception>
        /// <exception cref="ArgumentNullException"> if the field <paramref name="name"/> is <c>null</c>,
        ///         or the <paramref name="type"/> is <c>null</c> </exception>
        public Field(string name, byte[] value, int offset, int length, FieldType type)
            : this(name, new BytesRef(value, offset, length), type)
        {
        }

        /// <summary>
        /// Create field with binary value.
        ///
        /// <para/>NOTE: the provided BytesRef is not copied so be sure
        /// not to change it until you're done with this field.
        /// </summary>
        /// <param name="name"> field name </param>
        /// <param name="bytes"> BytesRef pointing to binary content (not copied) </param>
        /// <param name="type"> field type </param>
        /// <exception cref="ArgumentException"> if the <see cref="FieldType.IsIndexed"/> is true </exception>
        /// <exception cref="ArgumentNullException"> if the field <paramref name="name"/> is <c>null</c>,
        ///         or the <paramref name="type"/> is <c>null</c> </exception>
        public Field(string name, BytesRef bytes, FieldType type)
        {
            if (name == null)
            {
                throw new System.ArgumentNullException("name", "name cannot be null");
            }
            if (type == null)
            {
                throw new System.ArgumentNullException("type", "type cannot be null");
            }
            if (type.IsIndexed)
            {
                throw new System.ArgumentException("Fields with BytesRef values cannot be indexed");
            }
            this.m_fieldsData = bytes;
            this.m_type = type;
            this.m_name = name;
        }

        // TODO: allow direct construction of int, long, float, double value too..?

        /// <summary>
        /// Create field with <see cref="string"/> value. </summary>
        /// <param name="name"> field name </param>
        /// <param name="value"> string value </param>
        /// <param name="type"> field type </param>
        /// <exception cref="ArgumentException"> if the field's type is neither indexed() nor stored(),
        ///         or if indexed() is false but storeTermVectors() is true. </exception>
        /// <exception cref="ArgumentNullException"> if either the <paramref name="name"/> or <paramref name="value"/>
        ///         is <c>null</c>, or if the <paramref name="type"/> is <c>null</c> </exception>
        public Field(string name, string value, FieldType type)
        {
            if (name == null)
            {
                throw new System.ArgumentNullException("name", "name cannot be null");
            }
            if (value == null)
            {
                throw new System.ArgumentNullException("value", "value cannot be null");
            }
            if (type == null)
            {
                throw new System.ArgumentNullException("type", "type cannot be null");
            }
            if (!type.IsStored && !type.IsIndexed)
            {
                throw new System.ArgumentException("it doesn't make sense to have a field that " + "is neither indexed nor stored");
            }
            if (!type.IsIndexed && (type.StoreTermVectors))
            {
                throw new System.ArgumentException("cannot store term vector information " + "for a field that is not indexed");
            }

            this.m_type = type;
            this.m_name = name;
            this.m_fieldsData = value;
        }

        /// <summary>
        /// The value of the field as a <see cref="string"/>, or <c>null</c>. If <c>null</c>, the <see cref="TextReader"/> value or
        /// binary value is used. Exactly one of <see cref="GetStringValue()"/>, <see cref="GetReaderValue()"/>, and
        /// <see cref="GetBinaryValue()"/> must be set.
        /// </summary>
        public virtual string GetStringValue() // LUCENENET specific: Added verb Get to make it more clear that this returns the value
        {
            if (m_fieldsData is string || m_fieldsData is int || m_fieldsData is float || m_fieldsData is double || m_fieldsData is long)
            {
                return m_fieldsData.ToString();
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// The value of the field as a <see cref="TextReader"/>, or <c>null</c>. If <c>null</c>, the <see cref="string"/> value or
        /// binary value is used. Exactly one of <see cref="GetStringValue()"/>, <see cref="GetReaderValue()"/>, and
        /// <see cref="GetBinaryValue()"/> must be set.
        /// </summary>
        public virtual TextReader GetReaderValue() // LUCENENET specific: Added verb Get to make it more clear that this returns the value
        {
            return m_fieldsData is TextReader ? (TextReader)m_fieldsData : null;
        }

        /// <summary>
        /// The <see cref="TokenStream"/> for this field to be used when indexing, or <c>null</c>. If <c>null</c>,
        /// the <see cref="TextReader"/> value or <see cref="string"/> value is analyzed to produce the indexed tokens.
        /// </summary>
        public virtual TokenStream GetTokenStreamValue() // LUCENENET specific: Added verb Get to make it more clear that this returns the value
        {
            return m_tokenStream;
        }

        /// <summary>
        /// <para>
        /// Expert: change the value of this field. This can be used during indexing to
        /// re-use a single <see cref="Field"/> instance to improve indexing speed by avoiding GC
        /// cost of new'ing and reclaiming <see cref="Field"/> instances. Typically a single
        /// <see cref="Document"/> instance is re-used as well. This helps most on small
        /// documents.
        /// </para>
        ///
        /// <para>
        /// Each <see cref="Field"/> instance should only be used once within a single
        /// <see cref="Document"/> instance. See <a
        /// href="http://wiki.apache.org/lucene-java/ImproveIndexingSpeed"
        /// >ImproveIndexingSpeed</a> for details.
        /// </para>
        /// </summary>
        public virtual void SetStringValue(string value)
        {
            if (!(m_fieldsData is string))
            {
                throw new ArgumentException("cannot change value type from " + m_fieldsData.GetType().Name + " to string");
            }
            m_fieldsData = value;
        }

        /// <summary>
        /// Expert: change the value of this field. See 
        /// <see cref="SetStringValue(string)"/>.
        /// </summary>
        public virtual void SetReaderValue(TextReader value)
        {
            if (!(m_fieldsData is TextReader))
            {
                throw new ArgumentException("cannot change value type from " + m_fieldsData.GetType().Name + " to TextReader");
            }
            m_fieldsData = value;
        }

        /// <summary>
        /// Expert: change the value of this field. See
        /// <see cref="SetStringValue(string)"/>.
        ///
        /// <para/>NOTE: the provided <see cref="BytesRef"/> is not copied so be sure
        /// not to change it until you're done with this field.
        /// </summary>
        public virtual void SetBytesValue(BytesRef value)
        {
            if (!(m_fieldsData is BytesRef))
            {
                throw new System.ArgumentException("cannot change value type from " + m_fieldsData.GetType().Name + " to BytesRef");
            }
            if (m_type.IsIndexed)
            {
                throw new System.ArgumentException("cannot set a BytesRef value on an indexed field");
            }
            m_fieldsData = value;
        }

        /// <summary>
        /// Expert: change the value of this field. See
        /// <see cref="SetStringValue(string)"/>.
        /// </summary>
        public virtual void SetBytesValue(byte[] value)
        {
            SetBytesValue(new BytesRef(value));
        }

        /// <summary>
        /// Expert: change the value of this field. See
        /// <see cref="SetStringValue(string)"/>.
        /// </summary>
        public virtual void SetByteValue(byte value)
        {
            if (!(m_fieldsData is byte?))
            {
                throw new System.ArgumentException("cannot change value type from " + m_fieldsData.GetType().Name + " to Byte");
            }
            m_fieldsData = Convert.ToByte(value);
        }

        /// <summary>
        /// Expert: change the value of this field. See
        /// <see cref="SetStringValue(string)"/>.
        /// </summary>
        public virtual void SetInt16Value(short value) // LUCENENET specific: Renamed from SetShortValue to follow .NET conventions
        {
            if (!(m_fieldsData is short?))
            {
                throw new System.ArgumentException("cannot change value type from " + m_fieldsData.GetType().Name + " to Short");
            }
            m_fieldsData = Convert.ToInt16(value);
        }

        /// <summary>
        /// Expert: change the value of this field. See
        /// <see cref="SetStringValue(string)"/>.
        /// </summary>
        public virtual void SetInt32Value(int value) // LUCENENET specific: Renamed from SetIntValue to follow .NET conventions
        {
            if (!(m_fieldsData is int?))
            {
                throw new System.ArgumentException("cannot change value type from " + m_fieldsData.GetType().Name + " to Integer");
            }
            m_fieldsData = Convert.ToInt32(value);
        }

        /// <summary>
        /// Expert: change the value of this field. See
        /// <see cref="SetStringValue(string)"/>.
        /// </summary>
        public virtual void SetInt64Value(long value) // LUCENENET specific: Renamed from SetLongValue to follow .NET conventions
        {
            if (!(m_fieldsData is long?))
            {
                throw new System.ArgumentException("cannot change value type from " + m_fieldsData.GetType().Name + " to Long");
            }
            m_fieldsData = Convert.ToInt64(value);
        }

        /// <summary>
        /// Expert: change the value of this field. See
        /// <see cref="SetStringValue(string)"/>.
        /// </summary>
        public virtual void SetSingleValue(float value) // LUCENENET specific: Renamed from SetFloatValue to follow .NET conventions
        {
            if (!(m_fieldsData is float?))
            {
                throw new System.ArgumentException("cannot change value type from " + m_fieldsData.GetType().Name + " to Float");
            }
            m_fieldsData = Convert.ToSingle(value);
        }

        /// <summary>
        /// Expert: change the value of this field. See
        /// <see cref="SetStringValue(string)"/>.
        /// </summary>
        public virtual void SetDoubleValue(double value)
        {
            if (!(m_fieldsData is double?))
            {
                throw new System.ArgumentException("cannot change value type from " + m_fieldsData.GetType().Name + " to Double");
            }
            m_fieldsData = Convert.ToDouble(value);
        }

        // LUCENENET TODO: Add SetValue() overloads for each type?
        // Upside: Simpler API.
        // Downside: Must be vigilant about what type is passed or the wrong overload will be called and will get a runtime exception. 

        /// <summary>
        /// Expert: sets the token stream to be used for indexing and causes
        /// <see cref="FieldType.IsIndexed"/> and <see cref="FieldType.IsTokenized"/> to return true. May be combined with stored
        /// values from <see cref="GetStringValue()"/> or <see cref="GetBinaryValue()"/>
        /// </summary>
        public virtual void SetTokenStream(TokenStream tokenStream)
        {
            if (!m_type.IsIndexed || !m_type.IsTokenized)
            {
                throw new System.ArgumentException("TokenStream fields must be indexed and tokenized");
            }
            if (m_type.NumericType != NumericType.NONE)
            {
                throw new System.ArgumentException("cannot set private TokenStream on numeric fields");
            }
            this.m_tokenStream = tokenStream;
        }

        /// <summary>
        /// The field's name
        /// </summary>
        public virtual string Name
        {
            get { return m_name; }
        }

        /// <summary>
        /// Gets or sets the boost factor on this field.
        /// </summary>
        /// <remarks>The default value is <c>1.0f</c> (no boost).</remarks>
        /// <exception cref="ArgumentException"> (setter only) if this field is not indexed,
        ///         or if it omits norms. </exception>
        public virtual float Boost
        {
            get
            {
                return m_boost;
            }
            set
            {
                if (value != 1.0f)
                {
                    if (m_type.IsIndexed == false || m_type.OmitNorms)
                    {
                        throw new System.ArgumentException("You cannot set an index-time boost on an unindexed field, or one that omits norms");
                    }
                }
                this.m_boost = value;
            }
        }

        public virtual object GetNumericValue() // LUCENENET specific: Added verb Get to make it more clear that this returns the value
        {
            // LUCENENET NOTE: Originally, there was a conversion from string to a numeric value here.
            // This was causing the Lucene.Net.Documents.TestLazyDocument.TestLazy() test (in Lucene.Net.Tests.Misc) to fail.
            // It is important that if numeric data is provided as a string to the field that it remains a string or the
            // wrong StoredFieldsVisitor method will be called (in this case it was calling Int64Field() instead of StringField()).
            // This is an extremely difficult thing to track down and very confusing to end users.

            if (m_fieldsData is int || m_fieldsData is float || m_fieldsData is double || m_fieldsData is long)
            {
                return m_fieldsData;
            }

            return null;
        }

        public virtual BytesRef GetBinaryValue() // LUCENENET specific: Added verb Get to make it more clear that this returns the value
        {
            if (m_fieldsData is BytesRef)
            {
                return (BytesRef)m_fieldsData;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Prints a <see cref="Field"/> for human consumption. </summary>
        public override string ToString()
        {
            StringBuilder result = new StringBuilder();
            result.Append(m_type.ToString());
            result.Append('<');
            result.Append(m_name);
            result.Append(':');

            if (m_fieldsData != null)
            {
                result.Append(m_fieldsData);
            }

            result.Append('>');
            return result.ToString();
        }

        /// <summary>
        /// Returns the <see cref="Documents.FieldType"/> for this field. </summary>
        public virtual IIndexableFieldType FieldType
        {
            get { return m_type; }
        }

        public virtual TokenStream GetTokenStream(Analyzer analyzer)
        {
            if (!((FieldType)FieldType).IsIndexed)
            {
                return null;
            }
            NumericType numericType = ((FieldType)FieldType).NumericType;
            if (numericType != NumericType.NONE)
            {
                if (!(internalTokenStream is NumericTokenStream))
                {
                    // lazy init the TokenStream as it is heavy to instantiate
                    // (attributes,...) if not needed (stored field loading)
                    internalTokenStream = new NumericTokenStream(m_type.NumericPrecisionStep);
                }
                var nts = (NumericTokenStream)internalTokenStream;
                // initialize value in TokenStream
                object val = m_fieldsData;
                switch (numericType)
                {
                    case NumericType.INT32:
                        nts.SetInt32Value(Convert.ToInt32(val));
                        break;

                    case NumericType.INT64:
                        nts.SetInt64Value(Convert.ToInt64(val));
                        break;

                    case NumericType.SINGLE:
                        nts.SetSingleValue(Convert.ToSingle(val));
                        break;

                    case NumericType.DOUBLE:
                        nts.SetDoubleValue(Convert.ToDouble(val));
                        break;

                    default:
                        throw new Exception("Should never get here");
                }
                return internalTokenStream;
            }

            if (!((FieldType)FieldType).IsTokenized)
            {
                if (GetStringValue() == null)
                {
                    throw new System.ArgumentException("Non-Tokenized Fields must have a String value");
                }
                if (!(internalTokenStream is StringTokenStream))
                {
                    // lazy init the TokenStream as it is heavy to instantiate
                    // (attributes,...) if not needed (stored field loading)
                    internalTokenStream = new StringTokenStream();
                }
                ((StringTokenStream)internalTokenStream).SetValue(GetStringValue());
                return internalTokenStream;
            }

            if (m_tokenStream != null)
            {
                return m_tokenStream;
            }
            else if (GetReaderValue() != null)
            {
                return analyzer.GetTokenStream(Name, GetReaderValue());
            }
            else if (GetStringValue() != null)
            {
                TextReader sr = new StringReader(GetStringValue());
                return analyzer.GetTokenStream(Name, sr);
            }

            throw new System.ArgumentException("Field must have either TokenStream, String, Reader or Number value; got " + this);
        }

        internal sealed class StringTokenStream : TokenStream
        {
            internal void InitializeInstanceFields()
            {
                termAttribute = AddAttribute<ICharTermAttribute>();
                offsetAttribute = AddAttribute<IOffsetAttribute>();
            }

            internal ICharTermAttribute termAttribute;
            internal IOffsetAttribute offsetAttribute;
            internal bool used = false;
            internal string value = null;

            /// <summary>
            /// Creates a new <see cref="TokenStream"/> that returns a <see cref="string"/> as single token.
            /// <para/>Warning: Does not initialize the value, you must call
            /// <see cref="SetValue(string)"/> afterwards!
            /// </summary>
            internal StringTokenStream()
            {
                InitializeInstanceFields();
            }

            /// <summary>
            /// Sets the string value. </summary>
            internal void SetValue(string value)
            {
                this.value = value;
            }

            public override bool IncrementToken()
            {
                if (used)
                {
                    return false;
                }
                ClearAttributes();
                termAttribute.Append(value);
                offsetAttribute.SetOffset(0, value.Length);
                used = true;
                return true;
            }

            public override void End()
            {
                base.End();
                int finalOffset = value.Length;
                offsetAttribute.SetOffset(finalOffset, finalOffset);
            }

            public override void Reset()
            {
                used = false;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    value = null;
                }
            }
        }

        /// <summary>
        /// Specifies whether and how a field should be stored. </summary>
        public enum Store
        {
            /// <summary>
            /// Store the original field value in the index. this is useful for short texts
            /// like a document's title which should be displayed with the results. The
            /// value is stored in its original form, i.e. no analyzer is used before it is
            /// stored.
            /// </summary>
            YES,

            /// <summary>
            /// Do not store the field's value in the index. </summary>
            NO
        }

        //
        // Deprecated transition API below:
        //

        /// <summary>
        /// Specifies whether and how a field should be indexed. 
        /// </summary>
        [Obsolete("This is here only to ease transition from the pre-4.0 APIs.")]
        public enum Index
        {
            /// <summary>Do not index the field value. This field can thus not be searched,
            /// but one can still access its contents provided it is
            /// <see cref="Field.Store">stored</see>.
            /// </summary>
            NO,

            /// <summary>Index the tokens produced by running the field's
            /// value through an <see cref="Analyzer"/>.  This is useful for
            /// common text.
            /// </summary>
            ANALYZED,

            /// <summary>Index the field's value without using an <see cref="Analyzer"/>, so it can be searched.
            /// As no analyzer is used the value will be stored as a single term. This is
            /// useful for unique Ids like product numbers.
            /// </summary>
            NOT_ANALYZED,

            /// <summary>Expert: Index the field's value without an Analyzer,
            /// and also disable the storing of norms.  Note that you
            /// can also separately enable/disable norms by setting
            /// <see cref="FieldType.OmitNorms" />.  No norms means that
            /// index-time field and document boosting and field
            /// length normalization are disabled.  The benefit is
            /// less memory usage as norms take up one byte of RAM
            /// per indexed field for every document in the index,
            /// during searching.  Note that once you index a given
            /// field <i>with</i> norms enabled, disabling norms will
            /// have no effect.  In other words, for this to have the
            /// above described effect on a field, all instances of
            /// that field must be indexed with NOT_ANALYZED_NO_NORMS
            /// from the beginning.
            /// </summary>
            NOT_ANALYZED_NO_NORMS,

            /// <summary>Expert: Index the tokens produced by running the
            /// field's value through an Analyzer, and also
            /// separately disable the storing of norms.  See
            /// <see cref="NOT_ANALYZED_NO_NORMS" /> for what norms are
            /// and why you may want to disable them.
            /// </summary>
            ANALYZED_NO_NORMS,
        }

        /// <summary>
        /// Specifies whether and how a field should have term vectors. 
        /// </summary>
        [Obsolete("This is here only to ease transition from the pre-4.0 APIs.")]
        public enum TermVector
        {
            /// <summary>
            /// Do not store term vectors. 
            /// </summary>
            NO,

            /// <summary>
            /// Store the term vectors of each document. A term vector is a list
            /// of the document's terms and their number of occurrences in that document.
            /// </summary>
            YES,

            /// <summary>
            /// Store the term vector + token position information
            /// </summary>
            /// <seealso cref="YES"/>
            WITH_POSITIONS,

            /// <summary>
            /// Store the term vector + Token offset information
            /// </summary>
            /// <seealso cref="YES"/>
            WITH_OFFSETS,

            /// <summary>
            /// Store the term vector + Token position and offset information
            /// </summary>
            /// <seealso cref="YES"/>
            /// <seealso cref="WITH_POSITIONS"/>
            /// <seealso cref="WITH_OFFSETS"/>
            WITH_POSITIONS_OFFSETS,
        }

        /// <summary>
        /// Translates the pre-4.0 enums for specifying how a
        /// field should be indexed into the 4.0 <see cref="Documents.FieldType"/>
        /// approach.
        /// </summary>
        [Obsolete("This is here only to ease transition from the pre-4.0 APIs.")]
        public static FieldType TranslateFieldType(Store store, Index index, TermVector termVector)
        {
            FieldType ft = new FieldType();

            ft.IsStored = store == Store.YES;

            switch (index)
            {
                case Index.ANALYZED:
                    ft.IsIndexed = true;
                    ft.IsTokenized = true;
                    break;

                case Index.ANALYZED_NO_NORMS:
                    ft.IsIndexed = true;
                    ft.IsTokenized = true;
                    ft.OmitNorms = true;
                    break;

                case Index.NOT_ANALYZED:
                    ft.IsIndexed = true;
                    ft.IsTokenized = false;
                    break;

                case Index.NOT_ANALYZED_NO_NORMS:
                    ft.IsIndexed = true;
                    ft.IsTokenized = false;
                    ft.OmitNorms = true;
                    break;

                case Index.NO:
                    break;
            }

            switch (termVector)
            {
                case TermVector.NO:
                    break;

                case TermVector.YES:
                    ft.StoreTermVectors = true;
                    break;

                case TermVector.WITH_POSITIONS:
                    ft.StoreTermVectors = true;
                    ft.StoreTermVectorPositions = true;
                    break;

                case TermVector.WITH_OFFSETS:
                    ft.StoreTermVectors = true;
                    ft.StoreTermVectorOffsets = true;
                    break;

                case TermVector.WITH_POSITIONS_OFFSETS:
                    ft.StoreTermVectors = true;
                    ft.StoreTermVectorPositions = true;
                    ft.StoreTermVectorOffsets = true;
                    break;
            }
            ft.Freeze();
            return ft;
        }

        /// <summary>
        /// Create a field by specifying its <paramref name="name"/>, <paramref name="value"/> and how it will
        /// be saved in the index. Term vectors will not be stored in the index.
        /// </summary>
        /// <param name="name">The name of the field</param>
        /// <param name="value">The string to process</param>
        /// <param name="store">Whether <paramref name="value"/> should be stored in the index</param>
        /// <param name="index">Whether the field should be indexed, and if so, if it should
        /// be tokenized before indexing</param>
        /// <exception cref="ArgumentNullException">if <paramref name="name"/> or <paramref name="value"/> is <c>null</c></exception>
        /// <exception cref="ArgumentException">if the field is neither stored nor indexed</exception>
        [Obsolete("Use StringField, TextField instead.")]
        public Field(string name, string value, Store store, Index index)
            : this(name, value, TranslateFieldType(store, index, TermVector.NO))
        {
        }

        /// <summary>
        /// Create a field by specifying its <paramref name="name"/>, <paramref name="value"/> and how it will
        /// be saved in the index.
        /// </summary>
        /// <param name="name">The name of the field</param>
        /// <param name="value">The string to process</param>
        /// <param name="store">Whether <paramref name="value"/> should be stored in the index</param>
        /// <param name="index">Whether the field should be indexed, and if so, if it should
        /// be tokenized before indexing</param>
        /// <param name="termVector">Whether term vector should be stored</param>
        /// <exception cref="ArgumentNullException">if <paramref name="name"/> or <paramref name="value"/> is <c>null</c></exception>
        /// <exception cref="ArgumentException">in any of the following situations:
        /// <list type="bullet">
        ///     <item><description>the field is neither stored nor indexed</description></item>
        ///     <item><description>the field is not indexed but termVector is <see cref="TermVector.YES"/></description></item>
        /// </list>
        /// </exception>
        [Obsolete("Use StringField, TextField instead.")]
        public Field(string name, string value, Store store, Index index, TermVector termVector)
            : this(name, value, TranslateFieldType(store, index, termVector))
        {
        }

        /// <summary>
        /// Create a tokenized and indexed field that is not stored. Term vectors will
        /// not be stored.  The <see cref="TextReader"/> is read only when the <see cref="Document"/> is added to the index,
        /// i.e. you may not close the <see cref="TextReader"/> until <see cref="IndexWriter.AddDocument(System.Collections.Generic.IEnumerable{IIndexableField})"/>
        /// has been called.
        /// </summary>
        /// <param name="name">The name of the field</param>
        /// <param name="reader">The reader with the content</param>
        /// <exception cref="ArgumentNullException">if <paramref name="name"/> or <paramref name="reader"/> is <c>null</c></exception>
        [Obsolete("Use TextField instead.")]
        public Field(string name, TextReader reader)
            : this(name, reader, TermVector.NO)
        {
        }

        /// <summary>
        /// Create a tokenized and indexed field that is not stored, optionally with 
        /// storing term vectors.  The <see cref="TextReader"/> is read only when the <see cref="Document"/> is added to the index,
        /// i.e. you may not close the <see cref="TextReader"/> until <see cref="IndexWriter.AddDocument(System.Collections.Generic.IEnumerable{IIndexableField})"/>
        /// has been called.
        /// </summary>
        /// <param name="name">The name of the field</param>
        /// <param name="reader">The reader with the content</param>
        /// <param name="termVector">Whether term vector should be stored</param>
        /// <exception cref="ArgumentNullException">if <paramref name="name"/> or <paramref name="reader"/> is <c>null</c></exception>
        [Obsolete("Use TextField instead.")]
        public Field(string name, TextReader reader, TermVector termVector)
            : this(name, reader, TranslateFieldType(Store.NO, Index.ANALYZED, termVector))
        {
        }

        /// <summary>
        /// Create a tokenized and indexed field that is not stored. Term vectors will
        /// not be stored. This is useful for pre-analyzed fields.
        /// The <see cref="TokenStream"/> is read only when the <see cref="Document"/> is added to the index,
        /// i.e. you may not close the <see cref="TokenStream"/> until <see cref="IndexWriter.AddDocument(System.Collections.Generic.IEnumerable{IIndexableField})"/>
        /// has been called.
        /// </summary>
        /// <param name="name">The name of the field</param>
        /// <param name="tokenStream">The <see cref="TokenStream"/> with the content</param>
        /// <exception cref="ArgumentNullException">if <paramref name="name"/> or <paramref name="tokenStream"/> is <c>null</c></exception>
        [Obsolete("Use TextField instead.")]
        public Field(string name, TokenStream tokenStream)
            : this(name, tokenStream, TermVector.NO)
        {
        }

        /// <summary>
        /// Create a tokenized and indexed field that is not stored, optionally with 
        /// storing term vectors.  This is useful for pre-analyzed fields.
        /// The <see cref="TokenStream"/> is read only when the <see cref="Document"/> is added to the index,
        /// i.e. you may not close the <see cref="TokenStream"/> until <see cref="IndexWriter.AddDocument(System.Collections.Generic.IEnumerable{IIndexableField})"/>
        /// has been called.
        /// </summary>
        /// <param name="name">The name of the field</param>
        /// <param name="tokenStream">The <see cref="TokenStream"/> with the content</param>
        /// <param name="termVector">Whether term vector should be stored</param>
        /// <exception cref="ArgumentNullException">if <paramref name="name"/> or <paramref name="tokenStream"/> is <c>null</c></exception>
        [Obsolete("Use TextField instead.")]
        public Field(string name, TokenStream tokenStream, TermVector termVector)
            : this(name, tokenStream, TranslateFieldType(Store.NO, Index.ANALYZED, termVector))
        {
        }

        /// <summary>
        /// Create a stored field with binary value. Optionally the value may be compressed.
        /// </summary>
        /// <param name="name">The name of the field</param>
        /// <param name="value">The binary value</param>
        [Obsolete("Use StoredField instead.")]
        public Field(string name, byte[] value)
            : this(name, value, TranslateFieldType(Store.YES, Index.NO, TermVector.NO))
        {
        }

        /// <summary>
        /// Create a stored field with binary value. Optionally the value may be compressed.
        /// </summary>
        /// <param name="name">The name of the field</param>
        /// <param name="value">The binary value</param>
        /// <param name="offset">Starting offset in value where this <see cref="Field"/>'s bytes are</param>
        /// <param name="length">Number of bytes to use for this <see cref="Field"/>, starting at offset</param>
        [Obsolete("Use StoredField instead.")]
        public Field(string name, byte[] value, int offset, int length)
            : this(name, value, offset, length, TranslateFieldType(Store.YES, Index.NO, TermVector.NO))
        {
        }
    }

    /// <summary>
    /// LUCENENET specific extension methods to add functionality to enumerations
    /// that mimic Lucene
    /// </summary>
    public static class FieldExtensions
    {
        public static bool IsStored(this Field.Store store)
        {
            switch (store)
            {
                case Field.Store.YES:
                    return true;

                case Field.Store.NO:
                    return false;

                default:
                    throw new ArgumentOutOfRangeException("store", "Invalid value for Field.Store");
            }
        }

        [Obsolete("This is here only to ease transition from the pre-4.0 APIs.")]
        public static bool IsIndexed(this Field.Index index)
        {
            switch (index)
            {
                case Field.Index.NO:
                    return false;

                case Field.Index.ANALYZED:
                case Field.Index.NOT_ANALYZED:
                case Field.Index.NOT_ANALYZED_NO_NORMS:
                case Field.Index.ANALYZED_NO_NORMS:
                    return true;

                default:
                    throw new ArgumentOutOfRangeException("index", "Invalid value for Field.Index");
            }
        }

        [Obsolete("This is here only to ease transition from the pre-4.0 APIs.")]
        public static bool IsAnalyzed(this Field.Index index)
        {
            switch (index)
            {
                case Field.Index.NO:
                case Field.Index.NOT_ANALYZED:
                case Field.Index.NOT_ANALYZED_NO_NORMS:
                    return false;

                case Field.Index.ANALYZED:
                case Field.Index.ANALYZED_NO_NORMS:
                    return true;

                default:
                    throw new ArgumentOutOfRangeException("index", "Invalid value for Field.Index");
            }
        }

        [Obsolete("This is here only to ease transition from the pre-4.0 APIs.")]
        public static bool OmitNorms(this Field.Index index)
        {
            switch (index)
            {
                case Field.Index.ANALYZED:
                case Field.Index.NOT_ANALYZED:
                    return false;

                case Field.Index.NO:
                case Field.Index.NOT_ANALYZED_NO_NORMS:
                case Field.Index.ANALYZED_NO_NORMS:
                    return true;

                default:
                    throw new ArgumentOutOfRangeException("index", "Invalid value for Field.Index");
            }
        }

        [Obsolete("This is here only to ease transition from the pre-4.0 APIs.")]
        public static bool IsStored(this Field.TermVector tv)
        {
            switch (tv)
            {
                case Field.TermVector.NO:
                    return false;

                case Field.TermVector.YES:
                case Field.TermVector.WITH_OFFSETS:
                case Field.TermVector.WITH_POSITIONS:
                case Field.TermVector.WITH_POSITIONS_OFFSETS:
                    return true;

                default:
                    throw new ArgumentOutOfRangeException("tv", "Invalid value for Field.TermVector");
            }
        }

        [Obsolete("This is here only to ease transition from the pre-4.0 APIs.")]
        public static bool WithPositions(this Field.TermVector tv)
        {
            switch (tv)
            {
                case Field.TermVector.NO:
                case Field.TermVector.YES:
                case Field.TermVector.WITH_OFFSETS:
                    return false;

                case Field.TermVector.WITH_POSITIONS:
                case Field.TermVector.WITH_POSITIONS_OFFSETS:
                    return true;

                default:
                    throw new ArgumentOutOfRangeException("tv", "Invalid value for Field.TermVector");
            }
        }

        [Obsolete("This is here only to ease transition from the pre-4.0 APIs.")]
        public static bool WithOffsets(this Field.TermVector tv)
        {
            switch (tv)
            {
                case Field.TermVector.NO:
                case Field.TermVector.YES:
                case Field.TermVector.WITH_POSITIONS:
                    return false;

                case Field.TermVector.WITH_OFFSETS:
                case Field.TermVector.WITH_POSITIONS_OFFSETS:
                    return true;

                default:
                    throw new ArgumentOutOfRangeException("tv", "Invalid value for Field.TermVector");
            }
        }

        [Obsolete("This is here only to ease transition from the pre-4.0 APIs.")]
        public static Field.Index ToIndex(bool indexed, bool analyed)
        {
            return ToIndex(indexed, analyed, false);
        }

        [Obsolete("This is here only to ease transition from the pre-4.0 APIs.")]
        public static Field.Index ToIndex(bool indexed, bool analyzed, bool omitNorms) 
        {
            // If it is not indexed nothing else matters
            if (!indexed)
            {
                return Field.Index.NO;
            }

            // typical, non-expert
            if (!omitNorms)
            {
                if (analyzed)
                {
                    return Field.Index.ANALYZED;
                }
                return Field.Index.NOT_ANALYZED;
            }

            // Expert: Norms omitted
            if (analyzed)
            {
                return Field.Index.ANALYZED_NO_NORMS;
            }
            return Field.Index.NOT_ANALYZED_NO_NORMS;
        }

        /// <summary>
        /// Get the best representation of a TermVector given the flags.
        /// </summary>
        [Obsolete("This is here only to ease transition from the pre-4.0 APIs.")]
        public static Field.TermVector ToTermVector(bool stored, bool withOffsets, bool withPositions)
        {
            // If it is not stored, nothing else matters.
            if (!stored)
            {
                return Field.TermVector.NO;
            }

            if (withOffsets)
            {
                if (withPositions)
                {
                    return Field.TermVector.WITH_POSITIONS_OFFSETS;
                }
                return Field.TermVector.WITH_OFFSETS;
            }

            if (withPositions)
            {
                return Field.TermVector.WITH_POSITIONS;
            }
            return Field.TermVector.YES;
        }
    }
}