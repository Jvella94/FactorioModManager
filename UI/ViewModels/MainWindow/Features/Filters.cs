using ReactiveUI;
using System;
using System.Linq;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        /// <summary>
        /// Extracts author name from "Author (count)" format
        /// </summary>
        private static string ExtractAuthorName(string authorFilter)
        {
            var parenIndex = authorFilter.IndexOf('(');
            return parenIndex > 0
                ? authorFilter[..parenIndex].Trim()
                : authorFilter.Trim();
        }
    }
}