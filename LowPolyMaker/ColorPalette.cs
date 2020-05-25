using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LowPolyMaker
{
	public class ColorPalette
	{
		public static ColorPalette Current { get; set; } = new ColorPalette();

		// TODO add TriangleAlpha to UI
		public byte TriangleAlpha { get; set; } = 200;

		public bool Locked { get; set; } = false;
		public List<Color> Colors { get; private set; } = new List<Color>();
		public byte[] ImagePixels { get; private set; } = null;

		Size ImageSize { get; set; }

		Dictionary<int, int> ColorUsage { get; set; } = new Dictionary<int, int>();

		public void SetImage(BitmapImage image)
		{
			ImageSize = new Size(image.PixelWidth, image.PixelHeight);
			ImagePixels = new byte[image.PixelWidth * image.PixelHeight * 4];

			image.CopyPixels(ImagePixels, image.PixelWidth * 4, 0);

			Clear();
		}

		public void Fill(Graph graph)
		{
			Clear();
			foreach (var triangle in graph.Triangles)
				// GetTriangleColor add new colors to palette, and also updates color usage
				 GetTriangleColor(triangle);
		}

		public void SetColors(Color[] colors)
		{
			Clear();
			Colors.AddRange(colors);
		}

		/// <summary>
		/// get average color from all pixels inside triangle
		/// </summary>
		/// <param name="triangle"></param>
		/// <returns></returns>
		public Color GetTriangleColor(GraphTriangle triangle, bool readOnly = false)
		{
			if (ImagePixels == null || ImagePixels.Length < 1 || (Locked && Colors.Count == 0))
				return Color.FromArgb(128, 0xff, 0xff, 0xff);

			var bbox = triangle.GetBoundingBox();

			var rowLength = (int)(bbox.BottomRight.X - bbox.TopLeft.X);

			long avgA = 0;
			long avgR = 0;
			long avgG = 0;
			long avgB = 0;
			long pixelCount = 0;
			for (var y = (int)bbox.TopLeft.Y; y < bbox.BottomRight.Y; y++)
			{
				for (var x = (int)bbox.TopLeft.X; x < bbox.BottomRight.X; x++)
				{
					var pixelOffset = (y * (int)ImageSize.Width + x) * 4;
					var pixel = Color.FromArgb(
						ImagePixels[pixelOffset + 3],
						ImagePixels[pixelOffset + 2],
						ImagePixels[pixelOffset + 1],
						ImagePixels[pixelOffset + 0]);

					if (triangle.IsPointInside(new Point(x, y)))
					{
						avgA += pixel.A;
						avgR += pixel.R;
						avgG += pixel.G;
						avgB += pixel.B;
						pixelCount++;
					}
				}
			}

			if (pixelCount == 0)
				pixelCount = 1;

			var newColor = Color.FromArgb(
				TriangleAlpha,
				(byte)(avgR / pixelCount),
				(byte)(avgG / pixelCount),
				(byte)(avgB / pixelCount));

			if (Locked)
			{
				newColor = Colors.OrderBy(c => ColorDistance(newColor, c)).First();
				var newColorKey = GetColorKey(newColor);

				if (!ColorUsage.ContainsKey(newColorKey))
					ColorUsage.Add(newColorKey, 1);
				else
					ColorUsage[newColorKey]++;
			}
			else
			{
				var newColorKey = GetColorKey(newColor);
				var oldColor = (triangle.Shape.Fill as SolidColorBrush)?.Color;

				if (!oldColor.HasValue || !CompareColors(newColor, oldColor.Value) || !ColorUsage.ContainsKey(newColorKey))
				{
					if (Colors.Any(c => c.R == newColor.R && c.G == newColor.G && c.B == newColor.B))
						ColorUsage[newColorKey]++;
					else
					{
						Colors.Add(newColor);
						ColorUsage.Add(newColorKey, 1);
					}

					if (oldColor.HasValue && !CompareColors(newColor, oldColor.Value))
						RemoveColor(oldColor.Value);
				}
			}

			return newColor;
		}

		public void RemoveColor(Color? colorToRemove)
		{
			if (!colorToRemove.HasValue)
				return;

			var colorKey = GetColorKey(colorToRemove.Value);
			if (ColorUsage[colorKey] == 1)
			{
				var color = Colors.Single(c => CompareColors(c, colorToRemove.Value));

				Colors.Remove(color);
				ColorUsage.Remove(colorKey);
			}
			else
				ColorUsage[colorKey]--;
		}

		public static int GetColorKey(Color color)
		{
			return (color.R << 24) + (color.G << 16) + (color.B << 8);// + color.A;
		}

		private void Clear()
		{
			ColorUsage.Clear();
			Colors.Clear();

			Locked = false;
		}

		public static bool CompareColors(Color color1, Color color2)
		{
			return color1.R == color2.R && color1.G == color2.G && color1.B == color2.B;
		}

		/// <summary>
		/// distance between 2 colors in 3d space (xyz -> rgb)
		/// </summary>
		/// <param name="color1"></param>
		/// <param name="color2"></param>
		/// <returns></returns>
		private static double ColorDistance(Color color1, Color color2)
		{
			return Math.Sqrt(
				((int)color1.R - color2.R) * ((int)color1.R - color2.R) +
				((int)color1.G - color2.G) * ((int)color1.G - color2.G) +
				((int)color1.B - color2.B) * ((int)color1.B - color2.B));
		}

		public static Color GetAverage(Color[] colors)
		{
			return new Color
			{
				A = (byte)colors.Average(c => c.A),
				R = (byte)colors.Average(c => c.R),
				G = (byte)colors.Average(c => c.G),
				B = (byte)colors.Average(c => c.B),
			};
		}

		/// <summary>
		/// sort colors (in 3d color space 0-255) by nearest neighbour path
		/// </summary>
		/// <param name="colorsToSort"></param>
		/// <returns></returns>
		public static Color[] Sort(Color[] colorsToSort)
		{
			var distanceMatrix = new double[colorsToSort.Length, colorsToSort.Length];
			for (var y = 0; y < colorsToSort.Length; y++)
			{
				for (var x = y + 1; x < colorsToSort.Length; x++)
					distanceMatrix[y, x] = ColorDistance(colorsToSort[x], colorsToSort[y]);

				for (var x = 0; x < y; x++)
					distanceMatrix[y, x] = distanceMatrix[x, y];
			}

			var newColors = new Color[colorsToSort.Length];
			var nnPath = NearestNeighbour(distanceMatrix);
			for (var nn = 0; nn < nnPath.Length; nn++)
				newColors[nn] = colorsToSort[nnPath[nn]];

			return newColors;
		}

		/// <summary>
		/// returns shortest path for given distance matrix
		/// </summary>
		/// <param name="distanceMatrix"></param>
		/// <returns></returns>
		private static int[] NearestNeighbour(double[,] distanceMatrix)
		{
			var nodeCount = distanceMatrix.GetLength(0);

			var visited = new bool[nodeCount];

			var shortestPath = new int[nodeCount];
			var shortestPathCost = double.MaxValue;

			var path = new int[nodeCount];

			// for all starting points
			for (var start = 0; start < nodeCount; start++)
			{
				// reset visited
				for (var v = 0; v < visited.Length; v++)
					visited[v] = false;
				visited[start] = true;

				// set starting node
				path[0] = start;

				// fill remaining nodes
				double cost = 0;
				for (var node = 1; node < nodeCount; node++)
				{
					// find closest unvisited node
					var closest = double.MaxValue;
					for (var n = 0; n < nodeCount; n++)
						if (!visited[n] && distanceMatrix[path[node - 1], n] < closest)
						{
							closest = distanceMatrix[path[node - 1], n];
							path[node] = n;
						}
					visited[path[node]] = true;
					cost += closest;
				}

				// shortest path ?
				if (cost < shortestPathCost)
				{
					shortestPathCost = cost;
					path.CopyTo(shortestPath, 0);
				}
			}

			return shortestPath;
		}
	}
}
