﻿using Lucene.Net.Util;

namespace Lucene.Net.Analysis.Ja.TokenAttributes
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
    /// Attribute for <see cref="Token.GetBaseForm()"/>.
    /// </summary>
    public class BaseFormAttribute : Attribute, IBaseFormAttribute
    {
        private Token token;

        public virtual string GetBaseForm()
        {
            return token == null ? null : token.GetBaseForm();
        }

        public virtual void SetToken(Token token)
        {
            this.token = token;
        }

        public override void Clear()
        {
            token = null;
        }

        public override void CopyTo(IAttribute target)
        {
            BaseFormAttribute t = (BaseFormAttribute)target;
            t.SetToken(token);
        }

        public override void ReflectWith(IAttributeReflector reflector)
        {
            reflector.Reflect(typeof(BaseFormAttribute), "baseForm", GetBaseForm());
        }
    }
}
