using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using System.ComponentModel;
using RealNotes; 

namespace RealNotes
{
    public partial class MainWindow : Window
    {
        // track selected wrapper control
        private DraggableNote? _selectedNote;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += (_, _) => { var p = GetDefaultSavePath(); if (File.Exists(p)) LoadNotes(p); };
            this.Closing += (_, _) => { SaveNotes(GetDefaultSavePath()); };
            // ensure default preview is correct // THIS NEEDS EDITING //
            ColorPicker.SelectedIndex = 0;
            UpdateColorPickerPreview();
        }
        private class SavedNoteDto
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public string Text { get; set; } = "";
            public string Color { get; set; } = "#FFFFFF";
        }

        private string GetDefaultSavePath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RealNotes");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "notes.json");
        }
        // Save notes method
        public void SaveNotes(string path)
        {
            try
            {
                var list = new List<SavedNoteDto>();
                foreach (var child in NotesCanvas.Children)
                {
                    if (child is DraggableNote dn)
                    {
                        double x = Canvas.GetLeft(dn); if (double.IsNaN(x)) x = 0;
                        double y = Canvas.GetTop(dn); if (double.IsNaN(y)) y = 0;
                        string colorStr = "#FFFFFF";
                        if (dn.NoteBackground is SolidColorBrush scb)
                            colorStr = scb.Color.ToString(); // "#AARRGGBB"
                        list.Add(new SavedNoteDto
                        {
                            X = x,
                            Y = y,
                            Width = dn.Width,
                            Height = dn.Height,
                            Text = dn.NoteText ?? "",
                            Color = colorStr
                        });
                    }
                }
                var opts = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(path, JsonSerializer.Serialize(list, opts));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // Load notes method
        public void LoadNotes(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                var json = File.ReadAllText(path);
                var list = JsonSerializer.Deserialize<List<SavedNoteDto>>(json);
                if (list == null) return;

                NotesCanvas.Children.Clear();
                foreach (var dto in list)
                {
                    var note = new DraggableNote
                    {
                        Width = dto.Width > 0 ? dto.Width : 150,
                        Height = dto.Height > 0 ? dto.Height : 150,
                        NoteText = dto.Text ?? ""
                    };

                    // set color safely
                    try
                    {
                        var colorObj = ColorConverter.ConvertFromString(dto.Color);
                        if (colorObj is Color c)
                            note.NoteBackground = new SolidColorBrush(c);
                    }
                    catch { /* ignore color parse errors */ }

                    Canvas.SetLeft(note, dto.X);
                    Canvas.SetTop(note, dto.Y);

                    note.NoteClicked += Note_NoteClicked;
                    note.NoteDeleted += Note_NoteDeleted;

                    NotesCanvas.Children.Add(note);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ColorPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorPicker.SelectedItem is ComboBoxItem selectedItem &&
                selectedItem.Content is string colorName)
            {
                var brush = GetBrushForName(colorName);

                // Use the ComboBox itself as the preview: set its Background and readable Foreground // THIS NEEDS EDITING //
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

        // Change return type to SolidColorBrush and mark as static
        private static SolidColorBrush GetBrushForName(string colorName) =>
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

        // THIS NEEDS EDITING //
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
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "JSON files|*.json", FileName = "notes.json", InitialDirectory = Path.GetDirectoryName(GetDefaultSavePath()) };
            if (dlg.ShowDialog() == true) SaveNotes(dlg.FileName);
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "JSON files|*.json" };
            if (dlg.ShowDialog() == true) LoadNotes(dlg.FileName);
        }

        private void AddNote_Click(object sender, RoutedEventArgs e)
        {
            var initialBrush = (ColorPicker.SelectedItem is ComboBoxItem sel && sel.Content is string n) ? GetBrushForName(n) : Brushes.LightYellow; // This handles what the starting color of the note should be

            var note = new DraggableNote 
            {
                Width = 150,
                Height = 150,
                NoteText = "New Note",
                NoteBackground = initialBrush
            };

            // place in canvas staggered placement
            double left = 10 + (NotesCanvas.Children.Count % 10) * 20;
            double top = 10 + (NotesCanvas.Children.Count / 10) * 20;
            Canvas.SetLeft(note, left);
            Canvas.SetTop(note, top);

            note.NoteClicked += Note_NoteClicked;
            note.NoteDeleted += Note_NoteDeleted;

            NotesCanvas.Children.Add(note);

            // ensure the new note renders on top
            BringNoteToFront(note);

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

            // ensure the selected note is on top
            BringNoteToFront(note);
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

        // bring a note to the top of the Canvas z-order
        private void BringNoteToFront(DraggableNote note)
        {
            int max = int.MinValue;
            foreach (UIElement child in NotesCanvas.Children)
            {
                if (child == note) continue;
                int z = Canvas.GetZIndex(child);
                if (z > max) max = z;
            }

            if (max == int.MinValue) max = 0;
            Canvas.SetZIndex(note, max + 1);
        }

        private void NormalizeZIndices()
        {
            int z = 0;
            foreach (UIElement child in NotesCanvas.Children)
                Canvas.SetZIndex(child, z++);
        }
    }
}
