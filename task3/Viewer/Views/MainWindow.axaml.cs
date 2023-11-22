using Avalonia.Controls;
using Task3.Models;
using Task3.ViewModels;

namespace Task3.Views
{
    public partial class MainWindow : Window
    {
        MainWindowViewModel? viewModel = null;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnCanvasDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            if (sender is Control control &&
                control.DataContext is ItemModel dataContext &&
                DataContext is MainWindowViewModel viewModel &&
                viewModel != null)
                viewModel.ChangeCurrent(dataContext);
        }
    }
}