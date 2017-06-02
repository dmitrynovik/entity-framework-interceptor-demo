using System;

namespace EFInterceptorDemo
{
    public interface ITenant
    {
        long TenantId { get; set; }
    }

    public interface IEntity
    {
        DateTime CreatedOn { get; set; }
        long CreatedById { get; set; }
        DateTime ModifiedOn { get; set; }
        long ModifiedById { get; set; }
    }

    public interface ITenantedEntity : ITenant, IEntity {  }

    public abstract class Entity : IEntity
    {
        public long CreatedById { get; set; }
        public DateTime CreatedOn { get; set; }
        public long ModifiedById { get; set; }
        public DateTime ModifiedOn { get; set; }
    }

    public abstract class TenantedEntity : Entity, ITenantedEntity
    {
        public long TenantId { get; set; }
    }

    public class Employee : TenantedEntity
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public decimal Salary { get; set; }
    }
}
