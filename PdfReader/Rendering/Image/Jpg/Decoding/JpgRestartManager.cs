using PdfReader.Rendering.Image.Jpg.Readers;
using System;

namespace PdfReader.Rendering.Image.Jpg.Decoding
{
    /// <summary>
    /// Handles JPEG restart marker processing and state management.
    /// Manages restart intervals, marker validation, and DC predictor reset.
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

        public bool ProcessRestart(ref JpgBitReader bitReader, int[] previousDcValues)
        {
            if (!IsRestartNeeded)
            {
                return true;
            }

            return ProcessPendingRestart(ref bitReader, previousDcValues);
        }

        /// <summary>
        /// If a restart marker is pending in the bitstream, consume and process it regardless of counter.
        /// Returns true if a restart was processed, false if no marker was present.
        /// </summary>
        public bool TryProcessPendingRestart(ref JpgBitReader bitReader, int[] previousDcValues)
        {
            if (!IsRestartEnabled)
            {
                return false;
            }

            // Try to read a marker at current byte boundary
            bitReader.ByteAlign();
            if (!bitReader.TryReadMarker(out var marker))
            {
                return false;
            }

            if (marker < 0xD0 || marker > 0xD7)
            {
                // Not a restart marker; put no state changes (we cannot unread). Treat as no-op.
                return false;
            }

            // Validate expected sequence if we've seen one already
            if (!_seenFirstRestart)
            {
                _expectedRestart = marker;
                _seenFirstRestart = true;
            }
            else if (marker != _expectedRestart)
            {
                Console.Error.WriteLine($"[PdfReader][JPEG] Restart sequence mismatch (expected 0x{_expectedRestart:X2}, got 0x{marker:X2})");
                // Continue anyway to avoid deadlock
                _expectedRestart = marker;
            }

            // Advance expected for next time
            _expectedRestart = (byte)(0xD0 + (_expectedRestart - 0xD0 + 1 & 7));

            // Reset DC predictors
            if (previousDcValues != null)
            {
                for (int i = 0; i < previousDcValues.Length; i++)
                {
                    previousDcValues[i] = 0;
                }
            }

            // Reset counter
            _restartsToGo = _restartInterval;
            return true;
        }

        public void DecrementRestartCounter()
        {
            if (IsRestartEnabled)
            {
                _restartsToGo--;
            }
        }

        public void ResetForProgressivePass(int[] previousDcValues)
        {
            if (previousDcValues != null)
            {
                for (int i = 0; i < previousDcValues.Length; i++)
                {
                    previousDcValues[i] = 0;
                }
            }

            _restartsToGo = _restartInterval;
        }

        private bool ProcessPendingRestart(ref JpgBitReader bitReader, int[] previousDcValues)
        {
            bitReader.ByteAlign();
            if (!bitReader.TryReadMarker(out var marker))
            {
                Console.Error.WriteLine("[PdfReader][JPEG] Expected restart marker but none found");
                return false;
            }

            if (marker < 0xD0 || marker > 0xD7)
            {
                Console.Error.WriteLine($"[PdfReader][JPEG] Expected restart marker (RST0-RST7), got 0x{marker:X2}");
                return false;
            }

            if (!_seenFirstRestart)
            {
                _expectedRestart = marker;
                _seenFirstRestart = true;
            }
            else if (marker != _expectedRestart)
            {
                Console.Error.WriteLine($"[PdfReader][JPEG] Restart sequence mismatch (expected 0x{_expectedRestart:X2}, got 0x{marker:X2})");
                return false;
            }

            _expectedRestart = (byte)(0xD0 + (_expectedRestart - 0xD0 + 1 & 7));

            if (previousDcValues != null)
            {
                for (int i = 0; i < previousDcValues.Length; i++)
                {
                    previousDcValues[i] = 0;
                }
            }

            _restartsToGo = _restartInterval;
            return true;
        }
    }
}