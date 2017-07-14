﻿using Lucene.Net.Demo.Facet;
using System.Collections.Generic;

namespace Lucene.Net.Cli
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

    public class DemoRangeFacetsCommand : ICommand
    {
        public class Configuration : DemoConfiguration
        {
            public Configuration(CommandLineOptions options)
            {
                this.Main = (args) => RangeFacetsExample.Main(args);

                this.Name = "range-facets";
                this.Description = FromResource("Description");
                this.ExtendedHelpText = FromResource("ExtendedHelpText");

                this.OnExecute(() => new DemoRangeFacetsCommand().Run(this));
            }

            public override IEnumerable<string> SourceCodeFiles
            {
                get
                {
                    return new string[] { "RangeFacetsExample.cs" };
                }
            }
        }

        public int Run(ConfigurationBase cmd)
        {
            cmd.Main(new string[0]);
            return 0;
        }
    }
}
