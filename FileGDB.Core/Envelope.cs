namespace FileGDB.Core;

public class Envelope
{
	public double XMin { get; set; }
	public double YMin { get; set; }
	public double XMax { get; set; }
	public double YMax { get; set; }

	public bool HasZ { get; set; }
	public double ZMin { get; set; }
	public double ZMax { get; set; }

	public bool HasM { get; set; }
	public double MMin { get; set; }
	public double MMax { get; set; }

	public Envelope()
	{
		XMin = XMax = double.NaN;
		YMin = YMax = double.NaN;

		ZMin = ZMax = double.NaN;
		MMin = MMax = double.NaN;
	}
}
