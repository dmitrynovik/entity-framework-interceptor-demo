using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure.Interception;
using System.Linq;

namespace EFInterceptorDemo
{
    public class EntityTreeInterceptor : IDbCommandTreeInterceptor
    {
        public class TenantQueryVisitor : DefaultExpressionVisitor
        {
            public override DbExpression Visit(DbScanExpression expression)
            {
                var result = base.Visit(expression);
                var table = (EntityType)expression.Target.ElementType;
                if (table.Properties.Any(p => p.Name == TenantId))
                {
                    var binding = result.Bind();
                    var property = binding.VariableType.Variable(binding.VariableName).Property(TenantId);
                    return binding.Filter(property.Equal(property.Property.TypeUsage.Parameter(TenantId)));
                }
                return result;
            }
        }

        public class TenantPredicateVisitor : DefaultExpressionVisitor
        {
            private readonly long _tenantId;
            private readonly DbExpressionBinding _target;

            public TenantPredicateVisitor(DbExpressionBinding target, long tenantId)
            {
                _tenantId = tenantId;
                _target = target;
            }

            public override DbExpression Visit(DbComparisonExpression expression)
            {
                var result = (DbComparisonExpression)base.Visit(expression);
                var table = (expression.Left as DbPropertyExpression)?.Property.DeclaringType as EntityType;
                var tenantProp = table?.Properties.FirstOrDefault(p => p.Name == TenantId);
                if (tenantProp != null)
                {
                    var tenantVar = _target.VariableType.Variable(TenantId);
                    var tenantPropExpr = tenantVar.Property(TenantId);
                    var tenantExpr = tenantPropExpr.Equal(DbExpression.FromInt64(_tenantId));
                    return result.And(tenantExpr);
                }
                return result;
            }
        }

        const string TenantId = "TenantId";

        private static void SetPropertyIfExists(DataContext ctx, DbModificationCommandTree command, ICollection<DbModificationClause> setClauses, string name, DbExpression expr)
        {
            if (ctx == null)
                return;

            var type = command.Target.VariableType.EdmType as EntityType;
            var prop = type?.Properties.FirstOrDefault(x => x.Name == name);
            if (prop == null)
                return;

            AddClause(type, name, command, setClauses, expr);
        }

        public void TreeCreated(DbCommandTreeInterceptionContext interceptionContext)
        {
            if (interceptionContext.OriginalResult.DataSpace == DataSpace.SSpace)
            {
                DbQueryCommandTree selectCommand;
                DbDeleteCommandTree deleteCommand;
                DbInsertCommandTree insertCommand;
                DbUpdateCommandTree updateCommand;

                var context = interceptionContext.GetContext<DataContext>();
                if ((selectCommand = interceptionContext.Result as DbQueryCommandTree) != null)
                {
                    // SELECT case:
                    var newQuery = selectCommand.Query.Accept(new TenantQueryVisitor());
                    interceptionContext.Result = new DbQueryCommandTree(selectCommand.MetadataWorkspace, selectCommand.DataSpace, newQuery);
                }
                else if ((updateCommand = interceptionContext.OriginalResult as DbUpdateCommandTree) != null)
                {
                    // UPDATE case:
                    var clauses = new List<DbModificationClause>();                    
                    SetPropertyIfExists(context, updateCommand, clauses, "ModifiedById", DbExpression.FromInt64(context.UserId));
                    SetPropertyIfExists(context, updateCommand, clauses, "ModifiedOn", DbExpression.FromDateTime(DateTime.UtcNow));

                    var newUpdateCommand = new DbUpdateCommandTree(updateCommand.MetadataWorkspace,
                        updateCommand.DataSpace,
                        updateCommand.Target,
                        updateCommand.Predicate.Accept(new TenantPredicateVisitor(updateCommand.Target, context.TenantId)),
                        MergeSetClauses(clauses, updateCommand.SetClauses),
                        null);

                    interceptionContext.Result = newUpdateCommand;
                }
                else if ((insertCommand = interceptionContext.Result as DbInsertCommandTree) != null)
                {
                    // INSERT case:
                    var clauses = new List<DbModificationClause>();

                    SetPropertyIfExists(context, insertCommand, clauses, TenantId, DbExpression.FromInt64(context?.TenantId));
                    SetPropertyIfExists(context, insertCommand, clauses, "CreatedById", DbExpression.FromInt64(context?.UserId));
                    SetPropertyIfExists(context, insertCommand, clauses, "CreatedOn", DbExpression.FromDateTime(DateTime.UtcNow));
                    SetPropertyIfExists(context, insertCommand, clauses, "ModifiedById", DbExpression.FromInt64(context?.UserId));
                    SetPropertyIfExists(context, insertCommand, clauses, "ModifiedOn", DbExpression.FromDateTime(DateTime.UtcNow));

                    var newInsertCommand = new DbInsertCommandTree(insertCommand.MetadataWorkspace,
                        insertCommand.DataSpace,
                        insertCommand.Target,
                        MergeSetClauses(clauses, insertCommand.SetClauses),
                        insertCommand.Returning);

                    interceptionContext.Result = newInsertCommand;
                }
                else if ((deleteCommand = interceptionContext.OriginalResult as DbDeleteCommandTree) != null)
                {
                    // DELETE
                    var newDeleteCommand = new DbDeleteCommandTree(deleteCommand.MetadataWorkspace,
                        deleteCommand.DataSpace,
                        deleteCommand.Target,
                        deleteCommand.Predicate.Accept(new TenantPredicateVisitor(deleteCommand.Target, context.TenantId)));

                    interceptionContext.Result = newDeleteCommand;
                }
            }
        }

        private static ReadOnlyCollection<DbModificationClause> MergeSetClauses(ICollection<DbModificationClause> setClauses, IList<DbModificationClause> originalClauses)
        {
            // Each named clause must be specified in a command at most once, otherwise it'll cause runtime error:
            var original = originalClauses.Cast<DbSetClause>().ToDictionary(i => ((DbPropertyExpression)i.Property).Property.Name, i => i);
            var addenda = setClauses.Cast<DbSetClause>().ToDictionary(i => ((DbPropertyExpression)i.Property).Property.Name, i => i);
            foreach (var kvp in addenda)
            {
                original[kvp.Key] = kvp.Value;
            }
            return original.Values.Cast<DbModificationClause>().ToList().AsReadOnly();
        }

        private static void AddClause(EntityType entity, string column, DbModificationCommandTree command, ICollection<DbModificationClause> setClauses, DbExpression value)
        {
            if (entity.Properties.Any(p => p.Name == column))
            {
                setClauses.Add(DbExpressionBuilder.SetClause(command.Target.VariableType.Variable(command.Target.VariableName).Property(column), value));
            }
        }
    }
}