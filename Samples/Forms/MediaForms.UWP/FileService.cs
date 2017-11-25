using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using MediaForms.Interface;
using MediaForms.UWP;

[assembly: Xamarin.Forms.Dependency(typeof(FileService))]
namespace MediaForms.UWP
{
    public class FileService : IFileService
    {
        public string GetLocalFilePath()
        {
            var localFolder = ApplicationData.Current.LocalFolder;
            return localFolder?.Path;
        }
    }
}
