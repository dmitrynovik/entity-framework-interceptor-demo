using System.Data.Entity;
using System.Diagnostics;

namespace EFInterceptorDemo
{
    public class DbConfig : DbConfiguration
    {
        public DbConfig()
        {
            AddInterceptor(new EntityInterceptor());
        }
    }

    public class DataContext : DbContext
    {

        public DataContext(long tenantId, long userId) : this("Default", tenantId, userId) {  }

        public DataContext(string connStr, long tenantId, long userId) : base(connStr)
        {
            TenantId = tenantId;
            UserId = userId;

            Database.Log = sql => Debug.WriteLine(sql);
        }

        public long UserId { get; set; }
        public long TenantId { get; }

        public DbSet<Employee> Employees { get; set; }
    }
}
