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
        private readonly MainViewModel _viewModel;
        private readonly Logger _logger;

        public MainWindow(MainViewModel viewModel, Logger logger)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _logger = logger;
            DataContext = _viewModel;

            // Привязка Title к ViewModel
            SetBinding(TitleProperty, new Binding("WindowTitle"));

            // Запуск анимации загрузки
            var loadingAnimation = TryFindResource("LoadingAnimation") as Storyboard;
            if (loadingAnimation != null && LoadingIndicator != null)
            {
                var rotateTransform = LoadingIndicator.RenderTransform as RotateTransform;
                if (rotateTransform == null)
                {
                    rotateTransform = new RotateTransform(0);
                    LoadingIndicator.RenderTransform = rotateTransform;
                }
                Storyboard.SetTarget(loadingAnimation, LoadingIndicator);
                loadingAnimation.Begin();
            }

            // Обработка изменения IsLoading для индикатора
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.IsLoading))
                {
                    Dispatcher.Invoke(() =>
                    {
                        LoadingIndicator.Visibility = (_viewModel != null && _viewModel.IsLoading) ? Visibility.Visible : Visibility.Collapsed;
                    });
                }
            };

            // Убеждаемся, что окно видимо и активно
            this.ShowActivated = true;
            this.Visibility = Visibility.Visible;

            _logger.Info("MainWindow initialized");
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
