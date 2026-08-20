using HVACrate2.Core.FloorHeating;
using HVACrate2.Core.Models;

namespace HVACrate2.Core.Tests;

public class FloorHeatingCalculatorTests
{
    [Test]
    public async Task Calculate_WorkedExample_MatchesReferenceSheet()
    {
        var input = new HeatingRoomInput
        {
            DeltaBetM = 0.015,
            DeltaZamPodM = 0.01,
            DeltaTerM = 0.005,
            DeltaIzolM = 0.03,
            DeltaPlochM = 0.4,
            DeltaZamTavM = 0.01,
            QptW = 1000,
        };

        var result = FloorHeatingCalculator.Calculate(input);

        await Assert.That(result.RogM2KW).IsEqualTo(0.140802).Within(0.000005);
        await Assert.That(result.RodM2KW).IsEqualTo(1.347277).Within(0.000005);
        await Assert.That(result.RoM2KW).IsEqualTo(1.488079).Within(0.000005);
    }

    [Test]
    public async Task Calculate_RealReferenceTableRow_MatchesMAndQdol()
    {
        var input = new HeatingRoomInput
        {
            DeltaBetM = 0.015,
            DeltaZamPodM = 0.01,
            DeltaTerM = 0.005,
            DeltaIzolM = 0.03,
            DeltaPlochM = 0.4,
            DeltaZamTavM = 0.01,
            QptW = 1860,
        };

        var result = FloorHeatingCalculator.Calculate(input);

        await Assert.That(result.RPd).IsEqualTo(0.9054).Within(0.001);
        await Assert.That(result.QcW).IsEqualTo(2054.4).Within(1.0);
        await Assert.That(result.MKgH).IsEqualTo(176.64).Within(0.5);
        await Assert.That(result.QdolW).IsEqualTo(194.4).Within(1.0);
    }

    [Test]
    public async Task Calculate_ZeroDeltas_StillProducesFiniteResistances()
    {
        var input = new HeatingRoomInput
        {
            DeltaBetM = 0,
            DeltaZamPodM = 0,
            DeltaTerM = 0,
            DeltaIzolM = 0,
            DeltaPlochM = 0,
            DeltaZamTavM = 0,
            QptW = 500,
        };

        var result = FloorHeatingCalculator.Calculate(input);

        await Assert.That(result.RogM2KW).IsEqualTo(1.0 / 8.7).Within(1e-9);
        await Assert.That(result.RodM2KW).IsEqualTo(1.0 / 8.7).Within(1e-9);
        await Assert.That(result.RoM2KW).IsEqualTo(result.RogM2KW + result.RodM2KW).Within(1e-9);
    }

    [Test]
    public async Task Calculate_QdolCanBeNegative_WhenQcIsLessThanQpt()
    {
        var input = new HeatingRoomInput
        {
            DeltaBetM = 0.015,
            DeltaZamPodM = 0.01,
            DeltaTerM = 0.005,
            DeltaIzolM = 0.03,
            DeltaPlochM = 0.4,
            DeltaZamTavM = 0.01,
            QptW = 100,
        };

        var result = FloorHeatingCalculator.Calculate(input);

        await Assert.That(result.QdolW).IsEqualTo(result.QcW - input.QptW).Within(1e-9);
    }

    [Test]
    public async Task Calculate_MassFlow_UsesFixedWaterHeatFactor()
    {
        var input = new HeatingRoomInput
        {
            DeltaBetM = 0.015,
            DeltaZamPodM = 0.01,
            DeltaTerM = 0.005,
            DeltaIzolM = 0.03,
            DeltaPlochM = 0.4,
            DeltaZamTavM = 0.01,
            QptW = 1000,
        };

        var result = FloorHeatingCalculator.Calculate(input);

        await Assert.That(result.MKgH).IsEqualTo(3600 * result.QcW / 41870).Within(1e-9);
    }
}
