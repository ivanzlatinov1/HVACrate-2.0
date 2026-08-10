using HVACrate2.Core.Models;

namespace HVACrate2.Core.FloorHeating;

/// <summary>Floor heating heat-flow calculation for a single room, per the reference formulas.</summary>
public static class FloorHeatingCalculator
{
    // Heat-transfer coefficients [W/m2K]
    private const double AlphaPod = 8.7; // от пода (from the floor)
    private const double AlphaTav = 8.7; // от тавана (from the ceiling)

    // Thermal conductivities [W/m.K]
    private const double LambdaBet = 1.45;    // заливка на тръбите (pipe screed)
    private const double LambdaZamPod = 0.93; // изравнителна замазка (floor leveling screed)
    private const double LambdaTer = 1.05;    // теракота (tile)
    private const double LambdaIzol = 0.043;  // топлоизолация / стиропор (insulation)
    private const double LambdaPloch = 0.78;  // плоча (slab)
    private const double LambdaZamTav = 0.87; // замазка на тавана (ceiling screed)

    private const double WaterHeatFactor = 41870; // 3600 * Qc / WaterHeatFactor => mass flow in kg/h

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
