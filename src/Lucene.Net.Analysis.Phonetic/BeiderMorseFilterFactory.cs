﻿// lucene version compatibility level: 4.8.1
using Lucene.Net.Analysis.Phonetic.Language.Bm;
using Lucene.Net.Analysis.Util;
using System;
using System.Collections.Generic;

namespace Lucene.Net.Analysis.Phonetic
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
    /// Factory for <see cref="BeiderMorseFilter"/>.
    /// <code>
    /// &lt;fieldType name="text_bm" class="solr.TextField" positionIncrementGap="100"&gt;
    ///   &lt;analyzer&gt;
    ///     &lt;tokenizer class="solr.StandardTokenizerFactory"/&gt;
    ///     &lt;filter class="solr.BeiderMorseFilterFactory"
    ///        nameType="GENERIC" ruleType="APPROX" 
    ///        concat="true" languageSet="auto"
    ///     &lt;/filter&gt;
    ///   &lt;/analyzer&gt;
    /// &lt;/fieldType&gt;
    /// </code>
    /// </summary>
    public class BeiderMorseFilterFactory : TokenFilterFactory
    {
        private readonly PhoneticEngine engine;
        private readonly LanguageSet languageSet;

        /// <summary>Creates a new <see cref="BeiderMorseFilterFactory"/></summary>
        public BeiderMorseFilterFactory(IDictionary<string, string> args)
                  : base(args)
        {
            // PhoneticEngine = NameType + RuleType + concat
            // we use common-codec's defaults: GENERIC + APPROX + true
            NameType nameType = (NameType)Enum.Parse(typeof(NameType), Get(args, "nameType", NameType.GENERIC.ToString()), true);
            RuleType ruleType = (RuleType)Enum.Parse(typeof(RuleType), Get(args, "ruleType", RuleType.APPROX.ToString()), true);

            bool concat = GetBoolean(args, "concat", true);
            engine = new PhoneticEngine(nameType, ruleType, concat);

            // LanguageSet: defaults to automagic, otherwise a comma-separated list.
            ISet<string> langs = GetSet(args, "languageSet");
            languageSet = (null == langs || (1 == langs.Count && langs.Contains("auto"))) ? null : LanguageSet.From(langs);
            if (!(args.Count == 0))
            {
                throw new ArgumentException("Unknown parameters: " + args);
            }
        }

        public override TokenStream Create(TokenStream input)
        {
            return new BeiderMorseFilter(input, engine, languageSet);
        }
    }
}
