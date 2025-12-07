using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FactorioModManager.ViewModels;

namespace FactorioModManager.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void CancelRename(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ModGroupViewModel group)
            {
                group.IsEditing = false;
            }
        }

        private void DataGrid_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dataGrid && DataContext is MainWindowViewModel viewModel)
            {
                viewModel.SelectedMods.Clear();

                if (dataGrid.SelectedItems != null)
                {
                    foreach (var item in dataGrid.SelectedItems.OfType<ModViewModel>())
                    {
                        viewModel.SelectedMods.Add(item);
                    }
                }
            }
        }

        private void AuthorBox_OnGotFocus(object? sender, GotFocusEventArgs e)
        {
            if (sender is AutoCompleteBox autoCompleteBox &&
                string.IsNullOrEmpty(autoCompleteBox.Text))
            {
                // Open dropdown when focused and empty
                autoCompleteBox.IsDropDownOpen = true;
            }
        }
    }
}
