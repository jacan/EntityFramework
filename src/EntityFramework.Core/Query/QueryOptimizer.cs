// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq;
using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Clauses.ExpressionTreeVisitors;
using Remotion.Linq.Clauses.ResultOperators;
using Remotion.Linq.Transformations;

namespace Microsoft.Data.Entity.Query
{
    public class QueryOptimizer : SubQueryFromClauseFlattener
    {
        private readonly ICollection<QueryAnnotation> _queryAnnotations;

        public QueryOptimizer([NotNull] ICollection<QueryAnnotation> queryAnnotations)
        {
            Check.NotNull(queryAnnotations, nameof(queryAnnotations));

            _queryAnnotations = queryAnnotations;
        }

        public override void VisitJoinClause(JoinClause joinClause, QueryModel queryModel, int index)
        {
            Check.NotNull(joinClause, nameof(joinClause));
            Check.NotNull(queryModel, nameof(queryModel));

            var subQueryExpression = joinClause.InnerSequence as SubQueryExpression;

            if (subQueryExpression != null)
            {
                VisitQueryModel(subQueryExpression.QueryModel);

                if (subQueryExpression.QueryModel.IsIdentityQuery()
                    && !subQueryExpression.QueryModel.ResultOperators.Any())
                {
                    joinClause.InnerSequence
                        = subQueryExpression.QueryModel.MainFromClause.FromExpression;

                    foreach (var queryAnnotation 
                        in _queryAnnotations
                            .Where(qa => qa.QuerySource == subQueryExpression.QueryModel.MainFromClause))
                    {
                        queryAnnotation.QuerySource = joinClause;
                    }
                }
            }

            base.VisitJoinClause(joinClause, queryModel, index);
        }

        protected override void FlattenSubQuery(
            [NotNull] SubQueryExpression subQueryExpression,
            [NotNull] FromClauseBase fromClause,
            [NotNull] QueryModel queryModel,
            int destinationIndex)
        {
            Check.NotNull(subQueryExpression, nameof(subQueryExpression));
            Check.NotNull(fromClause, nameof(fromClause));
            Check.NotNull(queryModel, nameof(queryModel));

            var subQueryModel = subQueryExpression.QueryModel;

            VisitQueryModel(subQueryModel);

            if (subQueryModel.ResultOperators
                .Any(ro => !(ro is OfTypeResultOperator))
                || subQueryModel.BodyClauses.Any(bc => bc is OrderByClause))
            {
                return;
            }

            var innerMainFromClause
                = subQueryExpression.QueryModel.MainFromClause;

            CopyFromClauseData(innerMainFromClause, fromClause);

            var innerSelectorMapping = new QuerySourceMapping();

            innerSelectorMapping.AddMapping(fromClause, subQueryExpression.QueryModel.SelectClause.Selector);

            queryModel.TransformExpressions(
                ex => ReferenceReplacingExpressionTreeVisitor
                    .ReplaceClauseReferences(ex, innerSelectorMapping, false));

            InsertBodyClauses(subQueryExpression.QueryModel.BodyClauses, queryModel, destinationIndex);

            var innerBodyClauseMapping = new QuerySourceMapping();

            innerBodyClauseMapping
                .AddMapping(innerMainFromClause, new QuerySourceReferenceExpression(fromClause));

            queryModel.TransformExpressions(ex =>
                ReferenceReplacingExpressionTreeVisitor.ReplaceClauseReferences(ex, innerBodyClauseMapping, false));

            foreach (var resultOperator in subQueryModel.ResultOperators.Reverse())
            {
                queryModel.ResultOperators.Insert(0, resultOperator);
            }

            foreach (var queryAnnotation 
                in _queryAnnotations
                    .Where(qa => qa.QuerySource == subQueryExpression.QueryModel.MainFromClause))
            {
                queryAnnotation.QueryModel = queryModel;
                queryAnnotation.QuerySource = fromClause;
            }
        }

        public override void VisitResultOperator(ResultOperatorBase resultOperator, QueryModel queryModel, int index)
        {
            if (resultOperator is ValueFromSequenceResultOperatorBase 
                && !(resultOperator is ChoiceResultOperatorBase) 
                && !queryModel.ResultOperators.Any(r => r is TakeResultOperator) 
                && !queryModel.ResultOperators.Any(r => r is SkipResultOperator))
            {
                for (int i = queryModel.BodyClauses.Count - 1; i >= 0; i--)
                {
                    if (queryModel.BodyClauses[i] is OrderByClause)
                    {
                        queryModel.BodyClauses.RemoveAt(i);
                    }
                }
            }

            base.VisitResultOperator(resultOperator, queryModel, index);
        }
    }
}
