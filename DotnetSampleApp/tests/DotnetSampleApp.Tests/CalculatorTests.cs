using Xunit;
using DotnetSampleApp;

namespace DotnetSampleApp.Tests;

public class CalculatorTests
{
    [Fact]
    public void Add_TwoNumbers_ReturnsSum()
    {
        var calc = new Calculator();
        Assert.Equal(5, calc.Add(2, 3));
    }

    [Fact]
    public void Subtract_TwoNumbers_ReturnsDifference()
    {
        var calc = new Calculator();
        Assert.Equal(1, calc.Subtract(3, 2));
    }
}
