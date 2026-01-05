using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace FactorioModManager.Views.Controls
{
    public partial class RenameEditor : UserControl
    {
        public event EventHandler? SaveClicked;

        public event EventHandler? CancelClicked;

        private Border? _rootBorder;
        private TextBox? _editBox;
        private INotifyPropertyChanged? _notifier;
        private bool _pendingSelect;

        public RenameEditor()
        {
            AvaloniaXamlLoader.Load(this);
            AttachedToVisualTree += OnAttachedToVisualTree;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _rootBorder = this.FindControl<Border>("RootBorder");
            _editBox = this.FindControl<TextBox>("EditBox");

            var save = this.FindControl<Button>("SaveBtn");
            var cancel = this.FindControl<Button>("CancelBtn");

            if (save != null) save.Click += Save_Click;
            if (cancel != null) cancel.Click += Cancel_Click;

            if (_editBox != null)
            {
                _editBox.KeyDown += EditBox_KeyDown;

                // Only focus/select when appropriate (IsRenaming or pending request)
                _editBox.AttachedToVisualTree += (s, ev) =>
                {
                    try
                    {
                        // Use the sender as the TextBox to avoid dereferencing the nullable field
                        if (s is not TextBox tb)
                            return;

                        var shouldSelect = _pendingSelect;

                        // If DataContext indicates renaming, honor it
                        if (!shouldSelect && tb.DataContext != null)
                        {
                            var propInfo = tb.DataContext.GetType().GetProperty("IsRenaming");
                            if (propInfo != null && propInfo.GetValue(tb.DataContext) is bool isRenaming && isRenaming)
                                shouldSelect = true;
                        }

                        if (shouldSelect)
                        {
                            _pendingSelect = false;
                            // Post a small delay to ensure control is rendered and Text is set before selecting
                            Dispatcher.UIThread.Post(async () =>
                            {
                                try
                                {
                                    await Task.Delay(10);
                                    // Marshal back to UI thread to perform focus/select
                                    Dispatcher.UIThread.Post(() =>
                                    {
                                        try
                                        {
                                            if (TopLevel.GetTopLevel(tb) != null)
                                            {
                                                tb.Focus();
                                                tb.SelectAll();
                                            }
                                        }
                                        catch { }
                                    });
                                }
                                catch { }
                            }, DispatcherPriority.Background);
                        }
                    }
                    catch { }
                };

                // If we had a pending request and we're already attached, apply it now (delayed)
                if (_pendingSelect && TopLevel.GetTopLevel(_editBox) != null)
                {
                    Dispatcher.UIThread.Post(async () =>
                    {
                        try
                        {
                            await Task.Delay(10);
                            Dispatcher.UIThread.Post(() =>
                            {
                                try
                                {
                                    if (_editBox != null && TopLevel.GetTopLevel(_editBox) != null)
                                    {
                                        _pendingSelect = false;
                                        _editBox.Focus();
                                        _editBox.SelectAll();
                                    }
                                }
                                catch { }
                            });
                        }
                        catch { }
                    }, DispatcherPriority.Background);
                }
            }

            // initialize from current DataContext
            UpdateFromDataContext();
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            // Unsubscribe previous
            if (_notifier != null)
            {
                _notifier.PropertyChanged -= Notifier_PropertyChanged;
                _notifier = null;
            }

            // Subscribe new if it supports INotifyPropertyChanged
            if (this.DataContext is INotifyPropertyChanged npc)
            {
                _notifier = npc;
                _notifier.PropertyChanged += Notifier_PropertyChanged;
            }

            UpdateFromDataContext();
        }

        private void Notifier_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e == null) return;

            // React to IsRenaming and EditedName changes
            if (string.Equals(e.PropertyName, "IsRenaming", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var isRenamingProp = this.DataContext?.GetType().GetProperty("IsRenaming");
                    if (isRenamingProp != null && isRenamingProp.GetValue(this.DataContext) is bool isRenaming)
                    {
                        // Update UI on UI thread
                        Dispatcher.UIThread.Post(() =>
                        {
                            SetHighlight(isRenaming);

                            if (isRenaming)
                            {
                                // If edit box is present and attached, schedule delayed focus/select; otherwise mark pending
                                if (_editBox != null && TopLevel.GetTopLevel(_editBox) != null)
                                {
                                    Dispatcher.UIThread.Post(async () =>
                                    {
                                        try
                                        {
                                            await Task.Delay(10);
                                            Dispatcher.UIThread.Post(() =>
                                            {
                                                try
                                                {
                                                    if (_editBox != null && TopLevel.GetTopLevel(_editBox) != null)
                                                    {
                                                        _editBox.Focus();
                                                        _editBox.SelectAll();
                                                    }
                                                }
                                                catch { }
                                            });
                                        }
                                        catch { }
                                    }, DispatcherPriority.Background);
                                }
                                else
                                {
                                    _pendingSelect = true;
                                }
                            }
                        });
                    }
                }
                catch { }
            }
            else if (string.Equals(e.PropertyName, "EditedName", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var editedProp = this.DataContext?.GetType().GetProperty("EditedName");
                    if (editedProp != null && editedProp.GetValue(this.DataContext) is string val)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (_editBox != null && _editBox.Text != val)
                                _editBox.Text = val;
                        });
                    }
                }
                catch { }
            }
        }

        private void UpdateFromDataContext()
        {
            try
            {
                _editBox ??= this.FindControl<TextBox>("EditBox");
                _rootBorder ??= this.FindControl<Border>("RootBorder");

                if (this.DataContext != null)
                {
                    var editedProp = this.DataContext.GetType().GetProperty("EditedName");
                    if (editedProp != null)
                    {
                        if (_editBox != null && editedProp.GetValue(this.DataContext) is string val)
                            _editBox.Text = val;
                    }

                    var isRenamingProp = this.DataContext.GetType().GetProperty("IsRenaming");
                    if (isRenamingProp != null && isRenamingProp.GetValue(this.DataContext) is bool b && b)
                    {
                        SetHighlight(true);

                        // Ensure focus when renaming starts
                        if (_editBox != null)
                        {
                            if (TopLevel.GetTopLevel(_editBox) != null)
                            {
                                Dispatcher.UIThread.Post(async () =>
                                {
                                    try
                                    {
                                        await Task.Delay(10);
                                        Dispatcher.UIThread.Post(() =>
                                        {
                                            try
                                            {
                                                if (_editBox != null && TopLevel.GetTopLevel(_editBox) != null)
                                                {
                                                    _editBox.Focus();
                                                    _editBox.SelectAll();
                                                }
                                            }
                                            catch { }
                                        });
                                    }
                                    catch { }
                                }, DispatcherPriority.Background);
                            }
                            else
                            {
                                _pendingSelect = true;
                            }
                        }
                    }
                    else
                    {
                        SetHighlight(false);
                    }
                }
            }
            catch { }
        }

        private void EditBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // push text back to DataContext EditedName if present
                TrySyncTextToDataContext();
                SaveClicked?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelClicked?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        }

        private void TrySyncTextToDataContext()
        {
            try
            {
                if (this.DataContext != null && _editBox != null)
                {
                    var prop = this.DataContext.GetType().GetProperty("EditedName");
                    if (prop != null && prop.CanWrite)
                    {
                        prop.SetValue(this.DataContext, _editBox.Text);
                    }
                }
            }
            catch { }
        }

        private void Save_Click(object? sender, RoutedEventArgs e)
        {
            TrySyncTextToDataContext();
            SaveClicked?.Invoke(this, EventArgs.Empty);
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            CancelClicked?.Invoke(this, EventArgs.Empty);
        }

        public void SetHighlight(bool highlight)
        {
            _rootBorder ??= this.FindControl<Border>("RootBorder");
            if (_rootBorder == null) return;
            _rootBorder.BorderBrush = highlight ? Brushes.LightGreen : Brushes.Gray;
            _rootBorder.BorderThickness = highlight ? new Thickness(2) : new Thickness(1);
        }
    }
}