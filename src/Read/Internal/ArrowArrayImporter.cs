using Apache.Arrow;
using Apache.Arrow.C;
using Apache.Arrow.Types;

namespace ParquetRsForDotnet.Internal;

internal static unsafe class ArrowArrayImporter
{
    public static IArrowArray Import(CArrowArray* nativeArray, IArrowType arrowType)
    {
        ArgumentNullException.ThrowIfNull(arrowType);
        return CArrowArrayImporter.ImportArray(nativeArray, arrowType);
    }
}
