using System;
using Benchmarks;

namespace TestProgram
{
    class TestUniversalBernstein
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Testing Universal Bernstein Estimator");
            Console.WriteLine("====================================");

            // Test different powers and degrees
            float[] testPowers = { 0.5f, 1.0f, 2.0f, 2.2f, 3.0f };
            int[] testDegrees = { 2, 3, 4, 6, 8 };

            foreach (float power in testPowers)
            {
                Console.WriteLine($"\nTesting power = {power}:");
                
                foreach (int degree in testDegrees)
                {
                    try
                    {
                        var poly = PowerEstimator.EstimateBernsteinUniversal(power, degree);
                        
                        // Test boundary conditions
                        float p0 = poly.Evaluate(0.0f);
                        float p1 = poly.Evaluate(1.0f);
                        
                        // Test some intermediate points
                        float p025 = poly.Evaluate(0.25f);
                        float p05 = poly.Evaluate(0.5f);
                        float p075 = poly.Evaluate(0.75f);
                        
                        // Expected values
                        float expected025 = (float)Math.Pow(0.25, power);
                        float expected05 = (float)Math.Pow(0.5, power);
                        float expected075 = (float)Math.Pow(0.75, power);
                        
                        // Calculate errors
                        float error025 = Math.Abs(p025 - expected025);
                        float error05 = Math.Abs(p05 - expected05);
                        float error075 = Math.Abs(p075 - expected075);
                        float maxError = Math.Max(error025, Math.Max(error05, error075));
                        
                        Console.WriteLine($"  Degree {degree,2}: P(0)={p0:F6}, P(1)={p1:F6}, MaxError={maxError:E3}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Degree {degree,2}: ERROR - {ex.Message}");
                    }
                }
                
                // Compare with the existing degree-4 implementation
                if (testDegrees.Contains(4))
                {
                    var poly4Original = PowerEstimator.EstimateBernsteinDegree4(power);
                    var poly4Universal = PowerEstimator.EstimateBernsteinUniversal(power, 4);
                    
                    Console.WriteLine($"  Degree 4 comparison:");
                    Console.WriteLine($"    Original: [{poly4Original.C0:F6}, {poly4Original.C1:F6}, {poly4Original.C2:F6}, {poly4Original.C3:F6}, {poly4Original.C4:F6}]");
                    Console.WriteLine($"    Universal: [{string.Join(", ", Array.ConvertAll(poly4Universal.Coefficients, x => x.ToString("F6")))}]");
                    
                    // Test if they produce similar results
                    float diff025 = Math.Abs(poly4Original.Evaluate(0.25f) - poly4Universal.Evaluate(0.25f));
                    float diff05 = Math.Abs(poly4Original.Evaluate(0.5f) - poly4Universal.Evaluate(0.5f));
                    float diff075 = Math.Abs(poly4Original.Evaluate(0.75f) - poly4Universal.Evaluate(0.75f));
                    float maxDiff = Math.Max(diff025, Math.Max(diff05, diff075));
                    Console.WriteLine($"    Max difference: {maxDiff:E3}");
                }
            }

            Console.WriteLine("\nTest completed!");
        }
    }
}