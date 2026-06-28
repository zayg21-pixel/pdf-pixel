using System.Reflection;
var asm = Assembly.LoadFrom(@"bin/temp/SkiaSharp.dll");
var pictureType = asm.GetType("SkiaSharp.SKPicture");
foreach (var m in pictureType.GetMembers(BindingFlags.Public | BindingFlags.Instance))
    Console.WriteLine($"  {m.MemberType}: {m.Name}");
var grContextType = asm.GetType("SkiaSharp.GRContext");
Console.WriteLine("--- GRContext ---");
foreach (var m in grContextType.GetMembers(BindingFlags.Public | BindingFlags.Instance))
    Console.WriteLine($"  {m.MemberType}: {m.Name}");
