using System;
using System.Diagnostics;
using System.Linq;

namespace EFInterceptorDemo
{
    class Program
    {
        static void Main()
        {
            var name = Guid.NewGuid().ToString();

            using (var ctx = new DataContext(tenantId: 2, userId: 66))
            {
                Console.WriteLine("adding new employee with name {0} to tenant {1}", name, ctx.TenantId);
                // INSERT (will assign automatically TenantId, CreatedBy, CreatedOn, ModifiedBy, ModifiedOn):
                var employee = new Employee {Name = name, Salary = 100};
                ctx.Employees.Add(employee);
                ctx.SaveChanges();
            }

            using (var ctx = new DataContext(tenantId: 2, userId: 77))
            {
                // UPDATE: (will verify TenantId, modify ModifiedBy, ModifiedOn):
                var employee = ctx.Employees.First(x => x.Name == name);
                var salary = employee.Salary + 1;
                Console.WriteLine("\nupdating employee {0} with salary {1}", name, salary);
                employee.Salary = salary;
                ctx.SaveChanges();
            }

            using (var ctx = new DataContext(tenantId: 2, userId: 66))
            {
                // SELECT (will automatically add TenantId = 2 clause):
                var employee = ctx.Employees.First(x => x.Name == name);
                Console.WriteLine($"Name: {employee.Name}, Salary: {employee.Salary}, Tenant: {employee.TenantId}, CreatedBy: {employee.CreatedById}, CreatedOn: {employee.CreatedOn}, ModifiedBy: {employee.ModifiedById}, ModifiedAt: {employee.ModifiedOn}");
            }

            Console.WriteLine();
            using (var ctx = new DataContext(tenantId: 3, userId: 66))
            {
                // SELECT with different tenant returns NULL:
                Console.WriteLine($"\nselecting employee {name} with tenant id {ctx.TenantId} will return NULL:");
                var employee = ctx.Employees.FirstOrDefault(x => x.Name == name);
                Console.WriteLine(employee?.ToString());
                Debug.Assert(employee == null);
            }

            //using (var ctx = new DataContext(tenantId: 2, userId: 66))
            //{
            //    var employee = ctx.Employees.First(x => x.Name == name);
            //    ctx.Employees.Remove(employee);
            //    ctx.SaveChanges();
            //}

            Console.WriteLine("Press any key to exit ...");
            Console.Read();
        }
    }
}
