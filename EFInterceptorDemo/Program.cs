﻿using System;
using System.Diagnostics;
using System.Linq;

namespace EFInterceptorDemo
{
    class Program
    {
        static void Main()
        {
            var name = Guid.NewGuid().ToString();

            //using (var ctx = new DataContext(2, 2))
            //{
            //    var stopwatch = Stopwatch.StartNew();
            //    const int tries = 1000;
            //    for (int i = 0; i < tries; i++)
            //    {
            //        var obj = ctx.Employees.FirstOrDefault(x => x.Id == i);
            //    }
            //    stopwatch.Stop();
            //    Console.Write("Elapsed: {0}", stopwatch.Elapsed);
            //}
            //Console.Read();

            Employee employee = null;
            using (var ctx = new DataContext(tenantId: 2, userId: 66))
            {
                Console.WriteLine("adding new employee with name {0} to tenant {1}", name, ctx.TenantId);
                // INSERT (will assign automatically TenantId, CreatedBy, CreatedOn, ModifiedBy, ModifiedOn):
                employee = new Employee {Name = name, Salary = 100};
                ctx.Employees.Add(employee);
                Debug.Assert(ctx.SaveChanges() == 1);
            }

            using (var ctx = new DataContext(tenantId: 2, userId: 77))
            {
                // UPDATE: (will verify TenantId, modify ModifiedBy, ModifiedOn):
                employee = ctx.Employees.First(x => x.Name == name);
                var salary = employee.Salary + 1;
                employee.Salary = salary;
                var recNum = ctx.SaveChanges();
                Console.WriteLine("\nupdating employee {0} with salary {1} updated {2} records", name, salary, recNum);
                Debug.Assert(recNum == 1);
            }

            using (var ctx = new DataContext(tenantId: 3, userId: 77))
            {
                var salary = employee.Salary + 1;
                employee.Salary = salary;
                var recNum = ctx.SaveChanges();
                Console.WriteLine("\nupdating employee {0} with salary {1} updated {2} records", name, salary, recNum);
                Debug.Assert(recNum == 0);
            }

            using (var ctx = new DataContext(tenantId: 2, userId: 66))
            {
                // SELECT (will automatically add TenantId = 2 clause):
                employee = ctx.Employees.First(x => x.Name == name);
                Console.WriteLine($"Name: {employee.Name}, Salary: {employee.Salary}, Tenant: {employee.TenantId}, CreatedBy: {employee.CreatedById}, CreatedOn: {employee.CreatedOn}, ModifiedBy: {employee.ModifiedById}, ModifiedAt: {employee.ModifiedOn}");
            }

            using (var ctx = new DataContext(tenantId: 3, userId: 66))
            {
                // SELECT with different tenant returns NULL:
                Console.WriteLine($"\nselecting employee {name} with tenant id {ctx.TenantId} will return NULL:");
                employee = ctx.Employees.FirstOrDefault(x => x.Name == name);
                Console.WriteLine(employee?.ToString());
                Debug.Assert(employee == null);
            }

            using (var ctx = new DataContext(tenantId: 2, userId: 66))
            {
                employee = ctx.Employees.First(x => x.Name == name);
                ctx.Employees.Remove(employee);
                Debug.Assert(ctx.SaveChanges() == 1);
            }

            Console.WriteLine("Press any key to exit ...");
            Console.Read();
        }
    }
}
