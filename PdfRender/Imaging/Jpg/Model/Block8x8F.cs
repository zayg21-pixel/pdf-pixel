using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfRender.Imaging.Jpg.Model;

/// <summary>
/// Represents an 8x8 block of single-precision floating point samples used during JPEG decoding / IDCT processing.
/// Internally the block is laid out as 16 <see cref="Vector4"/> values (two vectors per logical row: left and right halves).
/// The explicit layout together with vector fields allows efficient SIMD operations across the block.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public partial struct Block8x8F
{
    private static readonly Vector4 Zero = Vector4.Zero;
    private static readonly Vector4 MaxByte = new Vector4(255f);

    /// <summary>
    /// Total number of scalar (float) coefficients held by a block (8 * 8).
    /// </summary>
    public const int Size = 64;

    /// <summary>
    /// Number of <see cref="Vector4"/> lanes stored in the block (16 * 4 = 64 scalars).
    /// </summary>
    public const int VectorCount = 16;

    // NOTE: The order of these fields (and their explicit offsets) is relied upon by Unsafe projections in methods.
    // Renaming for clarity (RowXLeft/RowXRight) does not change layout.

    [FieldOffset(0)]
    public Vector4 Row0Left;
    [FieldOffset(16)]
    public Vector4 Row0Right;

    [FieldOffset(32)]
    public Vector4 Row1Left;
    [FieldOffset(48)]
    public Vector4 Row1Right;

    [FieldOffset(64)]
    public Vector4 Row2Left;
    [FieldOffset(80)]
    public Vector4 Row2Right;

    [FieldOffset(96)]
    public Vector4 Row3Left;
    [FieldOffset(112)]
    public Vector4 Row3Right;

    [FieldOffset(128)]
    public Vector4 Row4Left;
    [FieldOffset(144)]
    public Vector4 Row4Right;

    [FieldOffset(160)]
    public Vector4 Row5Left;
    [FieldOffset(176)]
    public Vector4 Row5Right;

    [FieldOffset(192)]
    public Vector4 Row6Left;
    [FieldOffset(208)]
    public Vector4 Row6Right;

    [FieldOffset(224)]
    public Vector4 Row7Left;
    [FieldOffset(240)]
    public Vector4 Row7Right;

    /// <summary>
    /// Indexer over the 64 scalar coefficients in row-major order (row * 8 + column).
    /// </summary>
    /// <param name="index">Linear coefficient index in range [0, 63].</param>
    public float this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ref float selfRef = ref Unsafe.As<Block8x8F, float>(ref this);
            return Unsafe.Add(ref selfRef, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            ref float selfRef = ref Unsafe.As<Block8x8F, float>(ref this);
            Unsafe.Add(ref selfRef, index) = value;
        }
    }

    /// <summary>
    /// Gets a copy of the <see cref="Vector4"/> at the specified vector index (0..15).
    /// Two sequential vector indices represent a full logical row (Left then Right halves).
    /// </summary>
    /// <param name="vectorIndex">Index of the vector (0-based).</param>
    /// <returns>The vector value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 GetVector(int vectorIndex)
    {
        return Unsafe.Add(ref Unsafe.As<Block8x8F, Vector4>(ref this), vectorIndex);
    }

    /// <summary>
    /// Sets the <see cref="Vector4"/> value at the specified vector index (0..15).
    /// </summary>
    /// <param name="vectorIndex">Index of the vector (0-based).</param>
    /// <param name="value">Value to write.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetVector(int vectorIndex, in Vector4 value)
    {
        ref Vector4 vectorRef = ref Unsafe.Add(ref Unsafe.As<Block8x8F, Vector4>(ref this), vectorIndex);
        vectorRef = value;
    }

    /// <summary>
    /// In-place transpose of the 8x8 block (swap across the main diagonal).
    /// Only the upper triangular (excluding diagonal) elements are iterated; swaps are pairwise.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Transpose()
    {
        ref float elememntRef = ref Unsafe.As<Block8x8F, float>(ref this);

        // row #0
        Swap(ref Unsafe.Add(ref elememntRef, 1), ref Unsafe.Add(ref elememntRef, 8));
        Swap(ref Unsafe.Add(ref elememntRef, 2), ref Unsafe.Add(ref elememntRef, 16));
        Swap(ref Unsafe.Add(ref elememntRef, 3), ref Unsafe.Add(ref elememntRef, 24));
        Swap(ref Unsafe.Add(ref elememntRef, 4), ref Unsafe.Add(ref elememntRef, 32));
        Swap(ref Unsafe.Add(ref elememntRef, 5), ref Unsafe.Add(ref elememntRef, 40));
        Swap(ref Unsafe.Add(ref elememntRef, 6), ref Unsafe.Add(ref elememntRef, 48));
        Swap(ref Unsafe.Add(ref elememntRef, 7), ref Unsafe.Add(ref elememntRef, 56));

        // row #1
        Swap(ref Unsafe.Add(ref elememntRef, 10), ref Unsafe.Add(ref elememntRef, 17));
        Swap(ref Unsafe.Add(ref elememntRef, 11), ref Unsafe.Add(ref elememntRef, 25));
        Swap(ref Unsafe.Add(ref elememntRef, 12), ref Unsafe.Add(ref elememntRef, 33));
        Swap(ref Unsafe.Add(ref elememntRef, 13), ref Unsafe.Add(ref elememntRef, 41));
        Swap(ref Unsafe.Add(ref elememntRef, 14), ref Unsafe.Add(ref elememntRef, 49));
        Swap(ref Unsafe.Add(ref elememntRef, 15), ref Unsafe.Add(ref elememntRef, 57));

        // row #2
        Swap(ref Unsafe.Add(ref elememntRef, 19), ref Unsafe.Add(ref elememntRef, 26));
        Swap(ref Unsafe.Add(ref elememntRef, 20), ref Unsafe.Add(ref elememntRef, 34));
        Swap(ref Unsafe.Add(ref elememntRef, 21), ref Unsafe.Add(ref elememntRef, 42));
        Swap(ref Unsafe.Add(ref elememntRef, 22), ref Unsafe.Add(ref elememntRef, 50));
        Swap(ref Unsafe.Add(ref elememntRef, 23), ref Unsafe.Add(ref elememntRef, 58));

        // row #3
        Swap(ref Unsafe.Add(ref elememntRef, 28), ref Unsafe.Add(ref elememntRef, 35));
        Swap(ref Unsafe.Add(ref elememntRef, 29), ref Unsafe.Add(ref elememntRef, 43));
        Swap(ref Unsafe.Add(ref elememntRef, 30), ref Unsafe.Add(ref elememntRef, 51));
        Swap(ref Unsafe.Add(ref elememntRef, 31), ref Unsafe.Add(ref elememntRef, 59));

        // row #4
        Swap(ref Unsafe.Add(ref elememntRef, 37), ref Unsafe.Add(ref elememntRef, 44));
        Swap(ref Unsafe.Add(ref elememntRef, 38), ref Unsafe.Add(ref elememntRef, 52));
        Swap(ref Unsafe.Add(ref elememntRef, 39), ref Unsafe.Add(ref elememntRef, 60));

        // row #5
        Swap(ref Unsafe.Add(ref elememntRef, 46), ref Unsafe.Add(ref elememntRef, 53));
        Swap(ref Unsafe.Add(ref elememntRef, 47), ref Unsafe.Add(ref elememntRef, 61));

        // row #6
        Swap(ref Unsafe.Add(ref elememntRef, 55), ref Unsafe.Add(ref elememntRef, 62));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Swap<T>(ref T a, ref T b)
    {
        T tmp = a;
        a = b;
        b = tmp;
    }

    /// <summary>
    /// Adds the specified <paramref name="vector"/> to every <see cref="Vector4"/> lane in the block.
    /// </summary>
    /// <param name="vector">Value to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(Vector4 vector)
    {
        Row0Left += vector;
        Row0Right += vector;
        Row1Left += vector;
        Row1Right += vector;
        Row2Left += vector;
        Row2Right += vector;
        Row3Left += vector;
        Row3Right += vector;
        Row4Left += vector;
        Row4Right += vector;
        Row5Left += vector;
        Row5Right += vector;
        Row6Left += vector;
        Row6Right += vector;
        Row7Left += vector;
        Row7Right += vector;
    }

    /// <summary>
    /// Multiplies every <see cref="Vector4"/> lane in the block by <paramref name="vector"/> (component-wise).
    /// </summary>
    /// <param name="vector">Value to multiply by.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MultiplyBy(Vector4 vector)
    {
        Row0Left *= vector;
        Row0Right *= vector;
        Row1Left *= vector;
        Row1Right *= vector;
        Row2Left *= vector;
        Row2Right *= vector;
        Row3Left *= vector;
        Row3Right *= vector;
        Row4Left *= vector;
        Row4Right *= vector;
        Row5Left *= vector;
        Row5Right *= vector;
        Row6Left *= vector;
        Row6Right *= vector;
        Row7Left *= vector;
        Row7Right *= vector;
    }

    /// <summary>
    /// Component-wise multiplies every <see cref="Vector4"/> lane with the corresponding lane in another block.
    /// </summary>
    /// <param name="other">Block providing the per-lane multipliers.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MultiplyBy(in Block8x8F other)
    {
        Row0Left *= other.Row0Left;
        Row0Right *= other.Row0Right;
        Row1Left *= other.Row1Left;
        Row1Right *= other.Row1Right;
        Row2Left *= other.Row2Left;
        Row2Right *= other.Row2Right;
        Row3Left *= other.Row3Left;
        Row3Right *= other.Row3Right;
        Row4Left *= other.Row4Left;
        Row4Right *= other.Row4Right;
        Row5Left *= other.Row5Left;
        Row5Right *= other.Row5Right;
        Row6Left *= other.Row6Left;
        Row6Right *= other.Row6Right;
        Row7Left *= other.Row7Left;
        Row7Right *= other.Row7Right;
    }

    /// <summary>
    /// Clamps all scalar values in the block to the inclusive byte range [0, 255].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClampToByte()
    {
        Row0Left = Vector4.Clamp(Row0Left, Zero, MaxByte);
        Row0Right = Vector4.Clamp(Row0Right, Zero, MaxByte);
        Row1Left = Vector4.Clamp(Row1Left, Zero, MaxByte);
        Row1Right = Vector4.Clamp(Row1Right, Zero, MaxByte);
        Row2Left = Vector4.Clamp(Row2Left, Zero, MaxByte);
        Row2Right = Vector4.Clamp(Row2Right, Zero, MaxByte);
        Row3Left = Vector4.Clamp(Row3Left, Zero, MaxByte);
        Row3Right = Vector4.Clamp(Row3Right, Zero, MaxByte);
        Row4Left = Vector4.Clamp(Row4Left, Zero, MaxByte);
        Row4Right = Vector4.Clamp(Row4Right, Zero, MaxByte);
        Row5Left = Vector4.Clamp(Row5Left, Zero, MaxByte);
        Row5Right = Vector4.Clamp(Row5Right, Zero, MaxByte);
        Row6Left = Vector4.Clamp(Row6Left, Zero, MaxByte);
        Row6Right = Vector4.Clamp(Row6Right, Zero, MaxByte);
        Row7Left = Vector4.Clamp(Row7Left, Zero, MaxByte);
        Row7Right = Vector4.Clamp(Row7Right, Zero, MaxByte);
    }

    /// <summary>
    /// Fills the block with the specified scalar <paramref name="value"/> (broadcast to all components of every vector lane).
    /// </summary>
    /// <param name="value">Scalar value to assign (defaults to 0).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(float value = 0)
    {
        var vectorValue = new Vector4(value);
        Row0Left = vectorValue;
        Row0Right = vectorValue;
        Row1Left = vectorValue;
        Row1Right = vectorValue;
        Row2Left = vectorValue;
        Row2Right = vectorValue;
        Row3Left = vectorValue;
        Row3Right = vectorValue;
        Row4Left = vectorValue;
        Row4Right = vectorValue;
        Row5Left = vectorValue;
        Row5Right = vectorValue;
        Row6Left = vectorValue;
        Row6Right = vectorValue;
        Row7Left = vectorValue;
        Row7Right = vectorValue;
    }
}
