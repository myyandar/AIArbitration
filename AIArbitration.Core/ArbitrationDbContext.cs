using AIArbitration.Core.Entities;
using AIArbitration.Core.Interfaces;
using AIArbitration.Core.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AIArbitration.Core
{
    public class AIArbitrationDbContext : IdentityDbContext
    {
        private readonly IEncryptionService _encryptionService;
        public AIArbitrationDbContext(DbContextOptions<AIArbitrationDbContext> options,
            IEncryptionService encryptionService = null) : base(options) 
        {
            _encryptionService = encryptionService;
        }

        private string? Encrypt(string? value)
        {
            if (_encryptionService != null)
                return _encryptionService.Encrypt(value);

            // Fallback for development
            if (string.IsNullOrEmpty(value)) return value;
            return $"encrypted:{value}"; // Simple placeholder
        }

        private string? Decrypt(string? value)
        {
            if (_encryptionService != null)
                return _encryptionService.Decrypt(value);

            // Fallback for development
            if (string.IsNullOrEmpty(value) || !value.StartsWith("encrypted:"))
                return value;
            return value.Substring("encrypted:".Length);
        }

        public DbSet<AIModel> AIModels => Set<AIModel>();
        public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
        public DbSet<ApiRequest> ApiRequests => Set<ApiRequest>();
        public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();
        public DbSet<ArbitrationCandidate> ArbitrationCandidates => Set<ArbitrationCandidate>();
        public DbSet<ArbitrationDecision> ArbitrationDecisions => Set<ArbitrationDecision>();
        public DbSet<ArbitrationResult> ArbitrationResults => Set<ArbitrationResult>();
        public DbSet<ArbitrationRule> ArbitrationRules => Set<ArbitrationRule>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
        public DbSet<AuditTrail> AuditTrails => Set<AuditTrail>();
        public DbSet<BudgetAllocation> BudgetAllocations => Set<BudgetAllocation>();
        public DbSet<BudgetNotification> BudgetNotifications => Set<BudgetNotification>();
        public DbSet<ChatRequest> ChatRequests => Set<ChatRequest>();
        public DbSet<CircuitBreaker> CircuitBreakers => Set<CircuitBreaker>();
        public DbSet<CircuitBreakerConfig> CircuitBreakerConfigs => Set<CircuitBreakerConfig>();
        public DbSet<CircuitBreakerEvent> CircuitBreakerEvents => Set<CircuitBreakerEvent>();
        public DbSet<CircuitBreakerOptions> CircuitBreakerOptions => Set<CircuitBreakerOptions>();
        public DbSet<CircuitBreakerStatistics> CircuitBreakerStatistics => Set<CircuitBreakerStatistics>();
        public DbSet<CircuitBreakerWindow> CircuitBreakerWindows => Set<CircuitBreakerWindow>();
        public DbSet<ComplianceCheckResult> ComplianceCheckResults => Set<ComplianceCheckResult>();
        public DbSet<ComplianceConfiguration> ComplianceConfigurations => Set<ComplianceConfiguration>();
        public DbSet<ComplianceLog> ComplianceLogs => Set<ComplianceLog>();
        public DbSet<ComplianceRule> ComplianceRules => Set<ComplianceRule>();
        public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
        public DbSet<ConfigurationChangeLog> ConfigurationChangeLogs => Set<ConfigurationChangeLog>();
        public DbSet<CostEstimation> CostEstimations => Set<CostEstimation>();
        public DbSet<CostRecord> CostRecords => Set<CostRecord>();
        public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();
        public DbSet<DataRequest> DataRequests => Set<DataRequest>();
        public DbSet<ExecutionLog> ExecutionLogs => Set<ExecutionLog>();
        public DbSet<ModelCapability> ModelCapabilities => Set<ModelCapability>();
        public DbSet<ModelCapability> ModelCapability => Set<ModelCapability>();
        public DbSet<ModelProvider> ModelProviders => Set<ModelProvider>();
        public DbSet<PerformanceAnalysis> PerformanceAnalysis => Set<PerformanceAnalysis>();
        public DbSet<PredictionTrainingLog> PredictionTrainingLogs => Set<PredictionTrainingLog>();
        public DbSet<PerformanceDataPoint> PerformanceDataPoints => Set<PerformanceDataPoint>();
        public DbSet<PerformancePrediction> PerformancePredictions => Set<PerformancePrediction>();
        public DbSet<PricingInfo> PricingInfos => Set<PricingInfo>();
        public DbSet<Project> Projects => Set<Project>();
        public DbSet<ProviderConfiguration> ProviderConfigurations => Set<ProviderConfiguration>();
        public DbSet<ProviderHealth> ProviderHealth => Set<ProviderHealth>();
        public DbSet<ProviderIncident> ProviderIncidents => Set<ProviderIncident>();
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();
        public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();
        public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
        public DbSet<UserRole> UserRoles => Set<UserRole>();
        public DbSet<UserSession> UserSessions => Set<UserSession>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProviderHealth>(entity =>
            {
                entity.HasIndex(h => new { h.ProviderId, h.CheckedAt });
                entity.HasOne(h => h.Provider)
                      .WithMany(p => p.HealthMetrics)
                      .HasForeignKey(h => h.ProviderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Provider Configuration Configuration
            modelBuilder.Entity<ProviderConfiguration>(entity =>
            {
                entity.HasIndex(c => c.ProviderId).IsUnique();
                entity.HasOne(c => c.Provider)
                      .WithOne(p => p.Configuration)
                      .HasForeignKey<ProviderConfiguration>(c => c.ProviderId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Configure encryption for sensitive fields
                entity.Property(c => c.ApiKey)
                      .HasConversion(
                          v => Encrypt(v), // Encrypt on save
                          v => Decrypt(v)  // Decrypt on read
                      );

                entity.Property(c => c.SecretKey)
                      .HasConversion(
                          v => Encrypt(v),
                          v => Decrypt(v)
                      );
            });

            // Provider Incident Configuration
            modelBuilder.Entity<ProviderIncident>(entity =>
            {
                entity.HasIndex(i => new { i.ProviderId, i.DetectedAt });
                entity.HasOne(i => i.Provider)
                      .WithMany(p => p.Incidents)
                      .HasForeignKey(i => i.ProviderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AIModel>(entity =>
            {
                entity.HasIndex(m => new { m.ProviderId, m.ProviderModelId }).IsUnique();
                entity.HasOne(m => m.Provider)
                      .WithMany(p => p.Models)
                      .HasForeignKey(m => m.ProviderId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(m => m.CostPerMillionInputTokens)
                      .HasPrecision(18, 6);
                entity.Property(m => m.CostPerMillionOutputTokens)
                      .HasPrecision(18, 6);
            });

            modelBuilder.Entity<ModelCapability>(entity =>
            {
                entity.HasIndex(c => new { c.ModelId, c.CapabilityType }).IsUnique();
            });

            // CostRecord Configuration
            modelBuilder.Entity<CostRecord>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Indexes for query performance
                entity.HasIndex(e => new { e.TenantId, e.CreatedAt });
                entity.HasIndex(e => new { e.TenantId, e.BillingPeriod });
                entity.HasIndex(e => new { e.TenantId, e.ProjectId, e.CreatedAt });
                entity.HasIndex(e => new { e.TenantId, e.UserId, e.CreatedAt });
                entity.HasIndex(e => new { e.ModelId, e.CreatedAt });
                entity.HasIndex(e => new { e.ProviderId, e.CreatedAt });
                entity.HasIndex(e => e.InvoiceId);
                entity.HasIndex(e => e.PaymentStatus);
                entity.HasIndex(e => e.RecordType);
                entity.HasIndex(e => e.ResourceType);
                entity.HasIndex(e => e.ServiceName);
                entity.HasIndex(e => e.IsInvoiced);
                entity.HasIndex(e => new { e.IsEstimated, e.CreatedAt });

                // Configure precision for monetary values
                entity.Property(e => e.Amount)
                      .HasPrecision(18, 6);

                entity.Property(e => e.TaxAmount)
                      .HasPrecision(18, 6);

                entity.Property(e => e.DiscountAmount)
                      .HasPrecision(18, 6);

                entity.Property(e => e.Rate)
                      .HasPrecision(18, 6);

                entity.Property(e => e.Quantity)
                      .HasPrecision(18, 6);

                // JSON columns for complex types
                entity.Property(e => e.Metadata)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions)null) ?? new()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.Tags)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                // Computed columns
                entity.Property(e => e.TotalAmount)
                      .HasComputedColumnSql("[Amount] + [TaxAmount] - [DiscountAmount]");

                entity.Property(e => e.TotalTokens)
                      .HasComputedColumnSql("COALESCE([InputTokens], 0) + COALESCE([OutputTokens], 0)");

                // Default values
                entity.Property(e => e.RecordType)
                      .HasDefaultValue("model_usage");

                entity.Property(e => e.Currency)
                      .HasDefaultValue("USD");

                entity.Property(e => e.PaymentStatus)
                      .HasDefaultValue("pending");

                entity.Property(e => e.RateUnit)
                      .HasDefaultValue("tokens");

                entity.Property(e => e.ServiceName)
                      .HasDefaultValue("ai_arbitration");

                entity.Property(e => e.RetentionDays)
                      .HasDefaultValue(1825);

                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("GETUTCDATE()");

                // Relationships
                entity.HasOne(e => e.Tenant)
                      .WithMany()
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Project)
                      .WithMany(p => p.CostRecords)
                      .HasForeignKey(e => e.ProjectId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.User)
                      .WithMany(u => u.CostRecords)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ApiKey)
                      .WithMany()
                      .HasForeignKey(e => e.ApiKeyId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Session)
                      .WithMany()
                      .HasForeignKey(e => e.SessionId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TenantSetting>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => new { e.TenantId, e.SettingKey }).IsUnique();
                entity.HasIndex(e => e.Category);

                entity.Property(e => e.SettingValue)
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.AllowedValues)
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.ValidationRules)
                      .HasColumnType("nvarchar(max)");

                entity.HasOne(e => e.Tenant)
                      .WithMany(t => t.Settings)
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ComplianceRule Configuration
            modelBuilder.Entity<ComplianceRule>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => new { e.TenantId, e.RuleType });
                entity.HasIndex(e => new { e.TenantId, e.Standard });
                entity.HasIndex(e => e.IsEnabled);
                entity.HasIndex(e => e.Priority);

                entity.Property(e => e.Condition)
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.ActionParameters)
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.AppliedToResources)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                // Similar for other JSON properties...
                entity.Property(e => e.AppliedToUsers)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.NotificationChannels)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.Tags)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.HasOne(e => e.Tenant)
                      .WithMany(t => t.ComplianceRules)
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ComplianceLog Configuration
            modelBuilder.Entity<ComplianceLog>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => new { e.TenantId, e.CreatedAt });
                entity.HasIndex(e => new { e.RuleId, e.CreatedAt });

                entity.Property(e => e.Result)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<ComplianceCheckResult>(v, (JsonSerializerOptions)null) ?? new()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.HasOne(e => e.Rule)
                      .WithMany(r => r.ComplianceLogs)
                      .HasForeignKey(e => e.RuleId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Tenant)
                      .WithMany()
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<UserSession>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Indexes
                entity.HasIndex(e => e.SessionToken).IsUnique();
                entity.HasIndex(e => e.RefreshToken).IsUnique();
                entity.HasIndex(e => new { e.UserId, e.Status });
                entity.HasIndex(e => new { e.TenantId, e.Status, e.ExpiresAt });
                entity.HasIndex(e => e.IPAddress);
                entity.HasIndex(e => e.Country);
                entity.HasIndex(e => e.LastActivityAt);

                // JSON columns
                entity.Property(e => e.Scopes)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.Permissions)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.RiskFactors)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                // Relationships
                entity.HasOne(e => e.User)
                      .WithMany(u => u.Sessions)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Tenant)
                      .WithMany()
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ArbitrationRule Configuration
            modelBuilder.Entity<ArbitrationRule>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Indexes
                entity.HasIndex(e => new { e.TenantId, e.IsEnabled, e.Priority });
                entity.HasIndex(e => new { e.TenantId, e.ProjectId, e.IsEnabled });
                entity.HasIndex(e => e.RuleType);
                entity.HasIndex(e => e.TaskType);
                entity.HasIndex(e => e.OptimizationStrategy);
                entity.HasIndex(e => new { e.IsEnabled, e.LastMatchedAt });

                // JSON columns
                entity.Property(e => e.DaysOfWeek)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.RequiredKeywords)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.ExcludedKeywords)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.AllowedProviders)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.BlockedProviders)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.AllowedModels)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.BlockedModels)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.RequiredCapabilities)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<Dictionary<string, decimal>>(v, (JsonSerializerOptions)null) ?? new()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.RequiredFeatures)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.RequiredRegions)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.ComplianceStandards)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.StrategyWeights)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<Dictionary<string, decimal>>(v, (JsonSerializerOptions)null) ?? new()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.NotificationChannels)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.Tags)
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                          v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>()
                      )
                      .HasColumnType("nvarchar(max)");

                // Relationships
                entity.HasOne(e => e.Tenant)
                      .WithMany()
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Project)
                      .WithMany()
                      .HasForeignKey(e => e.ProjectId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Default values
                entity.Property(e => e.Priority).HasDefaultValue(100);
                entity.Property(e => e.ExecutionOrder).HasDefaultValue(1);
                entity.Property(e => e.IsEnabled).HasDefaultValue(true);
                entity.Property(e => e.ConditionType).HasDefaultValue("always");
                entity.Property(e => e.OptimizationStrategy).HasDefaultValue("balanced");
                entity.Property(e => e.FallbackStrategy).HasDefaultValue("next_best");
                entity.Property(e => e.MaxFallbackAttempts).HasDefaultValue(3);
                entity.Property(e => e.AllowProviderFallback).HasDefaultValue(true);
                entity.Property(e => e.ActionType).HasDefaultValue("select_model");
                entity.Property(e => e.CostMultiplier).HasDefaultValue(1.0m);
                entity.Property(e => e.SendNotification).HasDefaultValue(false);
                entity.Property(e => e.Version).HasDefaultValue("1.0");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // Configure relationships and indexes
            modelBuilder.Entity<AIModel>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ProviderModelId);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.Tier);
                entity.HasIndex(e => e.LastUpdated);

                entity.HasOne(e => e.Provider)
                    .WithMany(p => p.Models)
                    .HasForeignKey(e => e.ProviderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.PricingInfo)
                    .WithMany()
                    .HasForeignKey(e => e.PricingInfoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.Capabilities)
                    .WithOne(c => c.Model)
                    .HasForeignKey(c => c.ModelId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ModelProvider>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.Tier);

                entity.HasOne(e => e.Configuration)
                    .WithOne(c => c.Provider)
                    .HasForeignKey<ProviderConfiguration>(c => c.ProviderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ProviderHealth>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ProviderId);
                entity.HasIndex(e => e.CheckedAt);
                entity.HasIndex(e => e.ProviderHealthStatus);

                entity.HasOne(e => e.Provider)
                    .WithMany(p => p.HealthMetrics)
                    .HasForeignKey(e => e.ProviderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PerformanceAnalysis>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ModelId);
                entity.HasIndex(e => e.AnalysisPeriodStart);
                entity.HasIndex(e => e.AnalysisPeriodEnd);

            });

            modelBuilder.Entity<ArbitrationDecision>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.SelectedModelId);

                entity.HasOne(e => e.SelectedModel)
                    .WithMany()
                    .HasForeignKey(e => e.SelectedModelId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Tenant)
                    .WithMany(t => t.ArbitrationDecisions)
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure JSON serialization for complex properties
            modelBuilder.Entity<PerformanceAnalysis>()
                .Property(e => e.SuccessRateByTaskType)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, decimal>>(v, (JsonSerializerOptions)null));

            modelBuilder.Entity<PricingInfo>()
                .Property(e => e.AdditionalCharges)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, decimal>>(v, (JsonSerializerOptions)null));

            modelBuilder.Entity<ArbitrationDecision>()
                .Property(e => e.AdditionalData)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions)null));
            // Additional configurations...
            base.OnModelCreating(modelBuilder);
        }
    }
}
