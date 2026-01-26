namespace MathQuizLocker.Models
{
	/// <summary>One transition graphic entry from transition_graphics.json: maps biome transitions to graphic assets.</summary>
	public class TransitionGraphicConfig
	{
		public string FromBiome { get; set; } = string.Empty;
		public string ToBiome { get; set; } = string.Empty;
		public string GraphicPath { get; set; } = string.Empty;
	}
}
