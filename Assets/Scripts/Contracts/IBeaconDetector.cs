namespace FSOC.Contracts
{
    public interface IBeaconDetector
    {
        DetectionResult ProcessFrame(SensorFrame frame);
    }
}
