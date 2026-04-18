using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MhTsManager.Services;
using MhTsManager.ViewModels;

namespace MhTsManager.Views
{
    /// <summary>
    /// Главное окно приложения.
    /// </summary>
    public partial class MainWindow : Window
    {
        // Инициализируется в конструкторе, оператор ! подавляет ложное предупреждение CS8602
        private readonly MainViewModel _viewModel = null!;
        // Инициализируется в конструкторе, оператор ! подавляет ложное предупреждение CS8602
        private readonly Logger _logger = null!;

        public MainWindow(MainViewModel viewModel, Logger logger)
        {
            _logger = logger;
            _logger.Info("=== MAINWINDOW CONSTRUCTOR BEGIN ===");
            _logger.Info("Constructor called with ViewModel: {0}, Logger: {1}", 
                viewModel?.GetType().Name ?? "null", 
                logger?.GetType().Name ?? "null");

            try
            {
                _logger.Info("Step 1: Calling InitializeComponent()...");
                _logger.Info("  - Current thread: {0}", System.Threading.Thread.CurrentThread.ManagedThreadId);
                _logger.Info("  - Thread is STA: {0}", System.Threading.Thread.CurrentThread.GetApartmentState() == System.Threading.ApartmentState.STA);
                
                InitializeComponent();
                _logger.Info("Step 1 COMPLETE: InitializeComponent() completed successfully");
                _logger.Info("  - Window Handle (after InitializeComponent): {0}", 
                    new System.Windows.Interop.WindowInteropHelper(this).Handle);

                _viewModel = viewModel!;
                _logger.Info("Step 2: Setting DataContext to MainViewModel...");
                DataContext = _viewModel;
                _logger.Info("Step 2 COMPLETE: DataContext set successfully. DataContext type: {0}", DataContext?.GetType().Name ?? "null");

                // Привязка Title к ViewModel
                _logger.Info("Step 3: Setting up Title binding...");
                SetBinding(TitleProperty, new Binding("WindowTitle"));
                _logger.Info("Step 3 COMPLETE: Title binding established. Current Title: {0}", Title);

                // Проверка ресурсов после InitializeComponent
                _logger.Info("Step 4: Checking resources...");
                _logger.Info("  - Resources count: {0}", Resources.Count);
                _logger.Info("  - MergedDictionaries count: {0}", 
                    Application.Current?.Resources.MergedDictionaries.Count ?? -1);
                
                foreach (var key in Resources.Keys)
                {
                    _logger.Info("  - Resource key: {0}, Type: {1}", 
                        key?.ToString() ?? "(null)", 
                        Resources[key]?.GetType().Name ?? "(null)");
                }

                // Запуск анимации загрузки
// Removed loadingAnimation usage
// Removed loadingAnimation usage
// Removed loadingAnimation usage
                _logger.Info("  - LoadingIndicator control: {0}", LoadingIndicator != null ? "FOUND" : "NULL");
                
// Removed loadingAnimation usage
                {
                    _logger.Info("Step 5a: Creating and starting rotation animation programmatically...\n                    var rotateTransform = LoadingIndicator.RenderTransform as RotateTransform;\n                    if (rotateTransform == null)\n                    {\n                        rotateTransform = new RotateTransform(0);\n                        LoadingIndicator.RenderTransform = rotateTransform;\n                    }\n\n                    // Create animation programmatically to avoid ReadOnly exception\n                    var rotationAnimation = new DoubleAnimation\n                    {\n                        From = 0,\n                        To = 360,\n                        Duration = TimeSpan.FromSeconds(1.5),\n                        RepeatBehavior = RepeatBehavior.Forever,\n                        EasingFunction = new LinearEasing()\n                    };\n\n                    rotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotationAnimation);\n                    _logger.Info(\ Step 5a COMPLETE: Programmatic loading animation started successfully\)");
                }
                else
                {
// Removed loadingAnimation usage
// Removed loadingAnimation usage
                        LoadingIndicator == null ? "null" : "found");
                }

                // Убеждаемся, что окно видимо и активно
                _logger.Info("Step 6: Setting ShowActivated = true and Visibility = Visible");
                this.ShowActivated = true;
                this.Visibility = Visibility.Visible;
                
                _logger.Info("Step 6 COMPLETE: Properties set");
                _logger.Info("  - ShowActivated: {0}", this.ShowActivated);
                _logger.Info("  - Visibility: {0}", this.Visibility);
                _logger.Info("  - IsVisible: {0}", this.IsVisible);
                _logger.Info("  - IsActive: {0}", this.IsActive);
                _logger.Info("  - Width: {0}, Height: {1}", this.Width, this.Height);
                _logger.Info("  - MinWidth: {0}, MinHeight: {1}", this.MinWidth, this.MinHeight);
                _logger.Info("  - WindowStyle: {0}", this.WindowStyle);
                _logger.Info("  - AllowsTransparency: {0}", this.AllowsTransparency);
                _logger.Info("  - Background: {0}", this.Background?.ToString() ?? "null");
                
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                _logger.Info("  - Window Handle: {0}", helper.Handle);
                
                _logger.Info("=== MAINWINDOW CONSTRUCTOR COMPLETE ===");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "❌ ERROR during MainWindow initialization");
                _logger.Error("Exception type: {0}", ex.GetType().FullName);
                _logger.Error("Message: {0}", ex.Message);
                _logger.Error("Stack trace: {0}", ex.StackTrace);
                if (ex.InnerException != null)
                {
                    _logger.Error("Inner exception: {0}", ex.InnerException);
                    _logger.Error("Inner exception stack trace: {0}", ex.InnerException.StackTrace);
                }
                
                // Логируем все детали ресурсов для диагностики
                try
                {
                    _logger.Info("Diagnostic info at failure point:");
                    _logger.Info("  - Application.Current: {0}", Application.Current != null ? "YES" : "NO");
                    if (Application.Current != null)
                    {
                        _logger.Info("  - Application.Resources count: {0}", Application.Current.Resources.Count);
                        _logger.Info("  - Application.MergedDictionaries count: {0}", Application.Current.Resources.MergedDictionaries.Count);
                    }
                }
                catch (Exception diagEx)
                {
                    _logger.Error(diagEx, "Failed to collect diagnostic info");
                }
                
                throw;
            }
        }

        /// <summary>
        /// Развернуть все сессии.
        /// </summary>
        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            _logger.Info("ExpandAll_Click triggered");
            foreach (var vm in _viewModel.Sessions)
                vm.IsExpanded = true;
        }

        /// <summary>
        /// Свернуть все сессии.
        /// </summary>
        private void CollapseAll_Click(object sender, RoutedEventArgs e)
        {
            _logger.Info("CollapseAll_Click triggered");
            foreach (var vm in _viewModel.Sessions)
                vm.IsExpanded = false;
        }

        /// <summary>
        /// Развернуть выбранную сессию.
        /// </summary>
        private void ExpandSession_Click(object sender, RoutedEventArgs e)
        {
            _logger.Info("ExpandSession_Click triggered");
            if (_viewModel.SelectedSession != null)
                _viewModel.SelectedSession.IsExpanded = true;
        }

        /// <summary>
        /// Свернуть выбранную сессию.
        /// </summary>
        private void CollapseSession_Click(object sender, RoutedEventArgs e)
        {
            _logger.Info("CollapseSession_Click triggered");
            if (_viewModel.SelectedSession != null)
                _viewModel.SelectedSession.IsExpanded = false;
        }
        
        /// <summary>
        /// Обработчик события Loaded для дополнительной диагностики
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _logger.Info("=== WINDOW LOADED EVENT ===");
            _logger.Info("  - IsVisible: {0}", this.IsVisible);
            _logger.Info("  - IsActive: {0}", this.IsActive);
            _logger.Info("  - ActualWidth: {0}, ActualHeight: {1}", this.ActualWidth, this.ActualHeight);
            _logger.Info("  - RenderSize: {0}x{1}", this.RenderSize.Width, this.RenderSize.Height);
            
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            _logger.Info("  - Handle: {0}", helper.Handle);
            
            // Проверяем, есть ли дочерние элементы
            if (this.Content is Grid grid)
            {
                _logger.Info("  - Content is Grid with {0} children", grid.Children.Count);
            }
            else
            {
                _logger.Info("  - Content type: {0}", this.Content?.GetType().Name ?? "null");
            }
        }
    }
}

