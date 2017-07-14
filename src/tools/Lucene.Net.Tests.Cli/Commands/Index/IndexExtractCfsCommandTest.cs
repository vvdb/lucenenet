﻿using Lucene.Net.Attributes;
using NUnit.Framework;
using System.Collections.Generic;

namespace Lucene.Net.Cli.Commands
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

    public class IndexExtractCfsCommandTest : CommandTestCase
    {
        protected override ConfigurationBase CreateConfiguration(MockConsoleApp app)
        {
            return new IndexExtractCfsCommand.Configuration(new CommandLineOptions()) { Main = (args) => app.Main(args) };
        }

        protected override IList<Arg[]> GetOptionalArgs()
        {
            // NOTE: We must order this in the sequence of the expected output.
            return new List<Arg[]>()
            {
                new Arg[] { new Arg(inputPattern: "-dir SimpleFSDirectory|--directory-type SimpleFSDirectory", output: new string[] { "-dir-impl", "SimpleFSDirectory" }) },
            };
        }

        protected override IList<Arg[]> GetRequiredArgs()
        {
            // NOTE: We must order this in the sequence of the expected output.
            return new List<Arg[]>()
            {
                new Arg[] { new Arg(inputPattern: @"C:\lucene-temp", output: new string[] { @"C:\lucene-temp" }) },
                new Arg[] { new Arg(inputPattern: "", output: new string[] { "-extract" }) },
            };
        }

        [Test]
        [LuceneNetSpecific]
        public virtual void TestNotEnoughArguments()
        {
            AssertConsoleOutput("", FromResource("NotEnoughArguments", 1));
        }
    }
}
