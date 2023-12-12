using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Task3.ViewModels;

namespace Task3.Views
{
    internal class FSServices : IFSServices
    {
        private Window win = new Window() { IsVisible = false };

        public async Task<string[]?> Get_Load_Filenames()
        {
            // Get top level from the current control. Alternatively, you can use Window reference instead.
            var topLevel = TopLevel.GetTopLevel(win);

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
                return filenames;
            }
            else
                return null;
        }
    }
}
