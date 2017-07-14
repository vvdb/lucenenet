﻿namespace Lucene.Net.Cli
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

    public class RootCommand : ICommand
    {
        public class Configuration : ConfigurationBase
        {
            public Configuration(CommandLineOptions options)
            {
                this.Description = FromResource("RootCommandDescription");

                //// LUCENENET TODO: Fix this to use CommandLine stuff...
                //this.VersionOption("-v|--version", typeof(Program).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);

                this.Commands.Add(new AnalysisCommand.Configuration(options));
                this.Commands.Add(new IndexCommand.Configuration(options));
                this.Commands.Add(new LockCommand.Configuration(options));
                this.Commands.Add(new DemoCommand.Configuration(options));

                this.OnExecute(() => new RootCommand().Run(this));
            }
        }

        public int Run(ConfigurationBase cmd)
        {
            cmd.ShowHelp();
            return 1;
        }
    }
}
