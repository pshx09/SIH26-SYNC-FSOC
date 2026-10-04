namespace FSOC.Contracts
{
    public interface IFrameSource
    {
        bool HasNextFrame { get; }
        SensorFrame GetNextFrame();
        
        int Width { get; }
        int Height { get; }
        float FrameRate { get; }
        InputMode CurrentMode { get; }
    }
}
