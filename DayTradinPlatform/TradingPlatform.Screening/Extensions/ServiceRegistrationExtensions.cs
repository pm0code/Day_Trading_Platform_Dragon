using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Screening.Canonical;
using TradingPlatform.Screening.Criteria;
using TradingPlatform.Screening.Engines;
using TradingPlatform.Screening.Interfaces;
using TradingPlatform.Screening.Services;

namespace TradingPlatform.Screening.Extensions
{
    /// <summary>
    /// Extension methods for registering screening services with dependency injection.
    /// </summary>
    public static class ServiceRegistrationExtensions
    {
        /// <summary>
        /// Registers all screening services with canonical implementations.
        /// </summary>
        public static IServiceCollection AddScreeningServices(this IServiceCollection services)
        {
            // Register canonical criteria evaluators
            services.AddScoped<ICriteriaEvaluator, PriceCriteriaCanonical>();
            services.AddScoped<ICriteriaEvaluator, VolumeCriteriaCanonical>();
            services.AddScoped<ICriteriaEvaluator, VolatilityCriteriaCanonical>();
            services.AddScoped<ICriteriaEvaluator, GapCriteriaCanonical>();
            services.AddScoped<ICriteriaEvaluator, NewsCriteriaCanonical>();

            // Register screening engine
            services.AddScoped<IScreeningEngine, RealTimeScreeningEngineCanonical>();

            // Register screening orchestrator
            services.AddScoped<ScreeningOrchestratorCanonical>();

            // Register alert service if not already registered
            services.AddScoped<IAlertService, AlertService>();

            return services;
        }

        /// <summary>
        /// Registers legacy (non-canonical) screening services.
        /// This method is deprecated and should only be used for backward compatibility.
        /// </summary>
        [Obsolete("Use AddScreeningServices() to register canonical implementations")]
        public static IServiceCollection AddLegacyScreeningServices(this IServiceCollection services)
        {
            // Register legacy criteria evaluators
            services.AddScoped<ICriteriaEvaluator, PriceCriteria>();
            services.AddScoped<ICriteriaEvaluator, VolumeCriteria>();
            services.AddScoped<ICriteriaEvaluator, VolatilityCriteria>();
            services.AddScoped<ICriteriaEvaluator, GapCriteria>();
            services.AddScoped<ICriteriaEvaluator, NewsCriteria>();

            // Register legacy screening engine
            services.AddScoped<IScreeningEngine, RealTimeScreeningEngine>();

            // Register legacy screening orchestrator
            services.AddScoped<ScreeningOrchestrator>();

            // Register alert service if not already registered
            services.AddScoped<IAlertService, AlertService>();

            return services;
        }

        /// <summary>
        /// Registers screening services with custom implementations.
        /// Useful for testing or when you need to override specific evaluators.
        /// </summary>
        public static IServiceCollection AddScreeningServicesWithOverrides(
            this IServiceCollection services,
            Action<ScreeningServiceBuilder>? configure = null)
        {
            var builder = new ScreeningServiceBuilder(services);

            // Register default canonical implementations
            builder.AddCriteriaEvaluator<PriceCriteriaCanonical>()
                   .AddCriteriaEvaluator<VolumeCriteriaCanonical>()
                   .AddCriteriaEvaluator<VolatilityCriteriaCanonical>()
                   .AddCriteriaEvaluator<GapCriteriaCanonical>()
                   .AddCriteriaEvaluator<NewsCriteriaCanonical>()
                   .AddScreeningEngine<RealTimeScreeningEngineCanonical>()
                   .AddOrchestrator<ScreeningOrchestratorCanonical>()
                   .AddAlertService<AlertService>();

            // Apply custom configuration if provided
            configure?.Invoke(builder);

            return services;
        }
    }

    /// <summary>
    /// Builder for configuring screening services.
    /// </summary>
    public class ScreeningServiceBuilder
    {
        private readonly IServiceCollection _services;
        private readonly HashSet<Type> _registeredEvaluators = new();

        public ScreeningServiceBuilder(IServiceCollection services)
        {
            _services = services;
        }

        /// <summary>
        /// Adds a criteria evaluator implementation.
        /// </summary>
        public ScreeningServiceBuilder AddCriteriaEvaluator<T>() where T : class, ICriteriaEvaluator
        {
            var type = typeof(T);
            if (!_registeredEvaluators.Contains(type))
            {
                _services.AddScoped<ICriteriaEvaluator, T>();
                _registeredEvaluators.Add(type);
            }
            return this;
        }

        /// <summary>
        /// Replaces all criteria evaluator implementations with a custom one.
        /// </summary>
        public ScreeningServiceBuilder ReplaceCriteriaEvaluators<T>() where T : class, ICriteriaEvaluator
        {
            // Remove existing registrations
            var descriptorsToRemove = _services
                .Where(d => d.ServiceType == typeof(ICriteriaEvaluator))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                _services.Remove(descriptor);
            }

            _registeredEvaluators.Clear();
            return AddCriteriaEvaluator<T>();
        }

        /// <summary>
        /// Adds a screening engine implementation.
        /// </summary>
        public ScreeningServiceBuilder AddScreeningEngine<T>() where T : class, IScreeningEngine
        {
            _services.AddScoped<IScreeningEngine, T>();
            return this;
        }

        /// <summary>
        /// Adds an orchestrator implementation.
        /// </summary>
        public ScreeningServiceBuilder AddOrchestrator<T>() where T : class
        {
            _services.AddScoped<T>();
            return this;
        }

        /// <summary>
        /// Adds an alert service implementation.
        /// </summary>
        public ScreeningServiceBuilder AddAlertService<T>() where T : class, IAlertService
        {
            _services.AddScoped<IAlertService, T>();
            return this;
        }
    }
}