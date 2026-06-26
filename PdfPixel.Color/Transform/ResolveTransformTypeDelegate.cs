using System;
#if !NETSTANDARD2_0
using System.Diagnostics.CodeAnalysis;
#endif

namespace PdfPixel.Color.Transform;

/// <summary>
/// Delegate that resolves an <see cref="IColorTransform"/> instance to its concrete
/// <see cref="Type"/> for AOT/trimmer-safe expression compilation.
/// </summary>
#if !NETSTANDARD2_0
[return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
#endif
public delegate Type? ResolveTransformTypeDelegate(IColorTransform transform);
