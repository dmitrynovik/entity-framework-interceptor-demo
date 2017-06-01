using System.Data.Entity;

namespace EFInterceptorDemo
{
    public class DbConfig : DbConfiguration
    {
        public DbConfig()
        {
            AddInterceptor(new Interceptor());
        }
    }

    public class DataContext : DbContext
    {

        public DataContext(long tenantId, long userId) : this("Default", tenantId, userId) {  }

        public DataContext(string connStr, long userId, long tenantId) : base(connStr)
        {
            TenantId = tenantId;
            UserId = userId;
        }

        public long UserId { get; set; }
        public long TenantId { get; }

        public DbSet<Employee> Employees { get; set; }
    }
}
