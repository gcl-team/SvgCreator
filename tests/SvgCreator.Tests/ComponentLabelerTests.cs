using SvgCreator.Tracing;

namespace SvgCreator.Tests;

public class ComponentLabelerTests
{
    [Fact]
    public void Label_SeparateBlobs_GetDistinctLabels()
    {
        var mask = TestImages.Mask(
            "##...",
            "##...",
            "...##",
            "...##");

        var map = ComponentLabeler.Label(mask, minArea: 0);

        Assert.Equal(2, map.Count);
        Assert.Equal([4, 4], map.Areas[1..]);
    }

    [Fact]
    public void Label_DiagonalNeighbours_AreOneComponent()
    {
        var mask = TestImages.Mask(
            "#..",
            ".#.",
            "..#");

        var map = ComponentLabeler.Label(mask, minArea: 0);

        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void Label_UShape_MergesBranchesIntoOneComponent()
    {
        var mask = TestImages.Mask(
            "#...#",
            "#...#",
            "#####");

        var map = ComponentLabeler.Label(mask, minArea: 0);

        Assert.Equal(1, map.Count);
        Assert.Equal(9, map.Areas[1]);
    }

    [Fact]
    public void Label_RemovesComponentsSmallerThanMinArea()
    {
        var mask = TestImages.Mask(
            "###....",
            "###...#",
            "###....");

        var map = ComponentLabeler.Label(mask, minArea: 2);

        Assert.Equal(1, map.Count);
        Assert.False(map.IsInk(6, 1));
        Assert.True(map.IsInk(0, 0));
    }
}
