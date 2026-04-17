using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MhTsManager.Services;
using MhTsManager.ViewModels;

namespace MhTsManager.Views;

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

        // Обработка изменения IsLoading для индикатора
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsLoading))
            {
                Dispatcher.Invoke(() =>
                {
                    LoadingIndicator.Visibility = _viewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;
                });
            }
        };

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

/// <summary>
/// Конвертер текста статуса в цвет индикатора.
/// </summary>
namespace Local
{
    public class StringToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                "Активна" or "Active" => new SolidColorBrush(Color.FromRgb(16, 185, 129)),     // 🟢
                "Бездействие" or "Idle" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),   // 🟡
                "Отключена" or "Disconnected" => new SolidColorBrush(Color.FromRgb(107, 114, 128)), // ⚪
                "Заблокирована" or "Locked" => new SolidColorBrush(Color.FromRgb(99, 102, 241)),   // 🔵
                "Недоступна" or "Unavailable" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),   // 🔴
                _ => new SolidColorBrush(Color.FromRgb(156, 163, 175))
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
