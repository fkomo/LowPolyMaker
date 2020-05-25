using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Linq;
using System.IO;
using LowPolyMaker.Controls;

namespace LowPolyMaker
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		#region constants

		const int RadiusMultiplier = 2;
		const string ApplicationName = "LowPolyMaker";

		#endregion

		public MainWindow()
		{
			InitializeComponent();
		}

		#region MainCanvas events

		private void MainCanvas_Loaded(object sender, RoutedEventArgs e)
		{
			if (Debugger.IsAttached)
				OpenFile();
			else
				Reset();
		}

		private void MainCanvas_MouseDown(object sender, MouseButtonEventArgs e)
		{
			// ignore cursor on canvas while palette window present
			if (ColorPaletteWindow?.Visibility == Visibility.Visible)
				return;

			var cursorPosition = e.GetPosition(MainCanvas);

			if (e.ChangedButton == MouseButton.Left)
			{
				// open file with double click
				//if (e.ClickCount == 2 && ImageBrush == null)
				//{
				//	OpenFile();
				//	return;
				//}

				if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
				{
					CancelPointSelection();

					// grab hilighted point
					if (HilightedPoint != null)
					{
						CancelGraphPointInProgress();
						PointMoving = true;
					}
				}
			}
			else if (e.ChangedButton == MouseButton.Middle)
			{
				CancelGraphPointInProgress();

				// grab canvas
				CanvasMoving = true;
				LastCanvasPosition = cursorPosition;
			}
		}

		private void MainCanvas_MouseUp(object sender, MouseButtonEventArgs e)
		{
			if (ImageLoaded.HasValue && ImageLoaded.Value.AddSeconds(1) > DateTime.Now)
				return;

			// ignore cursor on canvas while palette window present
			if (ColorPaletteWindow?.Visibility == Visibility.Visible)
				return;

			var cursorPosition = e.GetPosition(MainCanvas);

			if (e.ChangedButton == MouseButton.Left)
			{
				// release hilighted point
				if (PointMoving)
				{
					//foreach (var triangle in TrianglesChanged)
					//	(triangle.Shape.Fill as SolidColorBrush).Color = ColorPalette.Current.GetTriangleColor(triangle);
					//TrianglesChanged.Clear();

					PointMoving = false;
				}
				else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
				{
					// add isolated point (ignore suggested edges)
					if (HilightedPoint == null)
						AddPoint(cursorPosition);

					// select point for new triangle
					else
					{
						AddPointToSelection(HilightedPoint);
						HilightedPoint = null;
					}
				}
				// try to add suggested edge (EdgeInProgress)
				else if (EdgeInProgress != null)
				{
					if (AddEdge(EdgeInProgress))
					{
						MainCanvas.Children.Remove(EdgeInProgress.SnapToShape);

						if (TriangleInProgress != null)
						{
							// try to add second suggested edge (TriangleInProgress)
							if (AddEdge(TriangleInProgress))
								AddTriangle(EdgeInProgress.Start, EdgeInProgress.End, TriangleInProgress.Start);

							MainCanvas.Children.Remove(TriangleInProgress.SnapToShape);
							TriangleInProgress = null;
						}

						EdgeInProgress = null;
					}
				}
				// add point
				else
					AddPoint(cursorPosition);
			}
			else if (e.ChangedButton == MouseButton.Right)
			{
				if (HilightedPoint != null)
					RemovePoint(HilightedPoint);

				HilightedPoint = null;
			}
			else if (e.ChangedButton == MouseButton.Middle)
			{
				// release canvas
				CanvasMoving = false;
			}
		}

		private void MainCanvas_MouseMove(object sender, MouseEventArgs e)
		{
			SetStatus(string.Empty);

			// TODO snap everything to grid

			var cursorPosition = e.GetPosition(MainCanvas);

			// ignore cursor on canvas while palette window present
			if (ColorPaletteWindow?.Visibility == Visibility.Visible)
			{
				CancelGraphPointInProgress();
				CancelEdgeInProgress();
				return;
			}

			// ignore cursor outside of canvas
			if (cursorPosition.Y <= 0 || cursorPosition.X <= 0 || cursorPosition.X > MainCanvas.Width || cursorPosition.Y > MainCanvas.Height)
			{
				CancelGraphPointInProgress();
				CancelEdgeInProgress();
				return;
			}

			SetStatus($"[{ (int)cursorPosition.X }, { (int)cursorPosition.Y }]");

			// move canvas
			if (CanvasMoving)
			{
				var offset = Point.Subtract(cursorPosition, LastCanvasPosition);

				// TODO move canvas

				LastCanvasPosition = cursorPosition;
			}
			// move point
			else if (PointMoving)
			{
				HilightedPoint.Position = cursorPosition;

				Canvas.SetLeft(HilightedPoint.Shape, cursorPosition.X - HilightedPoint.Shape.Width / 2);
				Canvas.SetTop(HilightedPoint.Shape, cursorPosition.Y - HilightedPoint.Shape.Height / 2);

				PointMoved(HilightedPoint);
			}
			else
			{
				// hilight point under cursor (if there is any)
				if (!UpdateHilightedPoint(cursorPosition))
				{
					// nothing is hilighted

					// try to find suggested edge
					if (UpdateEdgeInProgress(cursorPosition))
						CancelGraphPointInProgress();

					else
						// nothing, just update new point under cursor
						UpdatePointInProgress(cursorPosition);
				}
				else
					CancelGraphPointInProgress();
			}
		}

		private void MainCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			// ignore cursor on canvas while palette window present
			if (ColorPaletteWindow?.Visibility == Visibility.Visible)
				return;

			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
			{
				SnapRadius += e.Delta / 10;

				if (SnapRadius < 0)
					SnapRadius = 0;

				CancelEdgeInProgress();

				UpdateEdgeInProgress(e.GetPosition(MainCanvas));
			}
			else
			{
				// TODO zoom canvas
			}
		}

		private void MainCanvas_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				if (ColorPaletteWindow.Visibility == Visibility.Visible)
				{
					TogglePaletteWindow();
					return;
				}

				CancelPointSelection();

				HilightGraphPoint(null);

				CancelEdgeInProgress();
			}
			else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.S)
			{
				Save();
			}
			else if (e.Key == Key.Q)
			{
				HideBackground();
			}
			else if (e.Key == Key.F1)
			{
				TogglePaletteWindow();
			}
		}

		private void MainCanvas_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Q)
			{
				ShowBackground();
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			var result = SaveBefore();

			if (result == MessageBoxResult.Cancel)
				e.Cancel = true;
		}

		#endregion

		private void HideBackground()
		{
			// hide background image
			//ImageBrush = MainCanvas.Background as ImageBrush;
			MainCanvas.Background = null;

			// hide points
			foreach (var point in Graph.Current.Points)
				point.Shape.Visibility = Visibility.Collapsed;

			// hide edges
			foreach (var edge in Graph.Current.Edges)
				edge.Shape.Visibility = Visibility.Collapsed;
		}

		private void ShowBackground()
		{
			// show background image
			MainCanvas.Background = ImageBrush;

			// show points
			foreach (var point in Graph.Current.Points)
				point.Shape.Visibility = Visibility.Visible;

			// show points
			foreach (var edge in Graph.Current.Edges)
				edge.Shape.Visibility = Visibility.Visible;
		}

		#region Point/Edge/Triangle manipulation

		int EdgeThickness { get; set; } = 2;
		int PointRadius { get; set; } = 4;
		int SnapRadius { get; set; } = 128;
		
		SolidColorBrush HilightedBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 0));
		SolidColorBrush SelectedBrush = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
		SolidColorBrush NormalBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

		SolidColorBrush EdgeBrush = new SolidColorBrush(Color.FromArgb(255, 0, 0, 255));
		SolidColorBrush EdgeSnapBrush = new SolidColorBrush(Color.FromArgb(64, 0, 0, 255));

		SolidColorBrush TriangleBrush = new SolidColorBrush(Color.FromArgb(255, 0, 255, 0));
		SolidColorBrush TriangleSnapBrush = new SolidColorBrush(Color.FromArgb(64, 0, 255, 0));

		GraphPoint PointInProgress { get; set; } = null;
		GraphEdge EdgeInProgress { get; set; } = null;
		GraphEdge TriangleInProgress { get; set; } = null;

		GraphPoint HilightedPoint { get; set; } = null;
		List<GraphPoint> SelectedPoints { get; set; } = new List<GraphPoint>();

		bool PointMoving { get; set; } = false;

		bool CanvasMoving { get; set; } = false;
		Point LastCanvasPosition { get; set; }

		private Ellipse CreatePointShape(Brush fill = null)
		{
			return new Ellipse()
			{
				Width = PointRadius * 2,
				Height = PointRadius * 2,
				Fill = fill == null ? NormalBrush : fill,
			};
		}

		private Ellipse CreateSuggestionShape(int radius, Brush fill)
		{
			return new Ellipse()
			{
				Width = radius * 2,
				Height = radius * 2,
				Fill = fill,
			};
		}

		private Line CreateEdgeShape(Point start, Point end)
		{
			var shape = new Line()
			{
				Stroke = NormalBrush,
				StrokeThickness = EdgeThickness,
				X1 = start.X,
				Y1 = start.Y,
				X2 = end.X,
				Y2 = end.Y,
			};

			MainCanvas.Children.Add(shape);

			return shape;
		}

		/// <summary>
		/// add new point to graph at given position
		/// </summary>
		/// <param name="position"></param>
		/// <returns></returns>
		private bool AddPoint(Point position)
		{
			if (position.X > MainCanvas.ActualWidth || position.X < 0
				|| position.Y > MainCanvas.ActualHeight || position.Y < 0)
				return false;

			var newGraphPoint = new GraphPoint(true)
			{
				Shape = CreatePointShape(),
				Position = position
			};

			Canvas.SetLeft(newGraphPoint.Shape, position.X - newGraphPoint.Shape.Width / 2);
			Canvas.SetTop(newGraphPoint.Shape, position.Y - newGraphPoint.Shape.Height / 2);
			MainCanvas.Children.Add(newGraphPoint.Shape);

			Graph.Current.Points.Add(newGraphPoint);

			UpdateGraphInfoText();

			return true;
		}

		/// <summary>
		/// remove point (and connected edges/triangles)
		/// </summary>
		/// <param name="pointToRemove"></param>
		private void RemovePoint(GraphPoint pointToRemove)
		{
			var edgesToRemove = new List<GraphEdge>();
			foreach (var edge in Graph.Current.Edges)
			{
				if (edge.Start == pointToRemove || edge.End == pointToRemove)
				{
					MainCanvas.Children.Remove(edge.Shape);
					edgesToRemove.Add(edge);
				}
			}

			foreach (var edgeToRemove in edgesToRemove)
				Graph.Current.Edges.Remove(edgeToRemove);

			// remove triangles
			var trianglesToRemove = new List<GraphTriangle>();
			foreach (var triangle in Graph.Current.Triangles)
				if (triangle.Point1.Id == pointToRemove.Id || triangle.Point2.Id == pointToRemove.Id || triangle.Point3.Id == pointToRemove.Id)
					trianglesToRemove.Add(triangle);

			foreach (var triangleToRemove in trianglesToRemove)
			{
				// update color palette
				ColorPalette.Current.RemoveColor((triangleToRemove.Shape.Fill as SolidColorBrush)?.Color);

				MainCanvas.Children.Remove(triangleToRemove.Shape);
				Graph.Current.Triangles.Remove(triangleToRemove);
			}
					   
			if (SelectedPoints.Contains(pointToRemove))
				SelectedPoints.Remove(pointToRemove);

			MainCanvas.Children.Remove(pointToRemove.Shape);
			Graph.Current.Points.Remove(pointToRemove);

			// update color palette window
			ColorPaletteWindow?.Update(ColorPalette.Current.Colors.ToArray());

			UpdateGraphInfoText();
		}
		
		/// <summary>
		/// adds new edge connecting 2 points
		/// </summary>
		/// <param name="newEdge"></param>
		/// <returns></returns>
		private bool AddEdge(GraphEdge newEdge)
		{
			// ignore if end point is outside of background image
			if (newEdge.End.Position.X > MainCanvas.ActualWidth || newEdge.End.Position.X < 0
				|| newEdge.End.Position.Y > MainCanvas.ActualHeight || newEdge.End.Position.Y < 0)
				return false;

			// maintain start point color
			if (HilightedPoint == null || HilightedPoint != newEdge.Start)
				newEdge.Start.Shape.Fill = NormalBrush;

			newEdge.End.Shape.Fill = NormalBrush;
			newEdge.Shape.Stroke = NormalBrush;

			if (newEdge.EndPointId == -1)
				newEdge.EndPointId = newEdge.End.Id;
			if (newEdge.StartPointId == -1)
				newEdge.StartPointId = newEdge.Start.Id;

			if (!Graph.Current.Points.Contains(newEdge.End))
			{
				if (newEdge.End.Id == -1)
				{
					newEdge.End.Id = Graph.Current.GetNextId();
					newEdge.EndPointId = newEdge.End.Id;
				}

				Graph.Current.Points.Add(newEdge.End);
			}

			Graph.Current.Edges.Add(newEdge);

			UpdateGraphInfoText();

			return true;
		}

		/// <summary>
		/// add new triangle from given points
		/// </summary>
		/// <param name="p1"></param>
		/// <param name="p2"></param>
		/// <param name="p3"></param>
		/// <returns></returns>
		private bool AddTriangle(GraphPoint p1, GraphPoint p2, GraphPoint p3)
		{
			if (Graph.Current.Triangles.Any(t =>
				(t.Point1Id == p1.Id || t.Point2Id == p1.Id || t.Point3Id == p1.Id) &&
				(t.Point1Id == p2.Id || t.Point2Id == p2.Id || t.Point3Id == p2.Id) &&
				(t.Point1Id == p3.Id || t.Point2Id == p3.Id || t.Point3Id == p3.Id)))
				return false;

			var newTriangle = new GraphTriangle
			{
				Point1 = p1,
				Point2 = p2,
				Point3 = p3,
			};

			newTriangle.Shape = new Polygon
			{
				Points = new PointCollection()
				{
					newTriangle.Point1.Position,
					newTriangle.Point2.Position,
					newTriangle.Point3.Position
				},
			};

			var newColor = ColorPalette.Current.GetTriangleColor(newTriangle);
			newTriangle.Shape.Fill = new SolidColorBrush(newColor);

			Graph.Current.Triangles.Add(newTriangle);
			MainCanvas.Children.Add(newTriangle.Shape);

			// update color palette window
			ColorPaletteWindow?.Update(ColorPalette.Current.Colors.ToArray());

			UpdateGraphInfoText();

			return true;
		}

		List<GraphTriangle> TrianglesChanged { get; set; } = new List<GraphTriangle>();

		/// <summary>
		/// update edges and triangles connected to changedPoint
		/// </summary>
		/// <param name="changedPoint"></param>
		private void PointMoved(GraphPoint changedPoint)
		{
			// update edges
			foreach (var edge in Graph.Current.Edges)
			{
				if (edge.Start == changedPoint)
				{
					edge.Shape.X1 = changedPoint.Position.X;
					edge.Shape.Y1 = changedPoint.Position.Y;
				}
				else if (edge.End == changedPoint)
				{
					edge.Shape.X2 = changedPoint.Position.X;
					edge.Shape.Y2 = changedPoint.Position.Y;
				}
			}

			// update triangles
			foreach (var triangle in Graph.Current.Triangles)
			{
				var triangleChanged = false;

				if (triangle.Point1 == changedPoint)
				{
					triangle.Shape.Points[0] = changedPoint.Position;
					triangleChanged = true;
				}
				else if (triangle.Point2 == changedPoint)
				{
					triangle.Shape.Points[1] = changedPoint.Position;
					triangleChanged = true;
				}
				else if (triangle.Point3 == changedPoint)
				{
					triangle.Shape.Points[2] = changedPoint.Position;
					triangleChanged = true;
				}

				if (triangleChanged)
				{
					var newColor = ColorPalette.Current.GetTriangleColor(triangle);
					triangle.Shape.Fill = new SolidColorBrush(newColor);

					// update color palette window
					ColorPaletteWindow?.Update(ColorPalette.Current.Colors.ToArray());
				}
			}
		}

		/// <summary>
		/// hilight point under cursor (if there is one)
		/// </summary>
		/// <param name="cursorPosition"></param>
		/// <returns></returns>
		private bool UpdateHilightedPoint(Point cursorPosition)
		{
			foreach (var graphPoint in Graph.Current.Points)
			{
				if (Point.Subtract(graphPoint.Position, cursorPosition).Length < (PointRadius * RadiusMultiplier))
				{
					if (!SelectedPoints.Contains(graphPoint))
					{
						// there is point under cursor
						if (HilightedPoint != graphPoint)
							// new point found
							HilightGraphPoint(graphPoint);
					}

					return true;
				}
			}

			if (HilightedPoint != null)
				// no point found because cursor left old hilighted point
				HilightGraphPoint(null);

			return false;
		}

		/// <summary>
		/// hilight point
		/// </summary>
		/// <param name="point"></param>
		private void HilightGraphPoint(GraphPoint point)
		{
			// ignore selected points
			if (SelectedPoints.Contains(point) || (HilightedPoint != null && SelectedPoints.Contains(HilightedPoint)))
				return;

			// reset old hilighted point to default state
			if (HilightedPoint != null)
			{
				HilightedPoint.Shape.Width = PointRadius * 2;
				HilightedPoint.Shape.Height = PointRadius * 2;
				HilightedPoint.Shape.Fill = NormalBrush;

				Canvas.SetLeft(HilightedPoint.Shape, HilightedPoint.Position.X - HilightedPoint.Shape.Width / 2);
				Canvas.SetTop(HilightedPoint.Shape, HilightedPoint.Position.Y - HilightedPoint.Shape.Height / 2);

				HilightedPoint = null;
			}

			if (point != null)
			{
				// hilight new point
				HilightedPoint = point;

				CancelEdgeInProgress();

				HilightedPoint.Shape.Width = PointRadius * 2 * RadiusMultiplier;
				HilightedPoint.Shape.Height = PointRadius * 2 * RadiusMultiplier;
				HilightedPoint.Shape.Fill = HilightedBrush;

				Canvas.SetLeft(HilightedPoint.Shape, HilightedPoint.Position.X - HilightedPoint.Shape.Width / 2);
				Canvas.SetTop(HilightedPoint.Shape, HilightedPoint.Position.Y - HilightedPoint.Shape.Height / 2);

				CancelGraphPointInProgress();
			}
		}

		/// <summary>
		/// add point to selection (3 creates a triangle)
		/// </summary>
		/// <param name="point"></param>
		private void AddPointToSelection(GraphPoint point)
		{
			if (point == null || SelectedPoints.Contains(point))
				return;

			SelectGraphPoint(point);

			if (SelectedPoints.Count == 3)
			{
				// add missing edges
				if (!Graph.Current.Edges.Any(
					e => (e.Start == SelectedPoints[0] && e.End == SelectedPoints[1]) || (e.End == SelectedPoints[0] && e.Start == SelectedPoints[1])))
				{
					AddEdge(new GraphEdge
					{
						Start = SelectedPoints[0],
						End = SelectedPoints[1],
						Shape = CreateEdgeShape(SelectedPoints[0].Position, SelectedPoints[1].Position)
					});
				}

				if (!Graph.Current.Edges.Any(
					e => (e.Start == SelectedPoints[1] && e.End == SelectedPoints[2]) || (e.End == SelectedPoints[1] && e.Start == SelectedPoints[2])))
				{
					AddEdge(new GraphEdge
					{
						Start = SelectedPoints[1],
						End = SelectedPoints[2],
						Shape = CreateEdgeShape(SelectedPoints[1].Position, SelectedPoints[2].Position)
					});
				}

				if (!Graph.Current.Edges.Any(
					e => (e.Start == SelectedPoints[2] && e.End == SelectedPoints[0]) || (e.End == SelectedPoints[2] && e.Start == SelectedPoints[0])))
				{
					AddEdge(new GraphEdge
					{
						Start = SelectedPoints[2],
						End = SelectedPoints[0],
						Shape = CreateEdgeShape(SelectedPoints[2].Position, SelectedPoints[0].Position)
					});
				}

				AddTriangle(SelectedPoints[0], SelectedPoints[1], SelectedPoints[2]);

				CancelPointSelection();
			}
		}

		/// <summary>
		/// select point
		/// </summary>
		/// <param name="point"></param>
		private void SelectGraphPoint(GraphPoint point)
		{
			if (point != null)
			{
				// select new point
				SelectedPoints.Add(point);

				point.Shape.Width = PointRadius * 2 * RadiusMultiplier;
				point.Shape.Height = PointRadius * 2 * RadiusMultiplier;
				point.Shape.Fill = SelectedBrush;

				Canvas.SetLeft(point.Shape, point.Position.X - point.Shape.Width / 2);
				Canvas.SetTop(point.Shape, point.Position.Y - point.Shape.Height / 2);
			}
		}

		/// <summary>
		/// resets already selected points
		/// </summary>
		private void CancelPointSelection()
		{
			foreach (var point in SelectedPoints)
			{
				point.Shape.Width = PointRadius * 2;
				point.Shape.Height = PointRadius * 2;
				point.Shape.Fill = NormalBrush;

				Canvas.SetLeft(point.Shape, point.Position.X - point.Shape.Width / 2);
				Canvas.SetTop(point.Shape, point.Position.Y - point.Shape.Height / 2);
			}

			SelectedPoints.Clear();
		}

		/// <summary>
		/// update new suggested point under cursor
		/// </summary>
		/// <param name="cursorPosition"></param>
		private void UpdatePointInProgress(Point cursorPosition)
		{
			if (PointInProgress == null)
			{
				// create new
				PointInProgress = new GraphPoint(false)
				{
					Shape = CreatePointShape()
				};

				MainCanvas.Children.Add(PointInProgress.Shape);
			}

			// update position
			PointInProgress.Position = cursorPosition;
			Canvas.SetLeft(PointInProgress.Shape, PointInProgress.Position.X - PointInProgress.Shape.Width / 2);
			Canvas.SetTop(PointInProgress.Shape, PointInProgress.Position.Y - PointInProgress.Shape.Height / 2);
		}

		/// <summary>
		/// update new suggested edge
		/// PointInProgress needs to be in close range (SnapRadius) to another point
		/// </summary>
		/// <param name="cursorPosition"></param>
		/// <returns></returns>
		private bool UpdateEdgeInProgress(Point cursorPosition)
		{
			// get closest point in snap radius
			var closestPoint = null as GraphPoint;
			var closestDistance = double.MaxValue;
			foreach (var graphPoint in Graph.Current.Points)
			{
				// ignore selected points
				if (SelectedPoints.Contains(graphPoint))
					continue;

				var distance = Point.Subtract(graphPoint.Position, cursorPosition).Length;
				if (distance < SnapRadius && distance < closestDistance)
				{
					closestDistance = distance;
					closestPoint = graphPoint;
				}
			}

			if (closestPoint != null)
			{
				// if there already is another edge in progress with different starting point, cancel it
				if (EdgeInProgress != null && EdgeInProgress.Start != closestPoint)
					CancelEdgeInProgress();

				// create new edge in progress
				if (EdgeInProgress == null)
				{
					closestPoint.Shape.Fill = EdgeBrush;

					EdgeInProgress = new GraphEdge
					{
						Start = closestPoint,
						End = new GraphPoint(false)
						{
							Shape = CreatePointShape(EdgeBrush)
						},
						Shape = new Line()
						{
							Stroke = EdgeBrush,
							StrokeThickness = EdgeThickness,
							X1 = closestPoint.Position.X,
							Y1 = closestPoint.Position.Y,
						},
						SnapToShape = CreateSuggestionShape(SnapRadius, EdgeSnapBrush)
					};

					MainCanvas.Children.Add(EdgeInProgress.Shape);
					MainCanvas.Children.Add(EdgeInProgress.End.Shape);
					MainCanvas.Children.Add(EdgeInProgress.SnapToShape);
				}

				// update edge in progress ending point to mouse cursor position
				EdgeInProgress.End.Position = cursorPosition;

				EdgeInProgress.Shape.X2 = EdgeInProgress.End.Position.X;
				EdgeInProgress.Shape.Y2 = EdgeInProgress.End.Position.Y;

				Canvas.SetLeft(EdgeInProgress.SnapToShape, EdgeInProgress.End.Position.X - EdgeInProgress.SnapToShape.Width / 2);
				Canvas.SetTop(EdgeInProgress.SnapToShape, EdgeInProgress.End.Position.Y - EdgeInProgress.SnapToShape.Height / 2);

				Canvas.SetLeft(EdgeInProgress.End.Shape, EdgeInProgress.End.Position.X - EdgeInProgress.End.Shape.Width / 2);
				Canvas.SetTop(EdgeInProgress.End.Shape, EdgeInProgress.End.Position.Y - EdgeInProgress.End.Shape.Height / 2);

				UpdateTriangleInProgress(cursorPosition);

				return true;
			}

			// no point found, cancel current edge in progress
			else if (EdgeInProgress != null)
				CancelEdgeInProgress();

			return false;
		}

		/// <summary>
		/// update suggested edge forming a triangle
		/// PointInProgress needs to be close to center of another edge
		/// </summary>
		/// <param name="cursorPosition"></param>
		private void UpdateTriangleInProgress(Point cursorPosition)
		{
			// check if there is connected edge close enought to form a triangle 
			var bestEdgeHalfLength = double.MaxValue;
			var bestEdgeCenter = new Point();
			var closestPointToTriangle = null as GraphPoint;
			var closestDistanceToTriangle = double.MaxValue;
			foreach (var edge in Graph.Current.Edges)
			{
				// ignore selected points
				if (SelectedPoints.Contains(edge.Start) || SelectedPoints.Contains(edge.End))
					continue;

				var edgeHalfLength = Point.Subtract(edge.Start.Position, edge.End.Position).Length / 2;
				var edgeCenter = new Point((edge.Start.Position.X + edge.End.Position.X) / 2, (edge.Start.Position.Y + edge.End.Position.Y) / 2);

				if (edge.Start == EdgeInProgress.Start || edge.End == EdgeInProgress.Start)
				{
					var distance = Point.Subtract(edgeCenter, EdgeInProgress.End.Position).Length;
					if (distance < edgeHalfLength && distance < closestDistanceToTriangle)
					{
						closestDistanceToTriangle = distance;
						closestPointToTriangle = (edge.Start == EdgeInProgress.Start) ? edge.End : edge.Start;

						bestEdgeHalfLength = edgeHalfLength;
						bestEdgeCenter = edgeCenter;
					}
				}
			}

			if (closestPointToTriangle != null)
			{
				// if there already is another edge in progress with different starting point, cancel it
				if (TriangleInProgress != null && TriangleInProgress.Start != closestPointToTriangle)
					CancelTriangleInProgress();

				// create new triangle edge in progress
				if (TriangleInProgress == null)
				{
					closestPointToTriangle.Shape.Fill = TriangleBrush;

					TriangleInProgress = new GraphEdge
					{
						Start = closestPointToTriangle,
						End = EdgeInProgress.End,
						Shape = new Line()
						{
							Stroke = TriangleBrush,
							StrokeThickness = EdgeThickness,
							X1 = closestPointToTriangle.Position.X,
							Y1 = closestPointToTriangle.Position.Y,
						},
						SnapToShape = CreateSuggestionShape((int)bestEdgeHalfLength, TriangleSnapBrush)
					};

					MainCanvas.Children.Add(TriangleInProgress.Shape);
					MainCanvas.Children.Add(TriangleInProgress.SnapToShape);

					Canvas.SetLeft(TriangleInProgress.SnapToShape, bestEdgeCenter.X - TriangleInProgress.SnapToShape.Width / 2);
					Canvas.SetTop(TriangleInProgress.SnapToShape, bestEdgeCenter.Y - TriangleInProgress.SnapToShape.Height / 2);
				}

				TriangleInProgress.Shape.X2 = TriangleInProgress.End.Position.X;
				TriangleInProgress.Shape.Y2 = TriangleInProgress.End.Position.Y;
			}
			// no point found, cancel current triangle edge in progress
			else if (TriangleInProgress != null)
				CancelTriangleInProgress();
		}

		/// <summary>
		/// cancel current suggested point
		/// </summary>
		private void CancelGraphPointInProgress()
		{
			if (PointInProgress != null)
				MainCanvas.Children.Remove(PointInProgress.Shape);

			PointInProgress = null;
		}

		/// <summary>
		/// cancel current suggested edge connecting to closest point
		/// </summary>
		private void CancelEdgeInProgress()
		{
			if (EdgeInProgress != null)
			{
				// maintain color of starting point
				if (HilightedPoint == null || HilightedPoint != EdgeInProgress.Start)
					EdgeInProgress.Start.Shape.Fill = NormalBrush;

				EdgeInProgress.End.Shape.Fill = NormalBrush;

				MainCanvas.Children.Remove(EdgeInProgress.Shape);

				if (EdgeInProgress.SnapToShape != null)
					MainCanvas.Children.Remove(EdgeInProgress.SnapToShape);

				if (!Graph.Current.Points.Contains(EdgeInProgress.End))
					MainCanvas.Children.Remove(EdgeInProgress.End.Shape);
			}

			EdgeInProgress = null;

			CancelTriangleInProgress();
		}

		/// <summary>
		/// cancel current suggested edge forming a triangle
		/// </summary>
		private void CancelTriangleInProgress()
		{
			if (TriangleInProgress != null)
			{
				TriangleInProgress.Start.Shape.Fill = NormalBrush;

				MainCanvas.Children.Remove(TriangleInProgress.SnapToShape);
				MainCanvas.Children.Remove(TriangleInProgress.Shape);
			}

			TriangleInProgress = null;
		}

		#endregion

		#region Coloring

		DateTime? ImageLoaded { get; set; }
		ImageBrush ImageBrush { get; set; }

		#endregion

		#region Saving & Loading

		private void OpenFile()
		{
			try
			{
				SaveBefore();

				var dialog = new OpenFileDialog()
				{
					Filter = $"{ ApplicationName } files (*{ Graph.FileExtension })|*{ Graph.FileExtension }|"
							+ "Image files (*.jpg,*.jpeg,*.jpe,*.jfif,*.png,*.bmp)|*.jpg;*.jpeg;*.jpe;*.jfif;*.png;*.bmp",
					Multiselect = false,
					CheckFileExists = true
				};

				var filename = dialog.ShowDialog().Value ? dialog.FileName : null;
				if (filename != null)
				{
					if (filename.EndsWith(Graph.FileExtension))
						LoadGraph(filename);
					else
					{
						LoadImage(filename);

						Graph.Current.Filename = null;
						Graph.Current.ImageFilename = filename;
					}
				}
				else
				{
					MainCanvas.Background = Background;
					MainCanvas.Width = Width;
					MainCanvas.Height = Height;
				}

				MainCanvas.Focus();
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}
		
		private void Save()
		{
			if (ColorPalette.Current.Locked)
			{
				// TODO save color palette
			}

			if (Graph.Current.Filename != null)
				SaveGraph(Graph.Current.Filename);

			else
				SaveAs();
		}

		private void SaveAs()
		{
			var fileDialog = new SaveFileDialog()
			{
				Filter = $"{ ApplicationName } files (*{ Graph.FileExtension })|*{ Graph.FileExtension }",
			};
			{
				if (fileDialog.ShowDialog() == true)
					SaveGraph(fileDialog.FileName);
			}
		}

		private void SaveGraph(string graphFilename)
		{
			try
			{
				Graph.Current.Filename = graphFilename;

				File.WriteAllText(graphFilename, Graph.Serialize(Graph.Current));

				SetStatus($"{ DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") } saved");

				Title = $"{ ApplicationName } [{ Graph.Current.Filename }]";
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}

		private void LoadImage(string imageFilename)
		{
			try
			{
				if (imageFilename != null)
				{
					var bitmapImage = new BitmapImage(new Uri(imageFilename, UriKind.Absolute));

					ImageBrush = new ImageBrush();
					ImageBrush.ImageSource = bitmapImage;

					MainCanvas.Background = ImageBrush;
					MainCanvas.Width = bitmapImage.PixelWidth;
					MainCanvas.Height = bitmapImage.PixelHeight;

					RenderOptions.SetBitmapScalingMode(MainCanvas.Background, BitmapScalingMode.NearestNeighbor);

					ColorPalette.Current.SetImage(bitmapImage);
				}

				MainCanvas.Focus();

				ImageLoaded = DateTime.Now;
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}

		private void LoadGraph(string graphFilename)
		{
			try
			{
				Reset();

				Graph.Current = Graph.Deserialize(File.ReadAllText(graphFilename));

				LoadImage(Graph.Current.ImageFilename);
				Graph.Current.Filename = graphFilename;

				foreach (var point in Graph.Current.Points)
				{
					point.Shape = CreatePointShape();

					Canvas.SetLeft(point.Shape, point.Position.X - point.Shape.Width / 2);
					Canvas.SetTop(point.Shape, point.Position.Y - point.Shape.Height / 2);

					MainCanvas.Children.Add(point.Shape);
				}

				foreach (var edge in Graph.Current.Edges)
				{
					edge.Start = Graph.Current.Points.Single(p => p.Id == edge.StartPointId);
					edge.End = Graph.Current.Points.Single(p => p.Id == edge.EndPointId);
					edge.Shape = CreateEdgeShape(edge.Start.Position, edge.End.Position);
				}

				foreach (var triangle in Graph.Current.Triangles)
				{
					triangle.Point1 = Graph.Current.Points.Single(p => p.Id == triangle.Point1Id);
					triangle.Point2 = Graph.Current.Points.Single(p => p.Id == triangle.Point2Id);
					triangle.Point3 = Graph.Current.Points.Single(p => p.Id == triangle.Point3Id);
					triangle.Shape = new Polygon
					{
						Points = new PointCollection()
					{
						triangle.Point1.Position,
						triangle.Point2.Position,
						triangle.Point3.Position
					},
					};

					MainCanvas.Children.Add(triangle.Shape);
				}

				// TODO load color palette

				// set triangle colors based on current palette
				foreach (var triangle in Graph.Current.Triangles)
				{
					var triangleColor = ColorPalette.Current.GetTriangleColor(triangle);
					triangle.Shape.Fill = new SolidColorBrush(triangleColor);
				}

				UpdateGraphInfoText();

				Title = $"{ ApplicationName } [{ Graph.Current.Filename }]";
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}

		#endregion

		#region Menu

		ColorPaletteControl ColorPaletteWindow { get; set; }

		/// <summary>
		/// toggle color palette window
		/// </summary>
		private void TogglePaletteWindow()
		{
			if (ColorPaletteWindow == null)
			{
				ColorPaletteWindow = new ColorPaletteControl(ColorPalette.Current.Colors.ToArray());
				MainCanvas.Children.Add(ColorPaletteWindow);

				Canvas.SetZIndex(ColorPaletteWindow, int.MaxValue);
			}
			else
			{
				if (ColorPaletteWindow.Visibility == Visibility.Collapsed)
					ColorPaletteWindow.Visibility = Visibility.Visible;
				else
					ColorPaletteWindow.Visibility = Visibility.Collapsed;
			}
		}

		private void ColorsBtn_Click(object sender, RoutedEventArgs e)
		{
			TogglePaletteWindow();
		}

		private void ExitBtn_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void OpenBtn_Click(object sender, RoutedEventArgs e)
		{
			OpenFile();
		}

		private void SaveBtn_Click(object sender, RoutedEventArgs e)
		{
			Save();
		}

		private void SaveAsBtn_Click(object sender, RoutedEventArgs e)
		{
			SaveAs();
		}

		private void ExportBtn_Click(object sender, RoutedEventArgs e)
		{
			// TODO export color palette as image table including color codes
			// TODO export graph as color image
			// TODO export graph as color image with color codes
			// TODO export image as black edge graph on white background

			// each triangle must be numbered
		}

		private void NewBtn_Click(object sender, RoutedEventArgs e)
		{
			SaveBefore();
			Reset();
		}

		private void AboutBtn_Click(object sender, RoutedEventArgs e)
		{
			// TODO about
		}

		private MessageBoxResult SaveBefore()
		{
			var result = MessageBoxResult.No;

			if (Graph.Current.Points.Count > 0)
			{
				var filename = Graph.Current.Filename != null ? new FileInfo(Graph.Current.Filename).Name : "work";
				
				result = MessageBox.Show($"Save { filename } ?", ApplicationName, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
				if (result == MessageBoxResult.Yes)
					Save();
			}

			return result;
		}

		private void Reset()
		{
			Graph.Current = new Graph();
			ColorPalette.Current = new ColorPalette();

			ColorPaletteWindow = null;

			MainCanvas.Children.Clear();
			MainCanvas.Background = Background;
			MainCanvas.Width = Width;
			MainCanvas.Height = Height;
			
			PointInProgress = null;
			EdgeInProgress = null;
			TriangleInProgress = null;

			HilightedPoint = null;
			SelectedPoints.Clear();

			PointMoving = false;
			CanvasMoving = false;

			ImageLoaded = null;
			ImageBrush = null;
			
			UpdateGraphInfoText();

			Title = ApplicationName;
		}

		private void EdgeThicknessTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			try
			{
				EdgeThickness = int.Parse(EdgeThicknessTextBox.Text);
			}
			catch (Exception)
			{
				EdgeThickness = 2;
				EdgeThicknessTextBox.Text = EdgeThickness.ToString();
			}

			foreach (var edge in Graph.Current.Edges)
				edge.Shape.StrokeThickness = EdgeThickness;
		}

		private void PointRadiusTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			try
			{
				PointRadius = int.Parse(PointRadiusTextBox.Text);
			}
			catch (Exception)
			{
				PointRadius = 4;
				PointRadiusTextBox.Text = PointRadius.ToString();
			}

			foreach (var point in Graph.Current.Points)
			{
				point.Shape.Width = PointRadius * 2;
				point.Shape.Height = PointRadius * 2;

				Canvas.SetLeft(point.Shape, point.Position.X - point.Shape.Width / 2);
				Canvas.SetTop(point.Shape, point.Position.Y - point.Shape.Height / 2);
			}
		}

		private void GridTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			// TODO change grid resolution ?
		}

		private void UpdateGraphInfoText()
		{
			if (Graph.Current.Points.Count == 0)
				StatusBarInfoText.Text = string.Empty;

			else
			{
				StatusBarInfoText.Text = string.Join(", ", new[]
				{
					$"  Points: { Graph.Current.Points.Count }",
					$"Edges: { Graph.Current.Edges.Count }",
					$"Triangles: { Graph.Current.Triangles.Count }",
					$"Colors: { ColorPalette.Current.Colors.Count }  "
				});
			}
		}

		private void SetStatus(string statusText)
		{
			StatusBarText.Text = "  " + statusText;
		}

		#endregion
	}
}
