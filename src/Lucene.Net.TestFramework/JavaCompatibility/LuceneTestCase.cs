/*
 *
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 *
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using System.Diagnostics;
using System.Globalization;
using Lucene.Net.Randomized.Generators;
using Lucene.Net.Support;

namespace Lucene.Net.Util
{
    public abstract partial class LuceneTestCase
    {
        public static void assertTrue(bool condition)
        {
            Assert.IsTrue(condition);
        }

        public static void assertTrue(string message, bool condition)
        {
            Assert.IsTrue(condition, message);
        }

        public static void assertFalse(bool condition)
        {
            Assert.IsFalse(condition);
        }

        public static void assertFalse(string message, bool condition)
        {
            Assert.IsFalse(condition, message);
        }

        public static void assertEquals(object expected, object actual)
        {
            Assert.AreEqual(expected, actual);
        }

        public static void assertEquals(string message, object expected, object actual)
        {
            Assert.AreEqual(expected, actual, message);
        }

        public static void assertEquals(long expected, long actual)
        {
            Assert.AreEqual(expected, actual);
        }

        public static void assertEquals(string message, long expected, long actual)
        {
            Assert.AreEqual(expected, actual, message);
        }

        public static void assertEquals<T>(ISet<T> expected, ISet<T> actual)
        {
            Assert.True(expected.SetEquals(actual));
        }

        public static void assertEquals<T>(string message, ISet<T> expected, ISet<T> actual)
        {
            Assert.True(expected.SetEquals(actual), message);
        }

        public static void assertEquals<T, S>(IDictionary<T, S> expected, IDictionary<T, S> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            foreach (var key in expected.Keys)
            {
                Assert.AreEqual(expected[key], actual[key]);
            }
        }

        public static void assertNotSame(object unexpected, object actual)
        {
            Assert.AreNotSame(unexpected, actual);
        }

        public static void assertNotSame(string message, object unexpected, object actual)
        {
            Assert.AreNotSame(unexpected, actual, message);
        }

        protected static void assertEquals(double d1, double d2, double delta)
        {
            Assert.AreEqual(d1, d2, delta);
        }

        protected static void assertEquals(string msg, double d1, double d2, double delta)
        {
            Assert.AreEqual(d1, d2, delta, msg);
        }

        protected static void assertNotNull(object o)
        {
            Assert.NotNull(o);
        }

        protected static void assertNotNull(string msg, object o)
        {
            Assert.NotNull(o, msg);
        }

        protected static void assertNull(object o)
        {
            Assert.Null(o);
        }

        protected static void assertNull(string msg, object o)
        {
            Assert.Null(o, msg);
        }

        protected static void assertArrayEquals(IEnumerable a1, IEnumerable a2)
        {
            CollectionAssert.AreEqual(a1, a2);
        }

        protected static void assertSame(Object expected, Object actual)
        {
            Assert.AreSame(expected, actual);
        }

        protected static void assertSame(string message, Object expected, Object actual)
        {
            Assert.AreSame(expected, actual, message);
        }

        protected static void fail()
        {
            Assert.Fail();
        }

        protected static void fail(string message)
        {
            Assert.Fail(message);
        }


        protected static ISet<T> AsSet<T>(params T[] args)
        {
            return new HashSet<T>(args);
        }

        protected int randomInt(int max)
        {
            return randomIntBetween(0, max);
        }

        protected int randomIntBetween(int min, int max)
        {
            Debug.Assert(max >= min, "max must be >= min: " + min + ", " + max);
            long range = (long)max - (long)min;
            if (range < int.MaxValue)
            {
                return min + Random().nextInt(1 + (int)range);
            }
            else
            {
                return toIntExact(min + Random().Next(1 + (int)range));
            }
        }

        private static int toIntExact(long value)
        {
            if (value > int.MaxValue)
            {
                throw new ArithmeticException("Overflow: " + value);
            }
            else
            {
                return (int)value;
            }
        }

        private double nextNextGaussian;
        private bool haveNextNextGaussian = false;

        /**
         * Returns the next pseudorandom, Gaussian ("normally") distributed
         * <c>double</c> value with mean <c>0.0</c> and standard
         * deviation <c>1.0</c> from this random number generator's sequence.
         * <para/>
         * The general contract of <c>nextGaussian</c> is that one
         * <c>double</c> value, chosen from (approximately) the usual
         * normal distribution with mean <c>0.0</c> and standard deviation
         * <c>1.0</c>, is pseudorandomly generated and returned.
         *
         * <para/>The method <c>nextGaussian</c> is implemented by class
         * <c>Random</c> as if by a threadsafe version of the following:
         *  <code>
         * private double nextNextGaussian;
         * private boolean haveNextNextGaussian = false;
         *
         * public double nextGaussian() {
         *   if (haveNextNextGaussian) {
         *     haveNextNextGaussian = false;
         *     return nextNextGaussian;
         *   } else {
         *     double v1, v2, s;
         *     do {
         *       v1 = 2 * nextDouble() - 1;   // between -1.0 and 1.0
         *       v2 = 2 * nextDouble() - 1;   // between -1.0 and 1.0
         *       s = v1 * v1 + v2 * v2;
         *     } while (s >= 1 || s == 0);
         *     double multiplier = StrictMath.sqrt(-2 * StrictMath.log(s)/s);
         *     nextNextGaussian = v2 * multiplier;
         *     haveNextNextGaussian = true;
         *     return v1 * multiplier;
         *   }
         * }}</code>
         * This uses the <i>polar method</i> of G. E. P. Box, M. E. Muller, and
         * G. Marsaglia, as described by Donald E. Knuth in <i>The Art of
         * Computer Programming</i>, Volume 3: <i>Seminumerical Algorithms</i>,
         * section 3.4.1, subsection C, algorithm P. Note that it generates two
         * independent values at the cost of only one call to <c>StrictMath.log</c>
         * and one call to <c>StrictMath.sqrt</c>.
         *
         * @return the next pseudorandom, Gaussian ("normally") distributed
         *         <c>double</c> value with mean <c>0.0</c> and
         *         standard deviation <c>1.0</c> from this random number
         *         generator's sequence
         */
        public double randomGaussian()
        {
            // See Knuth, ACP, Section 3.4.1 Algorithm C.
            if (haveNextNextGaussian)
            {
                haveNextNextGaussian = false;
                return nextNextGaussian;
            }
            else
            {
                double v1, v2, s;
                do
                {
                    v1 = 2 * Random().NextDouble() - 1; // between -1 and 1
                    v2 = 2 * Random().NextDouble() - 1; // between -1 and 1
                    s = v1 * v1 + v2 * v2;
                } while (s >= 1 || s == 0);
                double multiplier = Math.Sqrt(-2 * Math.Log(s) / s);
                nextNextGaussian = v2 * multiplier;
                haveNextNextGaussian = true;
                return v1 * multiplier;
            }
        }
    }
}
