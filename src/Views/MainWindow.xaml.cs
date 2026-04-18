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
            
            try
            {
                _logger.Info("Calling InitializeComponent()...");
                InitializeComponent();
                _logger.Info("InitializeComponent completed successfully");
                
                _viewModel = viewModel;
                _logger.Info("Setting DataContext to MainViewModel...");
                DataContext = _viewModel;
                _logger.Info("DataContext set successfully");

                // Привязка Title к ViewModel
                _logger.Info("Setting up Title binding...");
                SetBinding(TitleProperty, new Binding("WindowTitle"));
                _logger.Info("Title binding established. Current Title: {0}", Title);

                // Запуск анимации загрузки
                _logger.Info("Looking for LoadingAnimation resource...");
                var loadingAnimation = TryFindResource("LoadingAnimation") as Storyboard;
                if (loadingAnimation != null && LoadingIndicator != null)
                {
                    _logger.Info("LoadingAnimation found, setting up rotation...");
                    var rotateTransform = LoadingIndicator.RenderTransform as RotateTransform;
                    if (rotateTransform == null)
                    {
                        _logger.Info("Creating new RotateTransform...");
                        rotateTransform = new RotateTransform(0);
                        LoadingIndicator.RenderTransform = rotateTransform;
                    }
                    Storyboard.SetTarget(loadingAnimation, LoadingIndicator);
                    _logger.Info("Starting animation...");
                    loadingAnimation.Begin();
                    _logger.Info("Loading animation started successfully");
                }
                else
                {
                    _logger.Warning("LoadingAnimation resource not found or LoadingIndicator is null. Animation: {0}, Indicator: {1}", 
                        loadingAnimation == null ? "null" : "found", 
                        LoadingIndicator == null ? "null" : "found");
                }

                // Убеждаемся, что окно видимо и активно
                _logger.Info("Setting ShowActivated = true and Visibility = Visible");
                this.ShowActivated = true;
                this.Visibility = Visibility.Visible;
                _logger.Info("MainWindow initialized successfully. IsVisible: {0}, IsActive: {1}", IsVisible, IsActive);
                _logger.Info("=== MAINWINDOW CONSTRUCTOR COMPLETE ===");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during MainWindow initialization");
                _logger.Error("Exception type: {0}", ex.GetType().FullName);
                _logger.Error("Stack trace: {0}", ex.StackTrace);
                if (ex.InnerException != null)
                {
                    _logger.Error("Inner exception: {0}", ex.InnerException);
                }
                throw;
            }
        }

        /// <summary>
        /// Развернуть все сессии.
        /// </summary>
        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var vm in _viewModel.Sessions)
                vm.IsExpanded = true;
        }

        /// <summary>
        /// Свернуть все сессии.
        /// </summary>
        private void CollapseAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var vm in _viewModel.Sessions)
                vm.IsExpanded = false;
        }

        /// <summary>
        /// Развернуть выбранную сессию.
        /// </summary>
        private void ExpandSession_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedSession != null)
                _viewModel.SelectedSession.IsExpanded = true;
        }

        /// <summary>
        /// Свернуть выбранную сессию.
        /// </summary>
        private void CollapseSession_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedSession != null)
                _viewModel.SelectedSession.IsExpanded = false;
        }
    }
}
