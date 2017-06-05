using System.Data.Entity;
using System.Data.Entity.Infrastructure.Interception;
using System.Linq;

namespace EFInterceptorDemo
{
    public static class DbCommandInterceptionContextExtensions
    {
        public static T GetContext<T>(this DbInterceptionContext interceptionContext) where T: DbContext => interceptionContext.DbContexts
            .OfType<T>()
            .FirstOrDefault();
    }
}
