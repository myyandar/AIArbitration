using AIArbitration.Core;
using AIArbitration.Core.Entities;
using AIArbitration.Core.Entities.Enums;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using AIArbitration.Infrastructure.Repositories;
using AIArbitration.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace AIArbitration.Infrastructure.Services
{
    public interface IUnitOfWork : IDisposable
    {
        // DbContext access
        AIArbitrationDbContext Context { get; }

        // Repository properties
        IModelRepository Models { get; }
        ITenantService Tenants { get; }
        IUserService Users { get; }
        IBudgetService Budgets { get; }
        //IUsageService Usage { get; }
        IComplianceService Compliance { get; }
        IArbitrationEngine Arbitration { get; }

        // Transaction management
        Task<IDbContextTransaction> BeginTransactionAsync(
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default);

        Task CommitTransactionAsync(
            IDbContextTransaction transaction,
            CancellationToken cancellationToken = default);

        Task RollbackTransactionAsync(
            IDbContextTransaction transaction,
            CancellationToken cancellationToken = default);

        // Save changes
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        // Database operations
        Task ExecuteSqlRawAsync(string sql, params object[] parameters);
        Task<T> ExecuteSqlRawAsync<T>(string sql, params object[] parameters);

        // Health check
        Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);

        // Cache management
        Task ClearChangeTrackerAsync();

        // Audit trail
        Task<IEnumerable<ErrorLog>> GetAuditLogsAsync(
            string entityType,
            string entityId,
            DateTime? startDate = null,
            DateTime? endDate = null);
    }

    public class UnitOfWork : IUnitOfWork
    {
        private readonly AIArbitrationDbContext _context;
        private bool _disposed = false;

        // Lazy-loaded repositories
        private IModelRepository _models;
        private ITenantService _tenants;
        private IUserService _users;
        private IBudgetService _budgets;
        //private IUsageRepository _usage;
        private IComplianceService _compliance;
        private IArbitrationEngine _arbitration;
        private ILogger<UnitOfWork> _logger;

        public UnitOfWork(AIArbitrationDbContext context, 
            ILogger<UnitOfWork> logger, 
            IModelRepository models,
            ITenantService tenants,
            IUserService users,
            IBudgetService _budgets,
            IComplianceService _compliance,
            IArbitrationEngine _arbitration)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger;
            _models = models;
            _tenants = tenants;
            _users = users;
            this._budgets = _budgets;
            this._compliance = _compliance;
            this._arbitration = _arbitration;
        }

        public AIArbitrationDbContext Context => _context;

        public IModelRepository Models => _models;

        public ITenantService Tenants => _tenants;

        public IUserService Users => _users;

        public IBudgetService Budgets => _budgets;

        //public IUsageRepository Usage => _usage;

        public IComplianceService Compliance => _compliance;

        public IArbitrationEngine Arbitration => _arbitration;

        public async Task<IDbContextTransaction> BeginTransactionAsync(
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default)
        {
            return await _context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        }

        public async Task CommitTransactionAsync(
            IDbContextTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            await transaction.CommitAsync(cancellationToken);
            await transaction.DisposeAsync();
        }

        public async Task RollbackTransactionAsync(
            IDbContextTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            await transaction.RollbackAsync(cancellationToken);
            await transaction.DisposeAsync();
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Add audit trail before saving
                AddAuditTrail();

                // Set timestamps
                SetTimestamps();

                return await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Handle concurrency conflicts
                throw new UnitOfWorkException("Concurrency conflict occurred while saving changes", ex);
            }
            catch (DbUpdateException ex)
            {
                // Handle database update errors
                throw new UnitOfWorkException("Database update error occurred", ex);
            }
        }

        public async Task ExecuteSqlRawAsync(string sql, params object[] parameters)
        {
            await _context.Database.ExecuteSqlRawAsync(sql, parameters);
        }

        public async Task<T> ExecuteSqlRawAsync<T>(string sql, params object[] parameters)
        {
            // For scalar queries
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = sql;

                if (parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }

                var result = await command.ExecuteScalarAsync();
                return (T)Convert.ChangeType(result, typeof(T));
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Database.CanConnectAsync(cancellationToken);
            }
            catch
            {
                return false;
            }
        }

        public async Task ClearChangeTrackerAsync()
        {
            _context.ChangeTracker.Clear();
            await Task.CompletedTask;
        }

        public async Task<IEnumerable<ErrorLog>> GetAuditLogsAsync(
            string entityType,
            string entityId,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var query = _context.ErrorLogs
                .Where(a => a.EntityType == entityType && a.EntityId == entityId);

            if (startDate.HasValue)
                query = query.Where(a => a.CreatedAt >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(a => a.CreatedAt <= endDate.Value);

            return await query
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        private void AddAuditTrail()
        {
            var entries = _context.ChangeTracker.Entries()
                .Where(e => e.Entity is IAuditableEntity &&
                           (e.State == EntityState.Added ||
                            e.State == EntityState.Modified ||
                            e.State == EntityState.Deleted))
                .ToList();

            foreach (var entry in entries)
            {
                var errorLog = new ErrorLog
                {
                    Id = Guid.NewGuid().ToString(),
                    EntityType = entry.Entity.GetType().Name,
                    EntityId = ((dynamic)entry.Entity).Id?.ToString(),
                    Action = entry.State.ToString(),
                    Changes = GetChanges(entry),
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = GetCurrentUserId() // You'd need to implement this
                };

                _context.ErrorLogs.Add(errorLog);
            }
        }

        private void SetTimestamps()
        {
            var entries = _context.ChangeTracker.Entries()
                .Where(e => e.Entity is ITimestampedEntity &&
                           (e.State == EntityState.Added || e.State == EntityState.Modified))
                .ToList();

            var now = DateTime.UtcNow;
            var userId = GetCurrentUserId();

            foreach (var entry in entries)
            {
                var entity = (ITimestampedEntity)entry.Entity;

                if (entry.State == EntityState.Added)
                {
                    entity.CreatedAt = now;
                    entity.CreatedBy = userId;
                }

                entity.UpdatedAt = now;
                entity.UpdatedBy = userId;
            }
        }

        private string GetChanges(EntityEntry entry)
        {
            var changes = new Dictionary<string, object>();

            foreach (var property in entry.Properties)
            {
                if (property.IsModified || entry.State == EntityState.Added)
                {
                    var originalValue = entry.State == EntityState.Added ? null : property.OriginalValue;
                    var currentValue = property.CurrentValue;

                    changes[property.Metadata.Name] = new
                    {
                        Original = originalValue,
                        Current = currentValue
                    };
                }
            }

            return System.Text.Json.JsonSerializer.Serialize(changes);
        }

        private string GetCurrentUserId()
        {
            // Implement your user context access logic
            // This is just a placeholder
            return "system"; // Or get from IHttpContextAccessor
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _context?.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    // Supporting interfaces
    public interface IAuditableEntity
    {
        // Marker interface for entities that need audit trail
    }

    public interface ITimestampedEntity
    {
        DateTime CreatedAt { get; set; }
        string CreatedBy { get; set; }
        DateTime UpdatedAt { get; set; }
        string UpdatedBy { get; set; }
    }

    // Custom exception
    public class UnitOfWorkException : Exception
    {
        public UnitOfWorkException() { }
        public UnitOfWorkException(string message) : base(message) { }
        public UnitOfWorkException(string message, Exception inner) : base(message, inner) { }
    }
}
