﻿using Lucene.Net.Attributes;
using Lucene.Net.Cli.CommandLine;
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

    public class LockStressTestCommandTest : CommandTestCase
    {
        protected override ConfigurationBase CreateConfiguration(MockConsoleApp app)
        {
            return new LockStressTestCommand.Configuration(new CommandLineOptions()) { Main = (args) => app.Main(args) };
        }

        protected override IList<Arg[]> GetOptionalArgs()
        {
            // NOTE: We must order this in the sequence of the expected output.
            return new List<Arg[]>();
        }
        protected override IList<Arg[]> GetRequiredArgs()
        {
            // NOTE: We must order this in the sequence of the expected output.
            return new List<Arg[]>()
            {
                new Arg[] { new Arg(inputPattern: "12345", output: new string[] { "12345" }) },
                new Arg[] { new Arg(inputPattern: "192.168.0.1", output: new string[] { "192.168.0.1" }) },
                new Arg[] { new Arg(inputPattern: "9876", output: new string[] { "9876" }) },
                new Arg[] { new Arg(inputPattern: "NativeFSLockFactory", output: new string[] { "NativeFSLockFactory" }) },
                new Arg[] { new Arg(inputPattern: @"F:\temp2", output: new string[] { @"F:\temp2" }) },
                new Arg[] { new Arg(inputPattern: "100", output: new string[] { "100" }) },
                new Arg[] { new Arg(inputPattern: "10", output: new string[] { "10" }) },
            };
        }

        [Test]
        [LuceneNetSpecific]
        public virtual void TestNotEnoughArguments()
        {
            AssertConsoleOutput("one two three four five six", FromResource("NotEnoughArguments", 7));
        }

        [Test]
        [LuceneNetSpecific]
        public virtual void TestTooManyArguments()
        {
            Assert.Throws<CommandParsingException>(() => AssertConsoleOutput("one two three four five six seven eight", ""));
        }
    }
}
