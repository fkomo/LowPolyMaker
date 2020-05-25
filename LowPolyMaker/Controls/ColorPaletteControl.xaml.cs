using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows.Input;

namespace LowPolyMaker.Controls
{
	/// <summary>
	/// Interaction logic for ColorPaletteControl.xaml
	/// </summary>
	public partial class ColorPaletteControl : UserControl
	{
		const int MinRowLength = 8;

		int RowLength = MinRowLength;
		Color[] Colors { get; set; }

		public ColorPaletteControl(Color[] colors, int rowLength = MinRowLength)
		{
			InitializeComponent();

			RowLength = rowLength;

			Update(colors);
		}

		public void Update(Color[] colors)
		{
			Colors = ColorPalette.Sort(colors);
			CreatePalette(RowLength);
		}

		List<Border> ColorCells { get; set; } = new List<Border>();

		private void CreatePalette(int rowLength = MinRowLength)
		{
			ResetPaletteGrid();

			RowLength = rowLength;

			var firstRow = 2;
			var firstColumn = 1;

			for (var i = 0; i < Colors.Length; i++)
			{
				var color = Colors[i];
				if (i > 0 && i % rowLength == 0)
					ColorGrid.RowDefinitions.Insert(ColorGrid.RowDefinitions.Count - 2, new RowDefinition { Height = new GridLength(32) });

				var colorCell = new Border()
				{
					Child = new Label()
					{
						Foreground = new SolidColorBrush(Color.FromRgb(0xc0, 0xc0, 0xc0)),
						HorizontalAlignment = HorizontalAlignment.Center,
						VerticalAlignment = VerticalAlignment.Center,
					},
					Name = "ColorCell" + i,
					ContextMenu = new ContextMenu(),
				};
				SetCellColor(colorCell, color);

				var menuItem = new MenuItem
				{
					Header = "Merge as Average",
				};
				menuItem.Click += MergeSelectedColorsBtn_Click;

				colorCell.ContextMenuOpening += ColorCellContextMenuOpening;
				colorCell.ContextMenu.Items.Add(menuItem);

				Grid.SetRow(colorCell, i / rowLength + firstRow);
				Grid.SetColumn(colorCell, i % rowLength + firstColumn);
				ColorGrid.Children.Add(colorCell);
				ColorCells.Add(colorCell);
			}

			// header
			Grid.SetColumnSpan(Header, ColorGrid.ColumnDefinitions.Count);

			// window border
			Grid.SetRowSpan(WindowBorder, ColorGrid.RowDefinitions.Count - 1);
			Grid.SetColumnSpan(WindowBorder, ColorGrid.ColumnDefinitions.Count);

			// bottom row
			Grid.SetRow(LockCheck, ColorGrid.RowDefinitions.Count - 2);

			Grid.SetRow(ResetBtn, ColorGrid.RowDefinitions.Count - 2);
			Grid.SetColumn(ResetBtn, ColorGrid.ColumnDefinitions.Count - 5);

			Grid.SetRow(ApplyBtn, ColorGrid.RowDefinitions.Count - 2);
			Grid.SetColumn(ApplyBtn, ColorGrid.ColumnDefinitions.Count - 3);
		}

		private void SetCellColor(Border colorCell, Color color)
		{
			colorCell.Background = new SolidColorBrush(color);
			(colorCell.Child as Label).Content = $"#{ color.R.ToString("x2") }{ color.G.ToString("x2") }{ color.B.ToString("x2") }";
		}

		private void ResetPaletteGrid(int rowLength = MinRowLength)
		{
			foreach (var colorCell in ColorCells)
				ColorGrid.Children.Remove(colorCell);
			ColorCells.Clear();

			ColorGrid.RowDefinitions.Clear();
			ColorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			ColorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
			ColorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
			ColorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
			ColorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });

			ColorGrid.ColumnDefinitions.Clear();
			ColorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
			for (var i = 0; i < rowLength; i++)
				ColorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
			ColorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });

			Grid.SetColumnSpan(Header, ColorGrid.ColumnDefinitions.Count);
			Grid.SetRowSpan(WindowBorder, ColorGrid.RowDefinitions.Count - 1);
			Grid.SetColumnSpan(WindowBorder, ColorGrid.ColumnDefinitions.Count);
		}

		bool Moving { get; set; } = false;
		Point LastCursorPosition { get; set; }

		private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			Moving = true;
			LastCursorPosition = e.GetPosition(this);
		}

		private void Header_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			Moving = false;
		}

		private void WindowBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			// TODO resize window start
		}

		private void WindowBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			// TODO resize window end
		}

		private void Header_MouseEnter(object sender, MouseEventArgs e)
		{
			Header.Background = new SolidColorBrush(Color.FromRgb(0x45, 0x45, 0x45));
			Header.BorderBrush = Header.Background;
			HeaderLabel.Foreground = new SolidColorBrush(System.Windows.Media.Colors.White);
		}

		private void Header_MouseLeave(object sender, MouseEventArgs e)
		{
			Header.Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x25));
			Header.BorderBrush = Header.Background;
			HeaderLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xc0, 0xc0, 0xc0));

			Moving = false;
		}

		private void WindowBorder_MouseEnter(object sender, MouseEventArgs e)
		{
			WindowBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x45, 0x45, 0x45));
		}

		private void Header_MouseMove(object sender, MouseEventArgs e)
		{
			if (Moving)
			{
				var cursorPosition = e.GetPosition(this);

				var topLeft = VisualTreeHelper.GetOffset(this);

				Canvas.SetLeft(this, topLeft.X + (cursorPosition.X - LastCursorPosition.X));
				Canvas.SetTop(this, topLeft.Y + (cursorPosition.Y - LastCursorPosition.Y));

				// TODO snap to main window edges
			}
		}

		private void WindowBorder_MouseLeave(object sender, MouseEventArgs e)
		{
			WindowBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x25));

			// TODO resize window end
		}

		private void ColorGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			var cursorPosition = e.GetPosition(this);

			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
			{
				var colorIndex = GetColorIndexAt(cursorPosition);
				if (colorIndex != -1)
				{
					var colorCell = ColorCells[colorIndex];
					ToggleCellSelection(colorCell);
				}
			}
			else
				ClearCellSelection();
		}

		/// <summary>
		/// get color index under given cursor position
		/// </summary>
		/// <param name="point"></param>
		/// <returns></returns>
		private int GetColorIndexAt(Point cursorPosition)
		{
			// ignore clicks outside of color grid area
			if (cursorPosition.X < ColorGrid.ColumnDefinitions.First().Width.Value ||
				cursorPosition.X > Width - ColorGrid.ColumnDefinitions.Last().Width.Value ||
				cursorPosition.Y < Header.Height + ColorGrid.RowDefinitions[1].Height.Value ||
				cursorPosition.Y > Height - ColorGrid.RowDefinitions.Last().Height.Value)
				return -1;

			var innerPosition = new Point(cursorPosition.X - ColorGrid.ColumnDefinitions.First().Width.Value,
				cursorPosition.Y - (Header.Height + ColorGrid.RowDefinitions[1].Height.Value));

			var colorIndex = ((int)innerPosition.Y / 32) * RowLength + (int)innerPosition.X / 64;
			if (colorIndex < Colors.Length)
				return colorIndex;

			return -1;
		}

		Border HilightedCell { get; set; }
		List<Border> SelectedCells { get; set; } = new List<Border>();

		private void ToggleCellSelection(Border colorCell)
		{
			if (!SelectedCells.Contains(colorCell))
			{
				colorCell.Opacity = .5;
				SelectedCells.Add(colorCell);
			}
			else
			{
				colorCell.Opacity = 1;
				SelectedCells.Remove(colorCell);
			}
		}

		private void ClearCellSelection()
		{
			foreach (var colorCell in SelectedCells)
				colorCell.Opacity = 1;
			SelectedCells.Clear();
		}

		/// <summary>
		/// hilight new cell and dehilight old (if needed)
		/// </summary>
		/// <param name="cell"></param>
		private void HilightCell(Border cell)
		{
			if ((cell == null && HilightedCell != null) || (HilightedCell != null && HilightedCell != cell))
			{
				// dehilight old
				(HilightedCell.Child as Label).Foreground = new SolidColorBrush(Color.FromRgb(0xc0, 0xc0, 0xc0));
				HilightedCell = null;
			}

			if (cell != null && HilightedCell != cell)
			{
				// hilight new
				HilightedCell = cell;
				(HilightedCell.Child as Label).Foreground = new SolidColorBrush(System.Windows.Media.Colors.White);
			}
		}

		private void ColorGrid_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
		{
			var cursorPosition = e.GetPosition(this);

			var colorIndex = GetColorIndexAt(cursorPosition);
			if (colorIndex != -1)
				HilightCell(ColorCells[colorIndex]);
			else
				HilightCell(null);
		}

		private void MergeSelectedColorsBtn_Click(object sender, RoutedEventArgs e)
		{
			if (SelectedCells.Count == 0)
				return;

			var avgColor = ColorPalette.GetAverage(SelectedCells.Select(c => (c.Background as SolidColorBrush).Color).ToArray());

			var newColors = new List<Color>();
			newColors.Add(avgColor);

			foreach (var color in Colors)
			{
				if (SelectedCells.Any(sc => ColorPalette.CompareColors(color, (sc.Background as SolidColorBrush).Color)))
					continue;

				newColors.Add(color);
			}

			Colors = ColorPalette.Sort(newColors.ToArray());
			CreatePalette(RowLength);
		}

		private void ColorCellContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			if (!SelectedCells.Contains(sender as Border))
				e.Handled = true;
		}

		private void ResetBtn_Click(object sender, RoutedEventArgs e)
		{
			ColorPalette.Current.Fill(Graph.Current);
			Update(ColorPalette.Current.Colors.ToArray());

			LockCheck.IsChecked = false;
		}

		private void ApplyBtn_Click(object sender, RoutedEventArgs e)
		{
			ColorPalette.Current.SetColors(Colors);
			ColorPalette.Current.Locked = LockCheck.IsChecked == true;

			Graph.Current.ApplyColorPalette(ColorPalette.Current);
		}

		private void LockCheck_Checked(object sender, RoutedEventArgs e)
		{
			ColorPalette.Current.Locked = LockCheck.IsChecked == true;
		}
	}
}
