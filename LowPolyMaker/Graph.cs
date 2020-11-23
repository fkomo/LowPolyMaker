using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml;

namespace LowPolyMaker
{
	// TODO "explode" triangle mesh - increase vertex radius and move triangles away from it

	public struct BoundingBox
	{
		public Point TopLeft;
		public Point BottomRight;
	}

	[Serializable]
	public class GraphPoint
	{
		public int Id = -1;

		[NonSerialized]
		public Ellipse Shape;

		public Point Position;

		public GraphPoint(bool generateId = true)
		{
			if (generateId)
				Id = Graph.Current.GetNextId();
		}
	}

	[Serializable]
	public class GraphEdge
	{
		[NonSerialized]
		public Line Shape;
		[NonSerialized]
		public Ellipse SnapToShape;

		// NOTE don't modify! used for serialization
		public int StartPointId;
		public int EndPointId;

		[NonSerialized]
		private GraphPoint start;
		[NonSerialized]
		private GraphPoint end;

		public GraphPoint Start
		{
			get { return start; }
			set { start = value; StartPointId = start.Id; }
		}

		public GraphPoint End
		{
			get { return end; }
			set { end = value; EndPointId = end.Id; }
		}
	}

	[Serializable]
	public class GraphTriangle
	{
		[NonSerialized]
		public Polygon Shape;

		// NOTE don't modify! used for serialization
		public int Point1Id;
		public int Point2Id;
		public int Point3Id;

		[NonSerialized]
		private GraphPoint point1;
		[NonSerialized]
		private GraphPoint point2;
		[NonSerialized]
		private GraphPoint point3;

		public GraphPoint Point1
		{
			get { return point1; }
			set { point1 = value; Point1Id = point1.Id; }
		}

		public GraphPoint Point2
		{
			get { return point2; }
			set { point2 = value; Point2Id = point2.Id; }
		}

		public GraphPoint Point3
		{
			get { return point3; }
			set { point3 = value; Point3Id = point3.Id; }
		}

		public BoundingBox GetBoundingBox()
		{
			return new BoundingBox
			{
				TopLeft = new Point(
					Math.Min(Math.Min(point1.Position.X, point2.Position.X), point3.Position.X),
					Math.Min(Math.Min(point1.Position.Y, point2.Position.Y), point3.Position.Y)),
				BottomRight = new Point(
					Math.Max(Math.Max(point1.Position.X, point2.Position.X), point3.Position.X),
					Math.Max(Math.Max(point1.Position.Y, point2.Position.Y), point3.Position.Y)),
			};
		}

		public bool IsPointInside(Point p)
		{
			var b2 = Sign(p, point2.Position, point3.Position) < 0;
			return ((Sign(p, point1.Position, point2.Position) < 0 == b2) && (b2 == Sign(p, point3.Position, point1.Position) < 0));
		}

		private double Sign(Point p1, Point p2, Point p3)
		{
			return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
		}
	}

	[Serializable]
	public class Graph
	{
		public const string FileExtension = ".lpm";
		public const string SvgExtension = ".svg";

		public static Graph Current { get; set; } = new Graph();

		public int NextId = 0;
		public int GetNextId()
		{
			return NextId++;
		}

		[NonSerialized]
		public string Filename;

		public string ImageFilename { get; set; }

		public List<GraphPoint> Points { get; set; } = new List<GraphPoint>();
		public List<GraphEdge> Edges { get; set; } = new List<GraphEdge>();
		public List<GraphTriangle> Triangles { get; set; } = new List<GraphTriangle>();

		public static string Serialize(Graph obj)
		{
			var serializer = new DataContractSerializer(obj.GetType());
			using (var writer = new StringWriter())
				using (var stm = new XmlTextWriter(writer))
				{
					serializer.WriteObject(stm, obj);
					return writer.ToString();
				}
		}

		public static Graph Deserialize(string serialized)
		{
			var serializer = new DataContractSerializer(typeof(Graph));
			using (var reader = new StringReader(serialized))
				using (var stm = new XmlTextReader(reader))
					return serializer.ReadObject(stm) as Graph;
		}

		public void ApplyColorPalette(ColorPalette colorPalette)
		{
			foreach (var triangle in Triangles)
			{
				triangle.Shape.Fill = null;
				triangle.Shape.Fill = new SolidColorBrush(colorPalette.GetTriangleColor(triangle, true));
			}
		}

		internal static string SerializeAsSvg(Graph graph)
		{
			var strBuilder = new StringBuilder();
			strBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>");

			var min = new Point(graph.Points.Min(p => p.Position.X), graph.Points.Min(p => p.Position.Y));
			var max = new Point(graph.Points.Max(p => p.Position.X), graph.Points.Max(p => p.Position.Y));
			strBuilder.AppendLine($"<svg width=\"{ max.X - min.X }\" height=\"{ max.Y - min.Y }\" xmlns=\"http://www.w3.org/2000/svg\">");

			foreach (var t in graph.Triangles)
			{
				var p1 = t.Point1.Position - min;
				var p2 = t.Point2.Position - min;
				var p3 = t.Point3.Position - min;

				var fillColor = t.Shape.Fill as SolidColorBrush;
				var fill = $"#{ fillColor.Color.R:x2}{ fillColor.Color.G:x2}{ fillColor.Color.B:x2}";
				
				var stroke = "#000000";
				var strokeOpacity = 0;

				strBuilder.AppendLine($"<polygon fill=\"{ fill }\" stroke-opacity=\"{ strokeOpacity }\" stroke=\"{ stroke }\" points=\"{ (int)p1.X },{ (int)p1.Y } { (int)p2.X },{ (int)p2.Y } { (int)p3.X },{ (int)p3.Y }\" class=\"triangle\" />");
			}

			strBuilder.AppendLine("</svg>");

			return strBuilder.ToString();
		}
	}
}
