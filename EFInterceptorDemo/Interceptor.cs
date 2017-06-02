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
    class SoftDeleteAttribute : Attribute
    {
        public static string GetSoftDeleteColumnName(EdmType type) => "IsDeleted";
    }

    public class EntityInterceptor : IDbCommandTreeInterceptor
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
                const string tenantId = "TenantId";

                var table = (EntityType)expression.Target.ElementType;
                if (table.Properties.Any(p => p.Name == tenantId))
                {
                    var binding = expression.Bind();
                    return binding.Filter(binding.VariableType.Variable(binding.VariableName).Property(tenantId).Equal(DbExpression.FromInt64(_tenantId)));
                }
                return base.Visit(expression);
            }
        }

        private static void SetProperty(DataContext ctx, DbModificationCommandTree command, List<DbModificationClause> setClauses, string name, DbExpression expr)
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

                if ((selectCommand = interceptionContext.Result as DbQueryCommandTree) != null)
                {
                    // SELECT case:
                    var context = GetContext(interceptionContext);
                    var newQuery = selectCommand.Query.Accept(new TenantQueryVisitor(context.TenantId));
                    interceptionContext.Result = new DbQueryCommandTree(selectCommand.MetadataWorkspace, selectCommand.DataSpace, newQuery);
                }
                else if ((updateCommand = interceptionContext.OriginalResult as DbUpdateCommandTree) != null)
                {
                    // UPDATE case:
                    var context = GetContext(interceptionContext);
                    var setClauses = new List<DbModificationClause>();

                    SetProperty(context, updateCommand, setClauses, "TenantId", DbExpression.FromInt64(context?.TenantId));
                    SetProperty(context, updateCommand, setClauses, "ModifiedById", DbExpression.FromInt64(context?.UserId));
                    SetProperty(context, updateCommand, setClauses, "ModifiedOn", DbExpression.FromDateTime(DateTime.UtcNow));

                    var newUpdateCommand = new DbUpdateCommandTree(updateCommand.MetadataWorkspace,
                        updateCommand.DataSpace,
                        updateCommand.Target,
                        updateCommand.Predicate,
                        MergeSetClauses(setClauses, updateCommand.SetClauses),
                        null);

                    interceptionContext.Result = newUpdateCommand;
                }
                else if ((insertCommand = interceptionContext.Result as DbInsertCommandTree) != null)
                {
                    // INSERT case:
                    var context = GetContext(interceptionContext);
                    var setClauses = new List<DbModificationClause>();

                    SetProperty(context, insertCommand, setClauses, "TenantId", DbExpression.FromInt64(context?.TenantId));
                    SetProperty(context, insertCommand, setClauses, "CreatedById", DbExpression.FromInt64(context?.UserId));
                    SetProperty(context, insertCommand, setClauses, "CreatedOn", DbExpression.FromDateTime(DateTime.UtcNow));
                    SetProperty(context, insertCommand, setClauses, "ModifiedById", DbExpression.FromInt64(context?.UserId));
                    SetProperty(context, insertCommand, setClauses, "ModifiedOn", DbExpression.FromDateTime(DateTime.UtcNow));

                    var newInsertCommand = new DbInsertCommandTree(insertCommand.MetadataWorkspace,
                        insertCommand.DataSpace,
                        insertCommand.Target,
                        MergeSetClauses(setClauses, insertCommand.SetClauses),
                        insertCommand.Returning);

                    interceptionContext.Result = newInsertCommand;
                }
                else if ((deleteCommand = interceptionContext.OriginalResult as DbDeleteCommandTree) != null)
                {
                    // DELETE: do nothing
                }
            }
        }

        private static ReadOnlyCollection<DbModificationClause> MergeSetClauses(ICollection<DbModificationClause> setClauses, IList<DbModificationClause> originalClauses)
        {
            // Each clause must be specified in a command only once, otherwise it'll cause runtime error:
            var original = originalClauses.Cast<DbSetClause>().ToDictionary(i => ((DbPropertyExpression)i.Property).Property.Name, i => i);
            var addenda = setClauses.Cast<DbSetClause>().ToDictionary(i => ((DbPropertyExpression)i.Property).Property.Name, i => i);
            addenda.Each(kvp =>
            {
                original[kvp.Key] = kvp.Value;
            });
            return original.Values.Cast<DbModificationClause>().ToList().AsReadOnly();
        }

        private static DataContext GetContext(DbInterceptionContext interceptionContext) => interceptionContext.DbContexts.OfType<DataContext>().FirstOrDefault();

        private static void AddClause(EntityType entity, string column, DbModificationCommandTree command, ICollection<DbModificationClause> setClauses, DbExpression value)
        {
            if (entity.Properties.Any(p => p.Name == column))
            {
                setClauses.Add(DbExpressionBuilder.SetClause(command.Target.VariableType.Variable(command.Target.VariableName).Property(column),
                    value));
            }
        }
    }
}