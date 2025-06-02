namespace FileGDB.Core;

public class Envelope : BoundingBox
{
	public bool HasZ { get; set; }
	public double ZMin { get; set; }
	public double ZMax { get; set; }

	public bool HasM { get; set; }
	public double MMin { get; set; }
	public double MMax { get; set; }

	public Envelope()
	{
		ZMin = ZMax = double.NaN;
		MMin = MMax = double.NaN;
	}
}
