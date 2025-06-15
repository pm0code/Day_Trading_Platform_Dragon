using Microsoft.EntityFrameworkCore;
using TradingPlatform.Database.Models;

namespace TradingPlatform.Database.Context;

/// <summary>
/// Entity Framework DbContext optimized for TimescaleDB
/// Configured for high-performance time-series data operations
/// </summary>
public class TradingDbContext : DbContext
{
    public TradingDbContext(DbContextOptions<TradingDbContext> options) : base(options)
    {
    }
    
    public DbSet<MarketDataRecord> MarketData { get; set; }
    public DbSet<ExecutionRecord> Executions { get; set; }
    public DbSet<PerformanceMetric> PerformanceMetrics { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure MarketDataRecord for TimescaleDB optimization
        modelBuilder.Entity<MarketDataRecord>(entity =>
        {
            // Composite index for high-frequency queries
            entity.HasIndex(e => new { e.Symbol, e.Timestamp })
                  .HasDatabaseName("idx_market_data_symbol_timestamp");
            
            // Venue-specific queries
            entity.HasIndex(e => new { e.Venue, e.Timestamp })
                  .HasDatabaseName("idx_market_data_venue_timestamp");
            
            // Data type filtering
            entity.HasIndex(e => new { e.DataType, e.Timestamp })
                  .HasDatabaseName("idx_market_data_type_timestamp");
            
            // Sequence number for ordering
            entity.HasIndex(e => new { e.Symbol, e.Timestamp, e.SequenceNumber })
                  .HasDatabaseName("idx_market_data_sequence");
        });
        
        // Configure ExecutionRecord for compliance and performance analysis
        modelBuilder.Entity<ExecutionRecord>(entity =>
        {
            // Primary execution queries
            entity.HasIndex(e => new { e.Symbol, e.ExecutionTime })
                  .HasDatabaseName("idx_executions_symbol_time");
            
            // Order tracking
            entity.HasIndex(e => e.ClientOrderId)
                  .HasDatabaseName("idx_executions_client_order_id");
            
            entity.HasIndex(e => e.ExchangeOrderId)
                  .HasDatabaseName("idx_executions_exchange_order_id");
            
            // Account-based queries
            entity.HasIndex(e => new { e.Account, e.ExecutionTime })
                  .HasDatabaseName("idx_executions_account_time");
            
            // Venue performance analysis
            entity.HasIndex(e => new { e.Venue, e.ExecutionTime })
                  .HasDatabaseName("idx_executions_venue_time");
            
            // Regulatory reporting
            entity.HasIndex(e => e.RegulatoryTransactionId)
                  .HasDatabaseName("idx_executions_regulatory_id");
        });
        
        // Configure PerformanceMetric for system monitoring
        modelBuilder.Entity<PerformanceMetric>(entity =>
        {
            // Performance category analysis
            entity.HasIndex(e => new { e.Category, e.Timestamp })
                  .HasDatabaseName("idx_performance_category_time");
            
            // Operation-specific metrics
            entity.HasIndex(e => new { e.Operation, e.Timestamp })
                  .HasDatabaseName("idx_performance_operation_time");
            
            // Component monitoring
            entity.HasIndex(e => new { e.Component, e.Timestamp })
                  .HasDatabaseName("idx_performance_component_time");
            
            // Symbol-specific performance
            entity.HasIndex(e => new { e.Symbol, e.Timestamp })
                  .HasDatabaseName("idx_performance_symbol_time");
            
            // Venue-specific performance
            entity.HasIndex(e => new { e.Venue, e.Timestamp })
                  .HasDatabaseName("idx_performance_venue_time");
        });
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Configure for high-performance TimescaleDB operations
            optionsBuilder.UseNpgsql(connectionString =>
            {
                connectionString.CommandTimeout(30);
            });
            
            // Optimize for performance
            optionsBuilder.EnableSensitiveDataLogging(false);
            optionsBuilder.EnableDetailedErrors(false);
            optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }
    }
}