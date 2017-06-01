using System;

namespace EFInterceptorDemo
{
    public interface ITenant
    {
        long TenantId { get; set; }
    }

    public interface IEntity
    {
        DateTime CreatedAt { get; set; }
        long CreatedBy { get; set; }
        DateTime ModifiedAt { get; set; }
        long ModifiedBy { get; set; }
    }

    public interface ITenantedEntity : ITenant, IEntity {  }

    public abstract class Entity : IEntity
    {
        public long CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public long ModifiedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
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
