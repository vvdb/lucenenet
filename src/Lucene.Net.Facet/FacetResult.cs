﻿using Lucene.Net.Support;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Lucene.Net.Facet
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
    /// Counts or aggregates for a single dimension.
    /// </summary>
    public sealed class FacetResult
    {
        /// <summary>
        /// Dimension that was requested.
        /// </summary>
        public string Dim { get; private set; }

        /// <summary>
        /// Path whose children were requested.
        /// </summary>
        [WritableArray]
        [SuppressMessage("Microsoft.Performance", "CA1819", Justification = "Lucene's design requires some writable array properties")]
        public string[] Path { get; private set; } 

        /// <summary>
        /// Total value for this path (sum of all child counts, or
        /// sum of all child values), even those not included in
        /// the topN. 
        /// </summary>
        public float Value { get; private set; }

        /// <summary>
        /// How many child labels were encountered.
        /// </summary>
        public int ChildCount { get; private set; }

        /// <summary>
        /// Child counts.
        /// </summary>
        [WritableArray]
        [SuppressMessage("Microsoft.Performance", "CA1819", Justification = "Lucene's design requires some writable array properties")]
        public LabelAndValue[] LabelValues { get; private set; }

        /// <summary>
        /// The original data type of <see cref="Value"/> that was passed through the constructor.
        /// </summary>
        public Type TypeOfValue { get; private set; }

        /// <summary>
        /// Constructor for <see cref="float"/> <paramref name="value"/>. Makes the <see cref="ToString()"/> method 
        /// print the <paramref name="value"/> as a <see cref="float"/> with at least 1 number after the decimal.
        /// </summary>
        public FacetResult(string dim, string[] path, float value, LabelAndValue[] labelValues, int childCount)
            : this(dim, path, labelValues, childCount)
        {
            this.Value = value;
            this.TypeOfValue = typeof(float);
        }

        /// <summary>
        /// Constructor for <see cref="int"/> <paramref name="value"/>. Makes the <see cref="ToString()"/> method 
        /// print the <paramref name="value"/> as an <see cref="int"/> with no decimal.
        /// </summary>
        public FacetResult(string dim, string[] path, int value, LabelAndValue[] labelValues, int childCount)
            : this(dim, path, labelValues, childCount)
        {
            this.Value = value;
            this.TypeOfValue = typeof(int);
        }

        /// <summary>
        /// Private constructor for shared parameters to be called by public constructors.
        /// </summary>
        private FacetResult(string dim, string[] path, LabelAndValue[] labelValues, int childCount)
        {
            this.Dim = dim;
            this.Path = path;
            this.LabelValues = labelValues;
            this.ChildCount = childCount;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("dim=");
            sb.Append(Dim);
            sb.Append(" path=");
            sb.Append("[" + Arrays.ToString(Path) + "]");
            sb.Append(" value=");
            if (TypeOfValue == typeof(int))
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0:0}", Value); // No formatting (looks like int)
            }
            else
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0:0.0#####}", Value); // Decimal formatting
                //sb.Append(Number.ToString(Value)); // LUCENENET TODO: Decimal formatting causing a Demo test to fail because of double formatting inaccuracy
            }
            sb.Append(" childCount=");
            sb.Append(ChildCount);
            sb.Append('\n');
            foreach (LabelAndValue labelValue in LabelValues)
            {
                sb.Append("  " + labelValue + "\n");
            }
            return sb.ToString();
        }

        public override bool Equals(object other)
        {
            if ((other is FacetResult) == false)
            {
                return false;
            }
            FacetResult other2 = (FacetResult)other;
            return Value.Equals(other2.Value) && ChildCount == other2.ChildCount && Arrays.Equals(LabelValues, other2.LabelValues);
        }

        public override int GetHashCode()
        {
            int hashCode = Value.GetHashCode() + 31 * ChildCount;
            foreach (LabelAndValue labelValue in LabelValues)
            {
                hashCode = labelValue.GetHashCode() + 31 * hashCode;
            }
            return hashCode;
        }
    }
}