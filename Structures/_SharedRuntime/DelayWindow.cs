public sealed record DelayWindow(int MinMs, int MaxMs)
{
	public int Mean => (MinMs + MaxMs) / 2;
}
