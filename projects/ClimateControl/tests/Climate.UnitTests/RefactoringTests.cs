using Climate.After;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Climate.UnitTests;
public sealed class RefactoringTests
{
    [Theory]
    [InlineData(-40, 0, true)]
    [InlineData(85, 100, true)]
    [InlineData(100, 45, false)]
    [InlineData(23.5, 250, false)]
    public void R01_RefactoredControllerEnforcesRange(double t, double h, bool accepted)
    {
        var store = new ReadingStore();
        var controller = new RefactoredClimateController(new ReadingService(store, new DiagnosticLog()));
        var response = controller.Send("DVC-001", (decimal)t, (decimal)h, 1);
        (response is OkResult).Should().Be(accepted);
        store.Count.Should().Be(accepted ? 1 : 0);
    }
}
