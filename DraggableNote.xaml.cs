using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RealNotes
{
    /// <summary>
    /// Interaction logic for DraggableNote.xaml
    /// Safe, border-based dragging only (no Thumb required).
    /// </summary>
    public partial class DraggableNote : UserControl
    {
        // When dragging many visuals, bitmap-caching the control during the drag
        // reduces the cost of repeatedly compositing complex visuals.
        // We'll enable CacheMode while dragging and disable it on drop.
        private bool _isDragging;
        private Point _dragStart;
        private double _origLeft;
        private double _origTop;
        private int _previousZIndex;
        // cache parent canvas during drag to avoid repeated visual tree walks
        private Canvas? _parentCanvas;
        // use a render transform while dragging to avoid layout churn
        private TranslateTransform? _dragTransform;
        // throttling: use a DispatcherTimer to apply the latest move at a fixed interval
        private DispatcherTimer? _moveTimer;
        private double _pendingDx;
        private double _pendingDy;
        private bool _hasPendingMove;

        public event EventHandler? NoteClicked;
        public event EventHandler? NoteDeleted;

        public DraggableNote()
        {
            InitializeComponent();

            try
            {
                // Attach handlers only if the named controls exist in XAML.
                if (OuterBorder != null)
                {
                    // Dragging is disabled: only support selection and right-click delete.
                    OuterBorder.MouseLeftButtonDown += (s, e) =>
                    {
                        NoteClicked?.Invoke(this, EventArgs.Empty);
                        // Do not start any drag or capture here.
                    };
                    OuterBorder.MouseRightButtonUp += OuterBorder_MouseRightButtonUp;
                }

                if (ResizeThumb != null)
                {
                    ResizeThumb.DragDelta += ResizeThumb_DragDelta;
                    ResizeThumb.PreviewMouseLeftButtonDown += (s, e) =>
                    {
                        if (s is Thumb t) t.Focus();
                        e.Handled = true; // stop parent ScrollViewer panning while resizing
                    };
                }

                if (InnerTextBox != null)
                {
                    InnerTextBox.PreviewMouseLeftButtonDown += (s, e) =>
                    {
                        // make sure clicks inside the text box give it focus so typing works
                        FocusInnerTextBox();
                        NoteClicked?.Invoke(this, EventArgs.Empty);
                        // do not mark handled here so normal text box input still occurs
                    };
                }

                // attach drag handler for the DragThumb defined in XAML
                if (DragThumb != null)
                {
                    DragThumb.DragDelta += DragThumb_DragDelta;
                    DragThumb.PreviewMouseLeftButtonDown += (s, e) =>
                    {
                        // forward selection to container
                        NoteClicked?.Invoke(this, EventArgs.Empty);
                        // ensure thumb gets focus
                        if (s is Thumb t) t.Focus();
                        e.Handled = false;
                    };
                }

                // safety: ensure we clean up if capture is lost or control unloaded
                this.LostMouseCapture += OnLostMouseCapture;
                this.Unloaded += OnUnloaded;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DraggableNote ctor error: {ex}");
            }
        }

        private void OnLostMouseCapture(object? sender, MouseEventArgs e)
        {
            try { CleanupDrag(); } catch { }
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            try { CleanupDrag(); } catch { }
        }

        private void CleanupDrag()
        {
            try
            {
                // stop timer
                if (_moveTimer != null)
                {
                    _moveTimer.Stop();
                    _moveTimer.Tick -= MoveTimer_Tick;
                    // keep instance for reuse, but ensure no pending tick
                }

                // release capture if still held
                if (IsMouseCaptured)
                    ReleaseMouseCapture();

                // clear transform and cached state
                try { this.RenderTransform = null; } catch { }
                _dragTransform = null;
                _hasPendingMove = false;
                _pendingDx =0; _pendingDy =0;

                // clear any caching and restore text box state
                try { this.CacheMode = null; } catch { }
                if (InnerTextBox != null) InnerTextBox.IsReadOnly = false;

                _isDragging = false;
                _parentCanvas = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CleanupDrag error: {ex}");
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

        private Canvas? GetParentCanvas()
        {
            // return cached value if available
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

        private void BringToFront()
        {
            try
            {
                var canvas = GetParentCanvas();
                if (canvas == null) return;

                _previousZIndex = Panel.GetZIndex(this);
                Panel.SetZIndex(this,1000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BringToFront error: {ex}");
            }
        }

        private void RestoreZIndex()
        {
            try
            {
                var canvas = GetParentCanvas();
                if (canvas == null) return;

                Panel.SetZIndex(this, _previousZIndex);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RestoreZIndex error: {ex}");
            }
        }

        // Full-surface dragging (click+drag on OuterBorder)
        private void OuterBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // Don't start drag when the click targets editing or thumbs in the template
                var orig = e.OriginalSource as DependencyObject;
                if (orig != null)
                {
                    if (FindAncestor<TextBox>(orig) != null || FindAncestor<Thumb>(orig) != null)
                    {
                        NoteClicked?.Invoke(this, EventArgs.Empty);
                        return;
                    }
                }

                var canvas = GetParentCanvas();
                if (canvas == null) return;

                _isDragging = true;
                // capture starting state
                _dragStart = e.GetPosition(canvas);

                _origLeft = Canvas.GetLeft(this);
                _origTop = Canvas.GetTop(this);
                if (double.IsNaN(_origLeft)) _origLeft =0;
                if (double.IsNaN(_origTop)) _origTop =0;

                NoteClicked?.Invoke(this, EventArgs.Empty);
                BringToFront();

                // use a lightweight render transform for the drag so layout is not invalidated on every move
                _dragTransform = new TranslateTransform(0,0);
                this.RenderTransform = _dragTransform;

                // enable bitmap cache while dragging to reduce render cost for complex visuals
                try { this.CacheMode = new BitmapCache(); } catch { }

                // make textbox read-only while dragging to avoid expensive caret/selection updates
                if (InnerTextBox != null) InnerTextBox.IsReadOnly = true;

                // prepare throttling timer (approx60Hz)
                if (_moveTimer == null)
                {
                    _moveTimer = new DispatcherTimer(DispatcherPriority.Render)
                    {
                        Interval = TimeSpan.FromMilliseconds(16)
                    };
                    _moveTimer.Tick += MoveTimer_Tick;
                }
                _hasPendingMove = false;
                _moveTimer.Start();

                CaptureMouse();
                e.Handled = true; // prevent parent ScrollViewer panning
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OuterBorder_MouseLeftButtonDown error: {ex}");
            }
        }

        private void OuterBorder_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (!_isDragging) return;

                // use cached parent canvas (set during GetParentCanvas call above)
                // compute latest delta and mark pending; timer will apply it at throttle rate
                var canvas = _parentCanvas ?? GetParentCanvas();
                if (canvas == null) return;

                var pos = e.GetPosition(canvas);
                var dx = pos.X - _dragStart.X;
                var dy = pos.Y - _dragStart.Y;

                // update transform only (very cheap) — final Canvas position applied on drop
                if (_dragTransform != null)
                {
                    _dragTransform.X = dx;
                    _dragTransform.Y = dy;
                }
                _pendingDx = pos.X - _dragStart.X;
                _pendingDy = pos.Y - _dragStart.Y;
                _hasPendingMove = true;

                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OuterBorder_MouseMove error: {ex}");
            }
        }

        private void MoveTimer_Tick(object? sender, EventArgs e)
        {
            if (!_hasPendingMove || _dragTransform == null) return;
            // apply latest pending values
            _dragTransform.X = _pendingDx;
            _dragTransform.Y = _pendingDy;
            _hasPendingMove = false;
        }

        private void OuterBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (!_isDragging) return;

                _isDragging = false;
                ReleaseMouseCapture();
                RestoreZIndex();

                // finalize position: apply the accumulated transform to Canvas coordinates
                // stop throttling and apply final pending move if any
                if (_moveTimer != null)
                {
                    _moveTimer.Stop();
                }

                if (_dragTransform != null)
                {
                    // if there's a pending move that wasn't applied by timer yet, apply it now
                    if (_hasPendingMove)
                    {
                        _dragTransform.X = _pendingDx;
                        _dragTransform.Y = _pendingDy;
                        _hasPendingMove = false;
                    }

                    double finalLeft = Math.Max(0, _origLeft + _dragTransform.X);
                    double finalTop = Math.Max(0, _origTop + _dragTransform.Y);
                    Canvas.SetLeft(this, finalLeft);
                    Canvas.SetTop(this, finalTop);

                    // clear transform
                    this.RenderTransform = null;
                    _dragTransform = null;
                }

                // disable bitmap cache after drag so text rendering and bindings are normal
                try { this.CacheMode = null; } catch { }

                if (InnerTextBox != null) InnerTextBox.IsReadOnly = false;

                // clear cached canvas after drag completes
                _parentCanvas = null;
                e.Handled = true;

                // final cleanup to ensure no leftover state
                CleanupDrag();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OuterBorder_MouseLeftButtonUp error: {ex}");
            }
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
                if (double.IsNaN(Width)) Width = MinWidth;
                if (double.IsNaN(Height)) Height = MinHeight;

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

        // helper to find an ancestor of a specific type in the visual tree
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
    }
}
