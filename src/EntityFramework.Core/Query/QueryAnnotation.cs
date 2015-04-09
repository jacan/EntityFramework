// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq;
using Remotion.Linq.Clauses;

namespace Microsoft.Data.Entity.Query
{
    public class QueryAnnotation
    {
        private IQuerySource _querySource;
        private QueryModel _queryModel;

        public QueryAnnotation([NotNull] ResultOperatorBase resultOperator)
        {
            Check.NotNull(resultOperator, nameof(resultOperator));

            ResultOperator = resultOperator;
        }

        public virtual QueryModel QueryModel
        {
            get { return _queryModel; }
            [param: NotNull]
            set
            {
                Check.NotNull(value, nameof(value));

                _queryModel = value;
            }
        }

        public virtual IQuerySource QuerySource
        {
            get { return _querySource; }
            [param: NotNull]
            set
            {
                Check.NotNull(value, nameof(value));

                _querySource = value;
            }
        }

        public virtual ResultOperatorBase ResultOperator { get; }
    }
}
