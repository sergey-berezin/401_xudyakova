using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using ReactiveUI;
using System;
using System.IO;
using Viewer.Models;
using Viewer.ViewModels;

namespace Viewer.Views
{
    public partial class MainWindow : Window
    {
        public MainWindowViewModel viewModel;

        public MainWindow()
        {
            InitializeComponent();
            viewModel = new MainWindowViewModel();
            DataContext = viewModel;
        }

        private  void OnCanvasDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            if (sender is Control control &&
                control.DataContext is ItemModel dataContext)
                viewModel.ChangeCurrent(dataContext);
        }

        private async void OnButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            FileDialog();
        }

        private async void FileDialog()
        {
            // Get top level from the current control. Alternatively, you can use Window reference instead.
            var topLevel = TopLevel.GetTopLevel(this);

            // Start async operation to open the dialog.
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open File",
                AllowMultiple = true
            });

            if (files.Count > 0)
            {
                string[] filenames = new string[files.Count];
                for (int i = 0; i < files.Count; i++)
                    filenames[i] = files[i].Path.LocalPath.ToString();
                await viewModel.FileProcessing(filenames);
                DataContext = viewModel;
            }
        }
    }
}