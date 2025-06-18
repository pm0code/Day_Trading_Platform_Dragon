using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml.Navigation;
using TradingPlatform.Logging.Configuration;
using TradingPlatform.Logging.Interfaces;
using TradingPlatform.Logging.Services;
using TradingPlatform.TradingApp.Services;
using TradingPlatform.TradingApp.Views;
using TradingPlatform.DisplayManagement.Extensions;

namespace TradingPlatform.TradingApp
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;
        private IHost? _host;

        /// <summary>
        /// Current application instance
        /// </summary>
        public static new App Current => (App)Application.Current;

        /// <summary>
        /// Application services
        /// </summary>
        public IServiceProvider Services => _host?.Services ?? throw new InvalidOperationException("Services not initialized");

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            
            // Configure dependency injection and services
            ConfigureServices();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            // Start the host
            await _host!.StartAsync();
            
            // Create main control window
            _window = new TradingControlWindow(_host.Services);
            _window.Activate();
        }

        private void ConfigureServices()
        {
            var builder = Host.CreateApplicationBuilder();
            
            // Configure logging
            LoggingConfiguration.ConfigureLogging(builder.Services, "TradingPlatform.TradingApp");
            
            // Register centralized display management services
            builder.Services.AddDisplayManagement(builder.Configuration);
            
            // Register trading platform services
            builder.Services.AddSingleton<ITradingLogger, TradingLogger>();
            builder.Services.AddSingleton<IMonitorService, MonitorService>();
            builder.Services.AddSingleton<ITradingWindowManager, TradingWindowManager>();
            
            _host = builder.Build();
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
    }
}
