using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using ReactiveUI;
using System;
using System.IO;
using System.Threading.Tasks;
using Viewer.Models;
using Viewer.ViewModels;

namespace Viewer.Views
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