using Apache.Arrow;
using Apache.Arrow.C;

namespace ParquetRsForDotnet.Internal;

internal static unsafe class ArrowSchemaImporter
{
    public static Schema Import(CArrowSchema* nativeSchema)
    {
        return CArrowSchemaImporter.ImportSchema(nativeSchema);
    }
}
