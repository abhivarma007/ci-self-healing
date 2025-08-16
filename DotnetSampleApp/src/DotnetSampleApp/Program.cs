namespace DotnetSampleApp;

public class Calculator
{
    public int Add(int a, int b) => a + b;
    public int Subtract(int a, int b) => a - b;
}

class Program
{
    static void Main()
    {
        var calc = new Calculator();
        Console.WriteLine($"2 + 3 = {calc.Add(2,3)}");
    }
}
