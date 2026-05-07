using Apache.Arrow;
using Apache.Arrow.C;

namespace ParquetRsForDotnet.Internal;

/// <summary>
/// Owns the unmanaged Arrow C Data wrapper used to expose one managed schema to native code.
/// </summary>
internal sealed unsafe class ArrowSchemaExporter : IDisposable
{
    private readonly CArrowSchema* _nativeSchema;

    public ArrowSchemaExporter(Schema schema)
    {
        TargetFrameworkCompat.ThrowIfNull(schema);

        _nativeSchema = CArrowSchema.Create();
        CArrowSchemaExporter.ExportSchema(schema, _nativeSchema);
    }

    public CArrowSchema* NativeSchema => _nativeSchema;

    public void Dispose()
    {
        if (_nativeSchema != null)
        {
            CArrowSchema.Free(_nativeSchema);
        }
    }
}
