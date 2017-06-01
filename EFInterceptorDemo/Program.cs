using System;
using System.Linq;

namespace EFInterceptorDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var ctx = new DataContext(tenantId: 77, userId: 2))
            {
                var me = ctx.Employees.FirstOrDefault(x => x.Name == "Dmitry");
                if (me == null)
                {
                    me = new Employee() {Name = "Dmitry", Salary = 1000000};
                    ctx.Employees.Add(me);
                }
                else me.Salary = me.Salary + 1;
                ctx.SaveChanges();

                me = ctx.Employees.First(x => x.Name == "Dmitry");
                Console.WriteLine($"Tenant: {me.TenantId}, ModifiedBy: {me.ModifiedBy}, ModifiedAt: {me.ModifiedAt}, Name: {me.Name}, Salary: {me.Salary}");
            }

            Console.WriteLine("Press any key to exit ...");
            Console.Read();
        }
    }
}
