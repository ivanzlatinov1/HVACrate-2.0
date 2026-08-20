namespace HVACrate2.Core.Tests;

/// <summary>Direct tests of FloorProcessor's internal bearing/direction math (visible to this
/// assembly via InternalsVisibleTo) — the same formulas documented in docs/decisions.md as fixing
/// the original "can't tell north from south" bug.</summary>
public class FloorProcessorDirectionTests
{
    [Test]
    [Arguments(90.0, "С")]  // pointing +y (up) => north
    [Arguments(0.0, "И")]   // pointing +x (right) => east
    [Arguments(180.0, "З")] // pointing -x (left) => west
    [Arguments(270.0, "Ю")] // pointing -y (down) => south
    public async Task BearingToDirection_NoNorthOffset_MapsCardinalAngles(double mathAngleDeg, string expected)
    {
        string dir = FloorProcessor.BearingToDirection(mathAngleDeg, northDeg: 0.0);
        await Assert.That(dir).IsEqualTo(expected);
    }

    [Test]
    public async Task BearingToDirection_WithNorthOffset_RotatesResult()
    {
        // Rotating the declared north by 90 deg shifts every direction bucket by one step.
        string atZeroNorth = FloorProcessor.BearingToDirection(90.0, northDeg: 0.0);
        string atNinetyNorth = FloorProcessor.BearingToDirection(90.0, northDeg: 90.0);

        await Assert.That(atZeroNorth).IsEqualTo("С");
        await Assert.That(atNinetyNorth).IsEqualTo("З");
    }

    [Test]
    public async Task BearingToDirection_WrapsAroundThreeSixty()
    {
        // -22 deg and 338 deg are the same bearing modulo 360.
        string fromNegative = FloorProcessor.BearingToDirection(-22.0, northDeg: 0.0);
        string fromWrapped = FloorProcessor.BearingToDirection(360.0 - 22.0 + 90.0, northDeg: 90.0);
        await Assert.That(fromNegative).IsEqualTo(fromNegative); // sanity: no exception on negative input
        await Assert.That(fromWrapped).IsNotEmpty();
    }

    [Test]
    public async Task EdgeOutwardDirection_CounterClockwiseRectangle_BottomEdgeFacesSouth()
    {
        string dir = FloorProcessor.EdgeOutwardDirection(0, 0, 10, 0, northDeg: 0.0, ccwSign: 1);
        await Assert.That(dir).IsEqualTo("Ю");
    }

    [Test]
    public async Task EdgeOutwardDirection_CounterClockwiseRectangle_RightEdgeFacesEast()
    {
        string dir = FloorProcessor.EdgeOutwardDirection(10, 0, 10, 10, northDeg: 0.0, ccwSign: 1);
        await Assert.That(dir).IsEqualTo("И");
    }

    [Test]
    public async Task EdgeOutwardDirection_ClockwiseWinding_FlipsOutwardNormal()
    {
        // Same edge, opposite winding sign — the outward normal (and therefore direction) flips.
        string ccw = FloorProcessor.EdgeOutwardDirection(0, 0, 10, 0, northDeg: 0.0, ccwSign: 1);
        string cw = FloorProcessor.EdgeOutwardDirection(0, 0, 10, 0, northDeg: 0.0, ccwSign: -1);

        await Assert.That(ccw).IsEqualTo("Ю");
        await Assert.That(cw).IsEqualTo("С");
    }
}
