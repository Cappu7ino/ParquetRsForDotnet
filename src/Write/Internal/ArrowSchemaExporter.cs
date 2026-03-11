using Apache.Arrow;
using Apache.Arrow.C;

namespace ParquetRsForDotnet.Internal;

/// <summary>
/// Owns the unmanaged Arrow C Data wrapper used to expose one managed schema to native code.
/// </summary>
internal sealed unsafe class ArrowSchemaExporter : IDisposable
{
    private readonly CArrowSchema* nativeSchema;

    public ArrowSchemaExporter(Schema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        nativeSchema = CArrowSchema.Create();
        CArrowSchemaExporter.ExportSchema(schema, nativeSchema);
    }

    public CArrowSchema* NativeSchema => nativeSchema;

    public void Dispose()
    {
        if (nativeSchema != null)
        {
            CArrowSchema.Free(nativeSchema);
        }
    }
}
