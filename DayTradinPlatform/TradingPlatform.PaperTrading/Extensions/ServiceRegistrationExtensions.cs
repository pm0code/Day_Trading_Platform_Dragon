using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.PaperTrading.Services;

namespace TradingPlatform.PaperTrading.Extensions
{
    /// <summary>
    /// Extension methods for registering paper trading services with dependency injection.
    /// </summary>
    public static class ServiceRegistrationExtensions
    {
        /// <summary>
        /// Registers all paper trading services with canonical implementations.
        /// </summary>
        public static IServiceCollection AddPaperTradingServices(this IServiceCollection services)
        {
            // Register canonical implementations
            services.AddScoped<IOrderExecutionEngine, OrderExecutionEngineCanonical>();
            services.AddScoped<IOrderBookSimulator, OrderBookSimulatorCanonical>();
            services.AddScoped<IPortfolioManager, PortfolioManagerCanonical>();
            services.AddScoped<ISlippageCalculator, SlippageCalculatorCanonical>();
            services.AddScoped<IExecutionAnalytics, ExecutionAnalyticsCanonical>();
            services.AddScoped<IPaperTradingService, PaperTradingServiceCanonical>();

            // Register background service for order processing
            services.AddHostedService<OrderProcessingBackgroundService>();

            return services;
        }

        /// <summary>
        /// Registers legacy (non-canonical) paper trading services.
        /// This method is deprecated and should only be used for backward compatibility.
        /// </summary>
        [Obsolete("Use AddPaperTradingServices() to register canonical implementations")]
        public static IServiceCollection AddLegacyPaperTradingServices(this IServiceCollection services)
        {
            // Register legacy implementations
            services.AddScoped<IOrderExecutionEngine, OrderExecutionEngine>();
            services.AddScoped<IOrderBookSimulator, OrderBookSimulator>();
            services.AddScoped<IPortfolioManager, PortfolioManager>();
            
            // Register other services
            services.AddScoped<ISlippageCalculator, SlippageCalculator>();
            services.AddScoped<IExecutionAnalytics, ExecutionAnalytics>();
            services.AddScoped<IPaperTradingService, PaperTradingService>();

            // Register background service
            services.AddHostedService<OrderProcessingBackgroundService>();

            return services;
        }

        /// <summary>
        /// Registers paper trading services with custom implementations.
        /// Useful for testing or when you need to override specific services.
        /// </summary>
        public static IServiceCollection AddPaperTradingServicesWithOverrides(
            this IServiceCollection services,
            Action<PaperTradingServiceBuilder>? configure = null)
        {
            var builder = new PaperTradingServiceBuilder(services);

            // Register default canonical implementations
            builder.AddOrderExecutionEngine<OrderExecutionEngineCanonical>()
                   .AddOrderBookSimulator<OrderBookSimulatorCanonical>()
                   .AddPortfolioManager<PortfolioManagerCanonical>()
                   .AddSlippageCalculator<SlippageCalculatorCanonical>()
                   .AddExecutionAnalytics<ExecutionAnalyticsCanonical>()
                   .AddPaperTradingService<PaperTradingServiceCanonical>();

            // Apply custom configuration if provided
            configure?.Invoke(builder);

            // Register background service
            services.AddHostedService<OrderProcessingBackgroundService>();

            return services;
        }
    }

    /// <summary>
    /// Builder for configuring paper trading services.
    /// </summary>
    public class PaperTradingServiceBuilder
    {
        private readonly IServiceCollection _services;

        public PaperTradingServiceBuilder(IServiceCollection services)
        {
            _services = services;
        }

        /// <summary>
        /// Replaces the order execution engine implementation.
        /// </summary>
        public PaperTradingServiceBuilder AddOrderExecutionEngine<T>() where T : class, IOrderExecutionEngine
        {
            _services.AddScoped<IOrderExecutionEngine, T>();
            return this;
        }

        /// <summary>
        /// Replaces the order book simulator implementation.
        /// </summary>
        public PaperTradingServiceBuilder AddOrderBookSimulator<T>() where T : class, IOrderBookSimulator
        {
            _services.AddScoped<IOrderBookSimulator, T>();
            return this;
        }

        /// <summary>
        /// Replaces the portfolio manager implementation.
        /// </summary>
        public PaperTradingServiceBuilder AddPortfolioManager<T>() where T : class, IPortfolioManager
        {
            _services.AddScoped<IPortfolioManager, T>();
            return this;
        }

        /// <summary>
        /// Replaces the slippage calculator implementation.
        /// </summary>
        public PaperTradingServiceBuilder AddSlippageCalculator<T>() where T : class, ISlippageCalculator
        {
            _services.AddScoped<ISlippageCalculator, T>();
            return this;
        }

        /// <summary>
        /// Replaces the execution analytics implementation.
        /// </summary>
        public PaperTradingServiceBuilder AddExecutionAnalytics<T>() where T : class, IExecutionAnalytics
        {
            _services.AddScoped<IExecutionAnalytics, T>();
            return this;
        }

        /// <summary>
        /// Replaces the paper trading service implementation.
        /// </summary>
        public PaperTradingServiceBuilder AddPaperTradingService<T>() where T : class, IPaperTradingService
        {
            _services.AddScoped<IPaperTradingService, T>();
            return this;
        }

        /// <summary>
        /// Adds a custom service to the container.
        /// </summary>
        public PaperTradingServiceBuilder AddCustomService<TInterface, TImplementation>()
            where TInterface : class
            where TImplementation : class, TInterface
        {
            _services.AddScoped<TInterface, TImplementation>();
            return this;
        }
    }
}