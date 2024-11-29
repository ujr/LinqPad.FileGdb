using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace FileGDB.Core;

public class WKTWriter : IDisposable
{
	private readonly TextWriter _writer;
	private readonly CultureInfo _invariant = CultureInfo.InvariantCulture;
	private State _state;
	private int _partIndex;
	private int _vertexIndex;
	private bool _newPolygon;

	public WKTWriter(TextWriter writer)
	{
		_writer = writer ?? throw new ArgumentNullException(nameof(writer));
		_state = State.Initial;
		DecimalDigits = -1;
	}

	public WKTWriter(StringBuilder sb)
		: this(new StringWriter(sb)) { }

	// TODO public int MaxLineLength { get; set; }

	/// <summary>
	/// How many decimal digits to emit for ordinates (X,Y,Z,M).
	/// A value x between 0 and 15 writes ordinates with .NET
	/// format string "Fx", all other values with format "G".
	/// Integer values always omit a decimal part, e.g., zero
	/// is written as "0" and not as "0.0".
	/// </summary>
	public int DecimalDigits { get; set; }

	/// <summary>
	/// Whether to emit point IDs. If false (the default)
	/// IDs are not written, even if ID values are provided.
	/// </summary>
	/// <remarks>Point IDs are not part of OGC WKT.</remarks>
	public bool EmitID { get; set; }

	public bool DefaultHasZ { get; set; }
	public bool DefaultHasM { get; set; }
	public bool DefaultHasID { get; set; }

	public bool CurrentHasZ { get; private set; }
	public bool CurrentHasM { get; private set; }
	public bool CurrentHasID { get; private set; }

	// Examples (from PostGIS.net):
	// POINT(0 0)
	// POINT Z (0 0 0)
	// POINT ZM (0 0 0 0)
	// POINT EMPTY
	// LINESTRING(0 0,1 1,1 2)
	// LINESTRING EMPTY
	// POLYGON((0 0,4 0,4 4,0 4,0 0),(1 1, 2 1, 2 2, 1 2,1 1))
	// MULTIPOINT((0 0),(1 2))
	// MULTIPOINT Z ((0 0 0),(1 2 3))
	// MULTIPOINT EMPTY
	// MULTILINESTRING((0 0,1 1,1 2),(2 3,3 2,5 4))
	// MULTIPOLYGON(((0 0,4 0,4 4,0 4,0 0),(1 1,2 1,2 2,1 2,1 1)), ((-1 -1,-1 -2,-2 -2,-2 -1,-1 -1)))

	public void BeginPoint(bool? hasZ = null, bool? hasM = null, bool? hasID = null)
	{
		BeginShape(State.Point, hasZ, hasM, hasID);
	}

	public void BeginMultipoint(bool? hasZ = null, bool? hasM = null, bool? hasID = null)
	{
		BeginShape(State.Multipoint, hasZ, hasM, hasID);
	}

	public void BeginLineString(bool? hasZ = null, bool? hasM = null, bool? hasID = null)
	{
		BeginShape(State.LineString, hasZ, hasM, hasID);
	}

	public void BeginMultiLineString(bool? hasZ = null, bool? hasM = null, bool? hasID = null)
	{
		BeginShape(State.MultiLineString, hasZ, hasM, hasID);
	}

	public void BeginPolygon(bool? hasZ = null, bool? hasM = null, bool? hasID = null)
	{
		BeginShape(State.Polygon, hasZ, hasM, hasID);
	}

	public void BeginMultiPolygon(bool? hasZ = null, bool? hasM = null, bool? hasID = null)
	{
		BeginShape(State.MultiPolygon, hasZ, hasM, hasID);
	}

	/// <summary>
	/// Start a new part in the current shape, which must be a multipart
	/// shape. For a MultiPolygon, this method starts a new inner ring;
	/// use <see cref="NewPolygon"/> to start a new outer ring.
	/// </summary>
	public void NewPart()
	{
		if (_state == State.Initial)
			throw InvalidOperation("No current shape");

		// NewPart right after BeginShape is a no-op and always allowed:
		if (_vertexIndex == 0 && _partIndex == 0) return;

		if (!IsMultiPart(_state))
			throw InvalidOperation("Current shape is not multi-part");

		if (_vertexIndex > 0)
		{
			_partIndex += 1;
		}

		_vertexIndex = 0;
	}

	/// <summary>
	/// Start a new polygon in the current shape, which must be a MultiPolygon
	/// </summary>
	/// <remarks>
	/// A WKT Polygon can have one outer (shell) ring and any number of inner
	/// (hole) rings; a MultiPolygon can have any number of Polygons.
	/// Esri Polygons are inherently "multi" (can have any number of outer
	/// rings) and whether a ring is outer or inner is not explicitly stored.
	/// </remarks>
	public void NewPolygon()
	{
		if (_state != State.MultiPolygon)
			throw InvalidOperation("Not writing a MultiPolygon");

		// NewPolygon right after BeginShape is a no-op and always allowed:
		if (_vertexIndex == 0 && _partIndex == 0) return;

		if (_vertexIndex > 0)
		{
			_partIndex += 1;
		}

		_newPolygon = true;
		_vertexIndex = 0;
	}

	public void AddVertex(double x, double y, double z = 0.0, double m = double.NaN, int id = 0)
	{
		if (_state == State.Initial)
			throw InvalidOperation("No current shape");
		if (_state == State.Point && _vertexIndex > 0)
			throw InvalidOperation("A point can only have one vertex");

		if (!CurrentHasZ && z is not (0.0 or double.NaN))
			throw new ArgumentException("Current shape is not Z aware", nameof(z));
		if (!CurrentHasM && !double.IsNaN(m))
			throw new ArgumentException("Current shape is not M aware", nameof(m));
		if (!CurrentHasID && id != 0)
			throw new ArgumentException("Current shape is not ID aware", nameof(id));

		if (_vertexIndex == 0)
		{
			if (_partIndex == 0)
			{
				if (_state == State.MultiPolygon)
				{
					_writer.Write(" (((");
				}
				else if (IsMultiPart(_state))
				{
					_writer.Write(" ((");
				}
				else
				{
					_writer.Write(" (");
				}
			}
			else
			{
				_writer.Write(_newPolygon ? ")), ((" : "), (");
			}
		}
		else if (_vertexIndex > 0)
		{
			// Multipoint is a special case
			_writer.Write(_state == State.Multipoint ? "), (" : ", ");
		}
		else
		{
			throw Bug($"Bug: {nameof(_vertexIndex)} must not be negative");
		}

		const string sep = " ";

		WriteOrdinate(x);
		_writer.Write(sep);
		WriteOrdinate(y);

		if (CurrentHasZ)
		{
			_writer.Write(sep);
			WriteOrdinate(z);
		}

		if (CurrentHasM)
		{
			_writer.Write(sep);
			WriteOrdinate(m);
		}

		if (CurrentHasID && EmitID)
		{
			_writer.Write(sep);
			_writer.Write(id);
		}

		_vertexIndex += 1;
		_newPolygon = false;
	}

	public void EndShape()
	{
		if (_state == State.Initial)
			throw InvalidOperation("No current shape");

		if (_vertexIndex == 0 && _partIndex == 0)
		{
			_writer.Write(" EMPTY");
		}
		else
		{
			if (_state == State.MultiPolygon)
			{
				_writer.Write(")");
			}

			if (IsMultiPart(_state))
			{
				_writer.Write(")");
			}

			_writer.Write(")");
		}

		_state = State.Initial;
		_partIndex = 0;
		_vertexIndex = 0;
		_newPolygon = false;
	}

	public void WritePoint(double x, double y, double z = 0.0, double m = double.NaN, int id = 0)
	{
		bool hasZ = DefaultHasZ || z != 0.0 && !double.IsNaN(z);
		bool hasM = DefaultHasM || !double.IsNaN(m);
		bool hasID = DefaultHasID || id != 0;

		BeginPoint(hasZ, hasM, hasID);
		AddVertex(x, y, z, m, id);
		EndShape();
	}

	/// <summary>
	/// Write WKT for a bounding box (aka envelope).
	/// This is a NON-CONFORMING extension to WKT!
	/// Syntax: "BOX (xmin ymin, xmax ymax)" or
	/// "BOX Z (xmin ymin zmin, xmax ymax zmax" or ...
	/// </summary>
	public void WriteBox(double xmin, double ymin, double xmax, double ymax,
		double zmin = double.NaN, double zmax = double.NaN,
		double mmin = double.NaN, double mmax = double.NaN)
	{
		bool isEmpty = double.IsNaN(xmin) || double.IsNaN(xmax) ||
		               double.IsNaN(ymin) || double.IsNaN(ymax) ||
		               xmin > xmax || ymin > ymax;

		bool hasZ = !double.IsNaN(zmin) && !double.IsNaN(zmax);
		bool hasM = !double.IsNaN(mmin) && !double.IsNaN(mmax);

		BeginShape(State.Box, hasZ, hasM, false);

		if (! isEmpty)
		{
			const string sep = " ";

			_writer.Write(" (");

			// minima

			WriteOrdinate(xmin);
			_writer.Write(sep);
			WriteOrdinate(ymin);

			if (hasZ)
			{
				_writer.Write(sep);
				WriteOrdinate(zmin);
			}

			if (hasM)
			{
				_writer.Write(sep);
				WriteOrdinate(mmin);
			}

			_writer.Write(", ");

			// maxima

			WriteOrdinate(xmax);
			_writer.Write(sep);
			WriteOrdinate(ymax);

			if (hasZ)
			{
				_writer.Write(sep);
				WriteOrdinate(zmax);
			}

			if (hasM)
			{
				_writer.Write(sep);
				WriteOrdinate(mmax);
			}

			_vertexIndex = 2; // to control EndShape
		}

		EndShape();
	}

	public void Flush()
	{
		_writer.Flush();
	}

	public void Dispose()
	{
		_writer.Flush();
		_writer.Dispose();
	}

	private void BeginShape(State state, bool? hasZ = null, bool? hasM = null, bool? hasID = null)
	{
		if (_state != State.Initial)
			throw InvalidOperation($"Must call {nameof(EndShape)} before beginning a new shape");

		CurrentHasZ = hasZ ?? DefaultHasZ;
		CurrentHasM = hasM ?? DefaultHasM;
		CurrentHasID = hasID ?? DefaultHasID;

		_writer.Write(GetTag(state));

		if (CurrentHasZ || CurrentHasM || CurrentHasID && EmitID)
		{
			_writer.Write(" ");
			_writer.Write(GetDim(CurrentHasZ, CurrentHasM, CurrentHasID && EmitID));
		}

		_state = state;
		_partIndex = 0;
		_vertexIndex = 0;
		_newPolygon = false;

		// NB: cannot write opening paren here as the shape may be empty!
	}

	private void WriteOrdinate(double value)
	{
		if (!double.IsFinite(value))
		{
			// NaN and positive/negative infinity
			_writer.Write("NaN");
		}
		else if (IsInteger(value))
		{
			// Use format "G" on integers to avoid zero decimals with "Fx"
			var text = value.ToString("G", _invariant);
			_writer.Write(text);
		}
		else
		{
			var format = GetOrdinateFormatString(DecimalDigits);
			var text = value.ToString(format, _invariant);
			_writer.Write(text);
		}
	}

	private static bool IsInteger(double value)
	{
		return Math.Abs(value % 1) < double.Epsilon;
	}

	private static string GetOrdinateFormatString(int decimalDigits)
	{
		switch (decimalDigits)
		{
			case 0: return "F0";
			case 1: return "F1";
			case 2: return "F2";
			case 3: return "F3";
			case 4: return "F4";
			case 5: return "F5";
			case 6: return "F6";
			case 7: return "F7";
			case 8: return "F8";
			case 9: return "F9";
			case 10: return "F10";
			case 11: return "F11";
			case 12: return "F12";
			case 13: return "F13";
			case 14: return "F14";
			case 15: return "F15";
		}

		return "G";
	}

	private static bool IsMultiPart(State state)
	{
		switch (state)
		{
			case State.Multipoint:
			case State.MultiLineString:
			case State.MultiPolygon:
				return true;
			case State.Polygon:
				return true; // we consider holes to be parts
			default:
				return false;
		}
	}

	private static string GetTag(State state)
	{
		switch (state)
		{
			case State.Point:
				return "POINT";
			case State.Multipoint:
				return "MULTIPOINT";
			case State.LineString:
				return "LINESTRING";
			case State.MultiLineString:
				return "MULTILINESTRING";
			case State.Polygon:
				return "POLYGON";
			case State.MultiPolygon:
				return "MULTIPOLYGON";
			case State.Box:
				return "BOX";
		}

		throw new ArgumentOutOfRangeException(nameof(state), state, "No WKT tag for this state");
	}

	private static string GetDim(bool hasZ, bool hasM, bool hasID)
	{
		int key = 0;

		if (hasZ) key += 1;
		if (hasM) key += 2;
		if (hasID) key += 4;

		switch (key)
		{
			case 1:
				return "Z";
			case 2:
				return "M";
			case 3:
				return "ZM";
			case 4:
				return "ID";
			case 5:
				return "ZID";
			case 6:
				return "MID";
			case 7:
				return "ZMID";
		}

		return string.Empty;
	}

	private InvalidOperationException InvalidOperation(string message)
	{
		return new InvalidOperationException(message);
	}

	private Exception Bug(string message)
	{
		return new InvalidOperationException(message ?? "Bug");
	}

	private enum State
	{
		Initial, Point, Multipoint, LineString, MultiLineString, Polygon, MultiPolygon, Box
	}
}
