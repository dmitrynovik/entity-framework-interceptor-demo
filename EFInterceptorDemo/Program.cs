using System;
using System.Linq;

namespace EFInterceptorDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            var name = Guid.NewGuid().ToString();

            using (var ctx = new DataContext(tenantId: 2, userId: 66))
            {
                // INSERT:
                var employee = new Employee {Name = name, Salary = 1000000};
                ctx.Employees.Add(employee);
                ctx.SaveChanges();

                // UPDATE:
                employee = ctx.Employees.First(x => x.Name == name);
                employee.Salary = employee.Salary + 1;
                ctx.SaveChanges();
            }

            using (var ctx = new DataContext(tenantId: 2, userId: 66))
            {
                // SELECT:
                var employee = ctx.Employees.First(x => x.Name == name);
                Console.WriteLine($"Name: {employee.Name}, Salary: {employee.Salary}, Tenant: {employee.TenantId}, CreatedBy: {employee.CreatedById}, CreatedOn: {employee.CreatedOn}, ModifiedBy: {employee.ModifiedById}, ModifiedAt: {employee.ModifiedOn}");
            }

            Console.WriteLine("Press any key to exit ...");
            Console.Read();
        }
    }
}
