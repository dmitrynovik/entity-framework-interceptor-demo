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
            private readonly long _tenantId;

            public TenantQueryVisitor(long tenantId)
            {
                _tenantId = tenantId;
            }

            public override DbExpression Visit(DbScanExpression expression)
            {                
                var table = (EntityType)expression.Target.ElementType;
                if (table.Properties.Any(p => p.Name == TenantId))
                {
                    DbExpression expr = base.Visit(expression);
                    var binding = expr.Bind();
                    var property = binding.VariableType.Variable(binding.VariableName).Property(TenantId);
                    expr = binding.Filter(property.Equal(property.Property.TypeUsage.Parameter(TenantId)));
                    return expr;
                }
                return base.Visit(expression);
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

        private static void ValidateTenantProperty(DbUpdateCommandTree command, long tenantId)
        {
            foreach (var clause in command.SetClauses.OfType<DbSetClause>())
            {
                var pe = clause.Property as DbPropertyExpression;
                var rvalue = clause.Value as DbConstantExpression;
                if (pe != null && pe.Property.Name == TenantId && rvalue != null)
                {
                    if ((long)rvalue.Value != tenantId)
                        throw new NotSupportedException("Cross-tenant opreration detected");
                }
            }
        }

        private static void ValidateTenantPredicate(DbDeleteCommandTree command, long tenantId)
        {
            var predicate = command.Predicate as DbBinaryExpression;
            var lvalue = (predicate?.Left as DbPropertyExpression)?.Property;
            var rvalue = (predicate?.Right as DbConstantExpression)?.Value;
            if (lvalue != null && lvalue.Name == TenantId && rvalue != null && (long)rvalue != tenantId)
                throw new NotSupportedException("Cross-tenant opreration detected");
        }

        private static void ValidateTenantPredicate(DbUpdateCommandTree command, long tenantId)
        {
            var predicate = command.Predicate as DbBinaryExpression;
            var lvalue = (predicate?.Left as DbPropertyExpression)?.Property;
            var rvalue = (predicate?.Right as DbConstantExpression)?.Value;
            if (lvalue != null && lvalue.Name == TenantId && rvalue != null && (long)rvalue != tenantId)
                throw new NotSupportedException("Cross-tenant opreration detected");
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
                    var newQuery = selectCommand.Query.Accept(new TenantQueryVisitor(context.TenantId));
                    interceptionContext.Result = new DbQueryCommandTree(selectCommand.MetadataWorkspace, selectCommand.DataSpace, newQuery);
                }
                else if ((updateCommand = interceptionContext.OriginalResult as DbUpdateCommandTree) != null)
                {
                    // UPDATE case:
                    ValidateTenantProperty(updateCommand, context.TenantId);
                    ValidateTenantPredicate(updateCommand, context.TenantId);

                    var clauses = new List<DbModificationClause>();                    

                    SetPropertyIfExists(context, updateCommand, clauses, "ModifiedById", DbExpression.FromInt64(context?.UserId));
                    SetPropertyIfExists(context, updateCommand, clauses, "ModifiedOn", DbExpression.FromDateTime(DateTime.UtcNow));

                    var newUpdateCommand = new DbUpdateCommandTree(updateCommand.MetadataWorkspace,
                        updateCommand.DataSpace,
                        updateCommand.Target,
                        updateCommand.Predicate,
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
                    ValidateTenantPredicate(deleteCommand, context.TenantId);
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