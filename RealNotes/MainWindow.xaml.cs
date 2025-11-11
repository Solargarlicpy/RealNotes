using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RealNotes; // Change made here

namespace RealNotes
{
    public partial class MainWindow : Window
    {
        // now track selected wrapper control
        private DraggableNote? _selectedNote;

        public MainWindow()
        {
            InitializeComponent();

            // ensure default preview is correct
            ColorPicker.SelectedIndex = 0;
            UpdateColorPickerPreview();
        }

        private void ColorPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorPicker.SelectedItem is ComboBoxItem selectedItem &&
                selectedItem.Content is string colorName)
            {
                var brush = GetBrushForName(colorName);

                // Use the ComboBox itself as the preview: set its Background and readable Foreground.
                ColorPicker.Background = brush;
                if (brush is SolidColorBrush solid)
                {
                    var c = solid.Color;
                    double luminance = (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
                    ColorPicker.Foreground = luminance < 0.5 ? Brushes.White : Brushes.Black;
                }
                else
                {
                    ColorPicker.Foreground = Brushes.Black;
                }

                // apply color to selected note only if one is selected
                if (_selectedNote != null)
                    _selectedNote.NoteBackground = brush;
            }
        }

        private Brush GetBrushForName(string colorName) =>
            colorName switch
            {
                "Yellow" => Brushes.Yellow,
                "Green" => Brushes.LightGreen,
                "Blue" => Brushes.LightBlue,
                "Pink" => Brushes.LightPink,
                "White" => Brushes.White,
                "Chartreuse" => Brushes.Chartreuse,
                "Violet" => Brushes.Violet,
                "Orange" => Brushes.Orange,
                _ => Brushes.LightGray
            };

        private void UpdateColorPickerPreview()
        {
            if (ColorPicker.SelectedItem is ComboBoxItem item && item.Content is string name)
            {
                var brush = GetBrushForName(name);
                ColorPicker.Background = brush;
                if (brush is SolidColorBrush solid)
                {
                    var c = solid.Color;
                    double luminance = (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
                    ColorPicker.Foreground = luminance < 0.5 ? Brushes.White : Brushes.Black;
                }
            }
        }

        private void AddNote_Click(object sender, RoutedEventArgs e)
        {
            var initialBrush = (ColorPicker.SelectedItem is ComboBoxItem sel && sel.Content is string n) ? GetBrushForName(n) : Brushes.LightYellow;

            var note = new DraggableNote
            {
                Width = 150,
                Height = 150,
                NoteText = "New Note",
                NoteBackground = initialBrush
            };

            // place in canvas - staggered placement
            double left = 10 + (NotesCanvas.Children.Count % 10) * 20;
            double top = 10 + (NotesCanvas.Children.Count / 10) * 20;
            Canvas.SetLeft(note, left);
            Canvas.SetTop(note, top);

            note.NoteClicked += Note_NoteClicked;
            note.NoteDeleted += Note_NoteDeleted;

            NotesCanvas.Children.Add(note);

            // select the new note
            SelectNote(note);
            note.FocusInnerTextBox();
        }

        private void Note_NoteClicked(object? sender, EventArgs e)
        {
            if (sender is DraggableNote dn)
            {
                SelectNote(dn);
            }
        }

        private void Note_NoteDeleted(object? sender, EventArgs e)
        {
            if (sender is DraggableNote dn)
            {
                if (NotesCanvas.Children.Contains(dn))
                    NotesCanvas.Children.Remove(dn);

                if (_selectedNote == dn)
                    _selectedNote = null;
            }
        }

        private void SelectNote(DraggableNote note)
        {
            // clear highlight of all notes
            foreach (var child in NotesCanvas.Children)
            {
                if (child is DraggableNote dn)
                    dn.IsSelected = false;
            }

            _selectedNote = note;
            _selectedNote.IsSelected = true;
        }

        private void NotesCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // if click was directly on canvas and not on a child > clear selection
            if (e.OriginalSource is Canvas)
            {
                ClearSelectedNote();
                NotesCanvas.Focus();
            }
        }

        private void ClearSelectedNote()
        {
            if (_selectedNote != null)
            {
                _selectedNote.IsSelected = false;
                _selectedNote = null;
            }
        }

        private void RemoveNote_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNote == null)
            {
                MessageBox.Show("Please select a note to remove.", "No Note Selected",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Ask for confirmation
            var result = MessageBox.Show(
                "Are you sure you want to delete this note?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (NotesCanvas.Children.Contains(_selectedNote))
                    NotesCanvas.Children.Remove(_selectedNote);
                _selectedNote = null;
            }
        }
    }
}
