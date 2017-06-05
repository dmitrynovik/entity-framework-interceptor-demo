using System.Data.Common;
using System.Data.Entity.Infrastructure.Interception;

namespace EFInterceptorDemo
{
    public class EntityCommandInterceptor : IDbCommandInterceptor
    {
        const string TenantId = "TenantId";

        public void NonQueryExecuting(DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
            SetTenantId(command, interceptionContext);
        }

        public void NonQueryExecuted(DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
        }

        public void ReaderExecuting(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            SetTenantId(command, interceptionContext);
        }

        public void ReaderExecuted(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
        }

        public void ScalarExecuting(DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
            SetTenantId(command, interceptionContext);
        }

        public void ScalarExecuted(DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
        }

        private void SetTenantId(DbCommand command, DbCommandInterceptionContext interceptionContext)
        {
            if (command.Parameters.Contains(TenantId))
            {
                var tenantId = interceptionContext.GetContext<DataContext>().TenantId;
                command.Parameters[TenantId].Value = tenantId;
            }
        }
    }
}