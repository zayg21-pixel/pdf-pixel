using PdfPixel.Imaging.Jpg.Readers;
using System;

namespace PdfPixel.Imaging.Jpg.Decoding;

/// <summary>
/// Handles JPEG restart marker processing: restart interval countdown, marker validation sequence (RST0-RST7),
/// and DC predictor reset. Fails fast with exceptions on structural errors expected by the JPEG spec.
/// </summary>
internal sealed class JpgRestartManager
{
    private readonly int _restartInterval;
    private int _restartsToGo;
    private byte _expectedRestart;
    private bool _seenFirstRestart;

    public JpgRestartManager(int restartInterval)
    {
        _restartInterval = restartInterval;
        _restartsToGo = restartInterval;
        _expectedRestart = 0xD0;
        _seenFirstRestart = false;
    }

    public bool IsRestartEnabled => _restartInterval > 0;
    public bool IsRestartNeeded => IsRestartEnabled && _restartsToGo == 0;

    /// <summary>
    /// Process a required restart marker when the MCU counter reaches zero.
    /// If no restart is currently needed the call is a no-op.
    /// Throws if the expected marker is absent or invalid when one is required.
    /// </summary>
    public void ProcessRestart(ref JpgBitReader bitReader, int[] previousDcValues)
    {
        if (!IsRestartNeeded)
        {
            return;
        }
        ProcessPendingRestart(ref bitReader, previousDcValues);
    }

    /// <summary>
    /// Decrement restart counter after each MCU in the entropy-coded segment.
    /// </summary>
    public void DecrementRestartCounter()
    {
        if (IsRestartEnabled)
        {
            _restartsToGo--;
        }
    }

    /// <summary>
    /// Reset predictors and restart counter at the beginning of a new progressive refinement pass.
    /// </summary>
    public void ResetForProgressivePass(int[] previousDcValues)
    {
        ResetPredictors(previousDcValues);
        _restartsToGo = _restartInterval;
    }

    private void ResetPredictors(int[] previousDcValues)
    {
        if (previousDcValues == null)
        {
            return;
        }
        for (int i = 0; i < previousDcValues.Length; i++)
        {
            previousDcValues[i] = 0;
        }
    }

    private void AdvanceExpectedRestart()
    {
        _expectedRestart = (byte)(0xD0 + (_expectedRestart - 0xD0 + 1 & 7));
    }

    /// <summary>
    /// Strict processing for a mandatory restart boundary.
    /// </summary>
    private void ProcessPendingRestart(ref JpgBitReader bitReader, int[] previousDcValues)
    {
        bitReader.ByteAlign();
        if (!bitReader.TryReadMarker(out byte marker))
        {
            throw new InvalidOperationException("Expected restart marker but none found.");
        }
        if (marker < 0xD0 || marker > 0xD7)
        {
            throw new InvalidOperationException($"Expected restart marker (RST0-RST7) but found 0x{marker:X2}.");
        }
        if (!_seenFirstRestart)
        {
            _expectedRestart = marker;
            _seenFirstRestart = true;
        }
        else if (marker != _expectedRestart)
        {
            throw new InvalidOperationException($"Restart marker sequence mismatch. Expected 0x{_expectedRestart:X2} got 0x{marker:X2}.");
        }
        AdvanceExpectedRestart();
        ResetPredictors(previousDcValues);
        _restartsToGo = _restartInterval;
    }
}