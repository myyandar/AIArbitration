using AIArbitration.Core.Entities;
using AIArbitration.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Infrastructure.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<ModelProvider> Providers { get; }
        IRepository<AIModel> Models { get; }
        IRepository<ArbitrationRule> Rules { get; }
        IRepository<AuditLog> AuditLogs { get; }

        Task<int> SaveChangesAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}
