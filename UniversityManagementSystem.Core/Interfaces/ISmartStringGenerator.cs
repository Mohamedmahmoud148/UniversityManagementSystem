using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface ISmartStringGenerator
    {
        Task<string> GenerateUniqueAsync<TEntity>(string baseValue, Expression<Func<TEntity, string>> selector) where TEntity : class;
    }
}
