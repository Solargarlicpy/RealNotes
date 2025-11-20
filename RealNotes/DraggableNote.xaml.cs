using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace RealNotes
{
    /// Interaction logic for DraggableNote.xaml
    /// Thumb-based dragging/resizing only (full-surface border drag removed).
    public partial class DraggableNote : UserControl
    {
        // cache parent canvas for DragThumb operations
        private Canvas? _parentCanvas;

        public event EventHandler? NoteClicked;
        public event EventHandler? NoteDeleted;

        public DraggableNote()
        {
            InitializeComponent();

            try
            {
                // Attach handlers only if the named controls exist in XAML
                if (OuterBorder != null)
                {
                    // Selection on left click (do not start a drag here)
                    OuterBorder.MouseLeftButtonDown += (s, e) =>
                    {
                        NoteClicked?.Invoke(this, EventArgs.Empty);
                        // intentionally do not capture mouse or start a full-surface drag
                    };
                    OuterBorder.MouseRightButtonUp += OuterBorder_MouseRightButtonUp;
                }

                if (ResizeThumb != null) // bottom-right resize thumb
                {
                    ResizeThumb.DragDelta += ResizeThumb_DragDelta;
                    ResizeThumb.PreviewMouseLeftButtonDown += (s, e) =>
                    {
                        if (s is Thumb t) t.Focus(); // keyboard resizing support
                        // do not mark handled so DragDelta fires
                    };
                }

                if (InnerTextBox != null)
                {
                    InnerTextBox.PreviewMouseLeftButtonDown += (s, e) =>
                    {
                        FocusInnerTextBox();
                        NoteClicked?.Invoke(this, EventArgs.Empty);
                        // allow normal text input to proceed
                    };
                }

                // attach drag handler for the DragThumb defined in XAML
                if (DragThumb != null)
                {
                    DragThumb.DragDelta += DragThumb_DragDelta;
                    DragThumb.PreviewMouseLeftButtonDown += (s, e) =>
                    {
                        NoteClicked?.Invoke(this, EventArgs.Empty);
                        if (s is Thumb t) t.Focus();
                        e.Handled = false;
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DraggableNote ctor error: {ex}");
            }
        }

        public Brush NoteBackground
        {
            get => InnerTextBox?.Background ?? Brushes.LightYellow;
            set
            {
                if (InnerTextBox != null) InnerTextBox.Background = value;
            }
        }

        public string NoteText
        {
            get => InnerTextBox?.Text ?? string.Empty;
            set
            {
                if (InnerTextBox != null) InnerTextBox.Text = value;
            }
        }

        public bool IsSelected
        {
            get => OuterBorder != null && OuterBorder.BorderBrush == Brushes.Red;
            set
            {
                if (OuterBorder != null) OuterBorder.BorderBrush = value ? Brushes.Red : Brushes.Gray;
            }
        }

        public void FocusInnerTextBox() => InnerTextBox?.Focus();

        // Keep canvas lookup for DragThumb behavior
        private Canvas? GetParentCanvas()
        {
            if (_parentCanvas != null) return _parentCanvas;

            DependencyObject? p = this;
            while (p != null)
            {
                if (p is Canvas c)
                {
                    _parentCanvas = c;
                    return c;
                }
                p = VisualTreeHelper.GetParent(p);
            }
            return null;
        }

        private void OuterBorder_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var result = MessageBox.Show("Delete this note?", "Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                    NoteDeleted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OuterBorder_MouseRightButtonUp error: {ex}");
            }
        }

        private void ResizeThumb_DragDelta(object? sender, DragDeltaEventArgs e)
        {
            try
            {
                if (double.IsNaN(Width)) Width = this.ActualWidth;
                if (double.IsNaN(Height)) Height = this.ActualHeight;

                double newW = Math.Max(MinWidth, Width + e.HorizontalChange);
                double newH = Math.Max(MinHeight, Height + e.VerticalChange);

                Width = newW;
                Height = newH;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ResizeThumb_DragDelta error: {ex}");
            }
        }

        private void DragThumb_DragDelta(object? sender, DragDeltaEventArgs e)
        {
            try
            {
                var canvas = GetParentCanvas();
                if (canvas == null) return;

                double left = Canvas.GetLeft(this);
                double top = Canvas.GetTop(this);
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;

                Canvas.SetLeft(this, Math.Max(0, left + e.HorizontalChange));
                Canvas.SetTop(this, Math.Max(0, top + e.VerticalChange));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DragThumb_DragDelta error: {ex}");
            }
        }

        // helper to find an ancestor of a specific type in the visual tree (still useful)
        private static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
        {
            DependencyObject? current = start;
            while (current != null)
            {
                if (current is T t) return t;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        // XAML-attached forwarding handlers (kept for compatibility if XAML references them)
        private void ResizeThumb_DragDelta_1(object sender, DragDeltaEventArgs e)
        {
            ResizeThumb_DragDelta(sender, e);
        }

        private void DragThumb_DragDelta_1(object sender, DragDeltaEventArgs e)
        {

        }
    }
}
