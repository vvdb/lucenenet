﻿using Lucene.Net.Support;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Lucene.Net.Analysis.Phonetic.Language.Bm
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
    /// Tests <see cref="PhoneticEngine"/> and <see cref="LanguageSet"/> in ways very similar to code found in solr-3.6.0.
    /// <para/>
    /// since 1.7
    /// </summary>
    public class PhoneticEngineRegressionTest
    {
        [Test]
        public void TestSolrGENERIC()
        {
            IDictionary<String, String> args;

            // concat is true, ruleType is EXACT
            args = new SortedDictionary<String, String>();
            args.Put("nameType", "GENERIC");
            Assert.AreEqual(Encode(args, true, "Angelo"), "agilo|angilo|aniilo|anilo|anxilo|anzilo|ogilo|ongilo|oniilo|onilo|onxilo|onzilo");
            args.Put("ruleType", "EXACT");
            Assert.AreEqual(Encode(args, true, "Angelo"), "anZelo|andZelo|angelo|anhelo|anjelo|anxelo");
            Assert.AreEqual(Encode(args, true, "D'Angelo"), "(anZelo|andZelo|angelo|anhelo|anjelo|anxelo)-(danZelo|dandZelo|dangelo|danhelo|danjelo|danxelo)");
            args.Put("languageSet", "italian,greek,spanish");
            Assert.AreEqual(Encode(args, true, "Angelo"), "andZelo|angelo|anxelo");
            Assert.AreEqual(Encode(args, true, "1234"), "");

            // concat is false, ruleType is EXACT
            args = new SortedDictionary<String, String>();
            Assert.AreEqual(Encode(args, false, "Angelo"), "agilo|angilo|aniilo|anilo|anxilo|anzilo|ogilo|ongilo|oniilo|onilo|onxilo|onzilo");
            args.Put("ruleType", "EXACT");
            Assert.AreEqual(Encode(args, false, "Angelo"), "anZelo|andZelo|angelo|anhelo|anjelo|anxelo");
            Assert.AreEqual(Encode(args, false, "D'Angelo"), "(anZelo|andZelo|angelo|anhelo|anjelo|anxelo)-(danZelo|dandZelo|dangelo|danhelo|danjelo|danxelo)");
            args.Put("languageSet", "italian,greek,spanish");
            Assert.AreEqual(Encode(args, false, "Angelo"), "andZelo|angelo|anxelo");
            Assert.AreEqual(Encode(args, false, "1234"), "");

            // concat is true, ruleType is APPROX
            args = new SortedDictionary<String, String>();
            Assert.AreEqual(Encode(args, true, "Angelo"), "agilo|angilo|aniilo|anilo|anxilo|anzilo|ogilo|ongilo|oniilo|onilo|onxilo|onzilo");
            args.Put("ruleType", "APPROX");
            Assert.AreEqual(Encode(args, true, "Angelo"), "agilo|angilo|aniilo|anilo|anxilo|anzilo|ogilo|ongilo|oniilo|onilo|onxilo|onzilo");
            Assert.AreEqual(Encode(args, true, "D'Angelo"), "(agilo|angilo|aniilo|anilo|anxilo|anzilo|ogilo|ongilo|oniilo|onilo|onxilo|onzilo)-(dagilo|dangilo|daniilo|danilo|danxilo|danzilo|dogilo|dongilo|doniilo|donilo|donxilo|donzilo)");
            args.Put("languageSet", "italian,greek,spanish");
            Assert.AreEqual(Encode(args, true, "Angelo"), "angilo|anxilo|anzilo|ongilo|onxilo|onzilo");
            Assert.AreEqual(Encode(args, true, "1234"), "");

            // concat is false, ruleType is APPROX
            args = new SortedDictionary<String, String>();
            Assert.AreEqual(Encode(args, false, "Angelo"), "agilo|angilo|aniilo|anilo|anxilo|anzilo|ogilo|ongilo|oniilo|onilo|onxilo|onzilo");
            args.Put("ruleType", "APPROX");
            Assert.AreEqual(Encode(args, false, "Angelo"), "agilo|angilo|aniilo|anilo|anxilo|anzilo|ogilo|ongilo|oniilo|onilo|onxilo|onzilo");
            Assert.AreEqual(Encode(args, false, "D'Angelo"), "(agilo|angilo|aniilo|anilo|anxilo|anzilo|ogilo|ongilo|oniilo|onilo|onxilo|onzilo)-(dagilo|dangilo|daniilo|danilo|danxilo|danzilo|dogilo|dongilo|doniilo|donilo|donxilo|donzilo)");
            args.Put("languageSet", "italian,greek,spanish");
            Assert.AreEqual(Encode(args, false, "Angelo"), "angilo|anxilo|anzilo|ongilo|onxilo|onzilo");
            Assert.AreEqual(Encode(args, false, "1234"), "");
        }

        [Test]
        public void TestSolrASHKENAZI()
        {
            IDictionary<String, String> args;

            // concat is true, ruleType is EXACT
            args = new SortedDictionary<String, String>();
            args.Put("nameType", "ASHKENAZI");
            Assert.AreEqual(Encode(args, true, "Angelo"), "AnElO|AnSelO|AngElO|AngzelO|AnkselO|AnzelO");
            args.Put("ruleType", "EXACT");
            Assert.AreEqual(Encode(args, true, "Angelo"), "andZelo|angelo|anhelo|anxelo");
            Assert.AreEqual(Encode(args, true, "D'Angelo"), "dandZelo|dangelo|danhelo|danxelo");
            args.Put("languageSet", "italian,greek,spanish");
            Assert.AreEqual(Encode(args, true, "Angelo"), "angelo|anxelo");
            Assert.AreEqual(Encode(args, true, "1234"), "");

            // concat is false, ruleType is EXACT
            args = new SortedDictionary<String, String>();
            args.Put("nameType", "ASHKENAZI");
            Assert.AreEqual(Encode(args, false, "Angelo"), "AnElO|AnSelO|AngElO|AngzelO|AnkselO|AnzelO");
            args.Put("ruleType", "EXACT");
            Assert.AreEqual(Encode(args, false, "Angelo"), "andZelo|angelo|anhelo|anxelo");
            Assert.AreEqual(Encode(args, false, "D'Angelo"), "dandZelo|dangelo|danhelo|danxelo");
            args.Put("languageSet", "italian,greek,spanish");
            Assert.AreEqual(Encode(args, false, "Angelo"), "angelo|anxelo");
            Assert.AreEqual(Encode(args, false, "1234"), "");

            // concat is true, ruleType is APPROX
            args = new SortedDictionary<String, String>();
            args.Put("nameType", "ASHKENAZI");
            Assert.AreEqual(Encode(args, true, "Angelo"), "AnElO|AnSelO|AngElO|AngzelO|AnkselO|AnzelO");
            args.Put("ruleType", "APPROX");
            Assert.AreEqual(Encode(args, true, "Angelo"), "AnElO|AnSelO|AngElO|AngzelO|AnkselO|AnzelO");
            Assert.AreEqual(Encode(args, true, "D'Angelo"), "dAnElO|dAnSelO|dAngElO|dAngzelO|dAnkselO|dAnzelO");
            args.Put("languageSet", "italian,greek,spanish");
            Assert.AreEqual(Encode(args, true, "Angelo"), "AnSelO|AngElO|AngzelO|AnkselO");
            Assert.AreEqual(Encode(args, true, "1234"), "");

            // concat is false, ruleType is APPROX
            args = new SortedDictionary<String, String>();
            args.Put("nameType", "ASHKENAZI");
            Assert.AreEqual(Encode(args, false, "Angelo"), "AnElO|AnSelO|AngElO|AngzelO|AnkselO|AnzelO");
            args.Put("ruleType", "APPROX");
            Assert.AreEqual(Encode(args, false, "Angelo"), "AnElO|AnSelO|AngElO|AngzelO|AnkselO|AnzelO");
            Assert.AreEqual(Encode(args, false, "D'Angelo"), "dAnElO|dAnSelO|dAngElO|dAngzelO|dAnkselO|dAnzelO");
            args.Put("languageSet", "italian,greek,spanish");
            Assert.AreEqual(Encode(args, false, "Angelo"), "AnSelO|AngElO|AngzelO|AnkselO");
            Assert.AreEqual(Encode(args, false, "1234"), "");
        }

        [Test]
        public void TestSolrSEPHARDIC()
        {
            IDictionary<String, String> args;

            // concat is true, ruleType is EXACT
            args = new SortedDictionary<String, String>();
            args.Put("nameType", "SEPHARDIC");
            Assert.AreEqual(Encode(args, true, "Angelo"), "anhila|anhilu|anzila|anzilu|nhila|nhilu|nzila|nzilu");
            args.Put("ruleType", "EXACT");
            Assert.AreEqual(Encode(args, true, "Angelo"), "anZelo|andZelo|anxelo");
            Assert.AreEqual(Encode(args, true, "D'Angelo"), "anZelo|andZelo|anxelo");
            args.Put("languageSet", "italian,greek,spanish");
            Assert.AreEqual(Encode(args, true, "Angelo"), "andZelo|anxelo");
            Assert.AreEqual(Encode(args, true, "1234"), "");

            // concat is false, ruleType is EXACT
            args = new SortedDictionary<String, String>();
            args.Put("nameType", "SEPHARDIC");
            Assert.AreEqual(Encode(args, false, "Angelo"), "anhila|anhilu|anzila|anzilu|nhila|nhilu|nzila|nzilu");
            args.Put("ruleType", "EXACT");
            Assert.AreEqual(Encode(args, false, "Angelo"), "anZelo|andZelo|anxelo");
            Assert.AreEqual(Encode(args, false, "D'Angelo"), "danZelo|dandZelo|danxelo");
            args.Put("languageSet", "italian,greek,spanish");
            Assert.AreEqual(Encode(args, false, "Angelo"), "andZelo|anxelo");
            Assert.AreEqual(Encode(args, false, "1234"), "");

            // concat is true, ruleType is APPROX
            args = new SortedDictionary<String, String>();
            args.Put("nameType", "SEPHARDIC");
            Assert.AreEqual(Encode(args, true, "Angelo"), "anhila|anhilu|anzila|anzilu|nhila|nhilu|nzila|nzilu");
            args.Put("ruleType", "APPROX");
            Assert.AreEqual(Encode(args, true, "Angelo"), "anhila|anhilu|anzila|anzilu|nhila|nhilu|nzila|nzilu");
            Assert.AreEqual(Encode(args, true, "D'Angelo"), "anhila|anhilu|anzila|anzilu|nhila|nhilu|nzila|nzilu");
            args.Put("languageSet", "italian,greek,spanish");
            Assert.AreEqual(Encode(args, true, "Angelo"), "anhila|anhilu|anzila|anzilu|nhila|nhilu|nzila|nzilu");
            Assert.AreEqual(Encode(args, true, "1234"), "");

            // concat is false, ruleType is APPROX
            args = new SortedDictionary<String, String>();
            args.Put("nameType", "SEPHARDIC");
            Assert.AreEqual(Encode(args, false, "Angelo"), "anhila|anhilu|anzila|anzilu|nhila|nhilu|nzila|nzilu");
            args.Put("ruleType", "APPROX");
            Assert.AreEqual(Encode(args, false, "Angelo"), "anhila|anhilu|anzila|anzilu|nhila|nhilu|nzila|nzilu");
            Assert.AreEqual(Encode(args, false, "D'Angelo"), "danhila|danhilu|danzila|danzilu|nhila|nhilu|nzila|nzilu");
            args.Put("languageSet", "italian,greek,spanish");
            Assert.AreEqual(Encode(args, false, "Angelo"), "anhila|anhilu|anzila|anzilu|nhila|nhilu|nzila|nzilu");
            Assert.AreEqual(Encode(args, false, "1234"), "");
        }

        /**
         * This code is similar in style to code found in Solr:
         * solr/core/src/java/org/apache/solr/analysis/BeiderMorseFilterFactory.java
         *
         * Making a JUnit test out of it to protect Solr from possible future
         * regressions in Commons-Codec.
         */
        private static String Encode(IDictionary<String, String> args, bool concat, String input)
        {
            LanguageSet languageSet;
            PhoneticEngine engine;

            // PhoneticEngine = NameType + RuleType + concat
            // we use common-codec's defaults: GENERIC + APPROX + true
            String nameTypeArg;
            args.TryGetValue("nameType", out nameTypeArg);
            NameType nameType = (nameTypeArg == null) ? NameType.GENERIC : (NameType)Enum.Parse(typeof(NameType), nameTypeArg, true);

            String ruleTypeArg;
            args.TryGetValue("ruleType", out ruleTypeArg);
            RuleType ruleType = (ruleTypeArg == null) ? RuleType.APPROX : (RuleType)Enum.Parse(typeof(RuleType), ruleTypeArg, true);

            engine = new PhoneticEngine(nameType, ruleType, concat);

            // LanguageSet: defaults to automagic, otherwise a comma-separated list.
            String languageSetArg;
            args.TryGetValue("languageSet", out languageSetArg);
            if (languageSetArg == null || languageSetArg.equals("auto"))
            {
                languageSet = null;
            }
            else
            {
                languageSet = LanguageSet.From(new HashSet<String>(Arrays.AsList(languageSetArg.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries))));
            }

            /*
                org/apache/lucene/analysis/phonetic/BeiderMorseFilter.java (lines 96-98) does this:

                encoded = (languages == null)
                    ? engine.encode(termAtt.toString())
                    : engine.encode(termAtt.toString(), languages);

                Hence our approach, below:
            */
            if (languageSet == null)
            {
                return engine.Encode(input);
            }
            else
            {
                return engine.Encode(input, languageSet);
            }
        }
    }
}
