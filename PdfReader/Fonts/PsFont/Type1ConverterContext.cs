using System.Collections.Generic;
using PdfReader.Models;

namespace PdfReader.Fonts.PsFont
{
    public struct Type1ConverterContext
    {
        public Dictionary<PdfString, byte[]> Source { get; set; }

        public Dictionary<int, byte[]> LocalSubrs { get; set; }

        public bool InFlexSequence { get; set; }

        public List<Type1CharStringNumber> FlexDeltas { get; set; }

        public bool SkipEndChar { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public double SideBearingX { get; set; }

        public double SideBearingY { get; set; }

        public double WidthX { get; set; }

        public double WidthY { get; set; }
    }
}
