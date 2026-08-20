using HVACrate2.Core.Models;

namespace HVACrate2.Core.FloorHeating;

/// <summary>Floor heating heat-flow calculation for a single room, per the reference formulas.</summary>
public static class FloorHeatingCalculator
{
    private const double AlphaPod = 8.7;
    private const double AlphaTav = 8.7;

    private const double LambdaBet = 1.45;
    private const double LambdaZamPod = 0.93;
    private const double LambdaTer = 1.05;
    private const double LambdaIzol = 0.043;
    private const double LambdaPloch = 0.78;
    private const double LambdaZamTav = 0.87;

    private const double WaterHeatFactor = 41870;

    public static HeatingRoomResult Calculate(HeatingRoomInput input)
    {
        double rog = 1 / AlphaPod
            + input.DeltaBetM / LambdaBet
            + input.DeltaZamPodM / LambdaZamPod
            + input.DeltaTerM / LambdaTer;

        double rod = 1 / AlphaTav
            + input.DeltaBetM / LambdaBet
            + input.DeltaIzolM / LambdaIzol
            + input.DeltaPlochM / LambdaPloch
            + input.DeltaZamTavM / LambdaZamTav;

        double ro = rog + rod;
        double rPd = rod / ro;
        double qc = input.QptW / rPd;
        double mKgH = 3600 * qc / WaterHeatFactor;
        double qdol = qc - input.QptW;

        return new HeatingRoomResult
        {
            RogM2KW = rog,
            RodM2KW = rod,
            RoM2KW = ro,
            RPd = rPd,
            QcW = qc,
            MKgH = mKgH,
            QdolW = qdol,
        };
    }
}
