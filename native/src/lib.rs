use std::ffi::{c_char, c_void, CStr, CString};
use std::io::{self, Write};
use std::panic::{self, AssertUnwindSafe};
use std::ptr;
use std::sync::Arc;

use arrow::array::StructArray;
use arrow::ffi::{from_ffi, FFI_ArrowArray};
use arrow_array::RecordBatch;
use arrow_schema::ffi::FFI_ArrowSchema;
use arrow_schema::Schema;
use parquet::arrow::arrow_writer::ArrowWriter;
use parquet::basic::Compression;
use parquet::file::metadata::KeyValue;
use parquet::file::properties::{EnabledStatistics, WriterProperties};

const SUCCESS: i32 = 0;
const INVALID_ARGUMENT: i32 = 1;
const ARROW_IMPORT_FAILED: i32 = 5;
const SINK_WRITE_FAILED: i32 = 6;
const PARQUET_ENCODE_FAILED: i32 = 7;
const INTERNAL_PANIC: i32 = 8;
const DEFAULT_MAX_ROW_GROUP_ROW_COUNT: usize = 8 * 1024;

struct FileWriterHandle {
    writer: ArrowWriter<SinkWriter>,
    schema: Arc<Schema>,
    finished: bool,
}

#[repr(C)]
pub struct ParquetWriteOptionsFFI {
    pub compression: i32,
    pub enable_dictionary_encoding: i32,
    pub statistics_level: i32,
    pub native_write_batch_size: i32,
    pub max_row_group_rows: i64,
    pub max_row_group_bytes: i64,
    pub created_by: *const c_char,
    pub metadata: *const NativeKeyValuePair,
    pub metadata_count: i32,
}

#[repr(C)]
pub struct NativeKeyValuePair {
    pub key: *const c_char,
    pub value: *const c_char,
}

#[repr(C)]
pub struct NativeError {
    pub code: i32,
    pub message: *mut c_char,
}

#[repr(C)]
pub struct ParquetOutputSink {
    pub write: Option<unsafe extern "C" fn(*mut c_void, *const u8, usize, *mut usize) -> i32>,
    pub flush: Option<unsafe extern "C" fn(*mut c_void) -> i32>,
    pub close: Option<unsafe extern "C" fn(*mut c_void) -> i32>,
    pub abort: Option<unsafe extern "C" fn(*mut c_void) -> i32>,
    pub get_last_error: Option<unsafe extern "C" fn(*mut c_void) -> *const c_char>,
    pub context: *mut c_void,
}

#[derive(Debug)]
struct FfiFailure {
    code: i32,
    message: String,
}

impl FfiFailure {
    fn new(code: i32, message: impl Into<String>) -> Self {
        Self {
            code,
            message: message.into(),
        }
    }
}

struct SinkWriter {
    write_callback: unsafe extern "C" fn(*mut c_void, *const u8, usize, *mut usize) -> i32,
    flush_callback: unsafe extern "C" fn(*mut c_void) -> i32,
    close_callback: unsafe extern "C" fn(*mut c_void) -> i32,
    get_last_error_callback: Option<unsafe extern "C" fn(*mut c_void) -> *const c_char>,
    context: usize,
    failed: bool,
}

impl SinkWriter {
    fn new(sink: &ParquetOutputSink) -> Result<Self, FfiFailure> {
        let write_callback = sink.write.ok_or_else(|| {
            FfiFailure::new(INVALID_ARGUMENT, "Sink callback table is incomplete.")
        })?;
        let flush_callback = sink.flush.ok_or_else(|| {
            FfiFailure::new(INVALID_ARGUMENT, "Sink callback table is incomplete.")
        })?;
        let close_callback = sink.close.ok_or_else(|| {
            FfiFailure::new(INVALID_ARGUMENT, "Sink callback table is incomplete.")
        })?;
        let _abort_callback = sink.abort.ok_or_else(|| {
            FfiFailure::new(INVALID_ARGUMENT, "Sink callback table is incomplete.")
        })?;

        Ok(Self {
            write_callback,
            flush_callback,
            close_callback,
            get_last_error_callback: sink.get_last_error,
            context: sink.context as usize,
            failed: false,
        })
    }

    fn close(&mut self) -> Result<(), FfiFailure> {
        self.invoke_unit(self.close_callback, "close")
    }

    fn invoke_unit(
        &mut self,
        callback: unsafe extern "C" fn(*mut c_void) -> i32,
        operation: &str,
    ) -> Result<(), FfiFailure> {
        let result = unsafe { callback(self.context as *mut c_void) };
        if result == SUCCESS {
            Ok(())
        } else {
            self.failed = true;
            Err(FfiFailure::new(
                SINK_WRITE_FAILED,
                format!("Sink {operation} failed: {}", self.last_error()),
            ))
        }
    }

    fn last_error(&self) -> String {
        match self.get_last_error_callback {
            Some(callback) => unsafe {
                let ptr = callback(self.context as *mut c_void);
                if ptr.is_null() {
                    "no additional sink error information".to_string()
                } else {
                    std::ffi::CStr::from_ptr(ptr).to_string_lossy().into_owned()
                }
            },
            None => "no additional sink error information".to_string(),
        }
    }
}

unsafe impl Send for SinkWriter {}

impl Write for SinkWriter {
    fn write(&mut self, buf: &[u8]) -> io::Result<usize> {
        let mut total_written = 0usize;

        while total_written < buf.len() {
            let mut written = 0usize;
            let code = unsafe {
                (self.write_callback)(
                    self.context as *mut c_void,
                    buf[total_written..].as_ptr(),
                    buf.len() - total_written,
                    &mut written,
                )
            };

            if code != SUCCESS {
                self.failed = true;
                return Err(io::Error::new(io::ErrorKind::Other, self.last_error()));
            }

            if written == 0 {
                self.failed = true;
                return Err(io::Error::new(
                    io::ErrorKind::WriteZero,
                    "sink reported a zero-byte write",
                ));
            }

            total_written += written;
        }

        Ok(total_written)
    }

    fn flush(&mut self) -> io::Result<()> {
        self.invoke_unit(self.flush_callback, "flush")
            .map_err(|err| io::Error::new(io::ErrorKind::Other, err.message))
    }
}

#[no_mangle]
pub unsafe extern "C" fn parquet_file_writer_create(
    schema: *mut FFI_ArrowSchema,
    options: *const ParquetWriteOptionsFFI,
    sink: *const ParquetOutputSink,
    writer: *mut *mut c_void,
    error: *mut NativeError,
) -> i32 {
    clear_error(error);

    let result = panic::catch_unwind(AssertUnwindSafe(|| {
        create_file_writer(schema, options, sink, writer)
    }));
    match result {
        Ok(Ok(())) => SUCCESS,
        Ok(Err(failure)) => {
            abort_sink(sink);
            set_error(error, failure.code, failure.message)
        }
        Err(_) => {
            abort_sink(sink);
            set_error(
                error,
                INTERNAL_PANIC,
                "Rust parquet file writer creation panicked.",
            )
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn parquet_file_writer_write_batch(
    writer: *mut c_void,
    batch: *mut FFI_ArrowArray,
    error: *mut NativeError,
) -> i32 {
    clear_error(error);

    let result = panic::catch_unwind(AssertUnwindSafe(|| write_batch(writer, batch)));
    match result {
        Ok(Ok(())) => SUCCESS,
        Ok(Err(failure)) => set_error(error, failure.code, failure.message),
        Err(_) => set_error(
            error,
            INTERNAL_PANIC,
            "Rust parquet file writer batch write panicked.",
        ),
    }
}

#[no_mangle]
pub unsafe extern "C" fn parquet_file_writer_finish(
    writer: *mut c_void,
    error: *mut NativeError,
) -> i32 {
    clear_error(error);

    let result = panic::catch_unwind(AssertUnwindSafe(|| finish_file_writer(writer)));
    match result {
        Ok(Ok(())) => SUCCESS,
        Ok(Err(failure)) => set_error(error, failure.code, failure.message),
        Err(_) => set_error(
            error,
            INTERNAL_PANIC,
            "Rust parquet file writer finish panicked.",
        ),
    }
}

#[no_mangle]
pub unsafe extern "C" fn parquet_file_writer_dispose(writer: *mut c_void) {
    if !writer.is_null() {
        drop(Box::from_raw(writer as *mut FileWriterHandle));
    }
}

#[no_mangle]
pub unsafe extern "C" fn parquet_free_string(value: *mut c_char) {
    if !value.is_null() {
        let _ = CString::from_raw(value);
    }
}

unsafe fn create_file_writer(
    schema: *mut FFI_ArrowSchema,
    options: *const ParquetWriteOptionsFFI,
    sink: *const ParquetOutputSink,
    writer: *mut *mut c_void,
) -> Result<(), FfiFailure> {
    if schema.is_null() {
        return Err(FfiFailure::new(
            INVALID_ARGUMENT,
            "Arrow schema pointer is null.",
        ));
    }

    if writer.is_null() {
        return Err(FfiFailure::new(
            INVALID_ARGUMENT,
            "Writer output pointer is null.",
        ));
    }

    let sink = sink
        .as_ref()
        .ok_or_else(|| FfiFailure::new(INVALID_ARGUMENT, "ParquetOutputSink pointer is null."))?;
    let sink_writer = SinkWriter::new(sink)?;
    let props = build_writer_properties(options)?;
    let schema = Schema::try_from(&*schema).map_err(|err| {
        FfiFailure::new(
            ARROW_IMPORT_FAILED,
            format!("Failed to import Arrow schema: {err}"),
        )
    })?;

    let schema = Arc::new(schema);
    let writer_handle = ArrowWriter::try_new(sink_writer, Arc::clone(&schema), Some(props))
        .map_err(|err| {
            FfiFailure::new(
                PARQUET_ENCODE_FAILED,
                format!("Failed to create Parquet writer: {err}"),
            )
        })?;

    *writer = Box::into_raw(Box::new(FileWriterHandle {
        writer: writer_handle,
        schema,
        finished: false,
    })) as *mut c_void;
    Ok(())
}

unsafe fn write_batch(writer: *mut c_void, batch: *mut FFI_ArrowArray) -> Result<(), FfiFailure> {
    if writer.is_null() {
        return Err(FfiFailure::new(
            INVALID_ARGUMENT,
            "Parquet file writer pointer is null.",
        ));
    }

    if batch.is_null() {
        return Err(FfiFailure::new(
            INVALID_ARGUMENT,
            "Arrow batch pointer is null.",
        ));
    }

    let handle = &mut *(writer as *mut FileWriterHandle);
    if handle.finished {
        return Err(FfiFailure::new(
            INVALID_ARGUMENT,
            "Parquet file writer has already been finished.",
        ));
    }

    let ffi_batch = ptr::read(batch);
    ptr::write(batch, std::mem::zeroed());

    let array_data = from_ffi(
        ffi_batch,
        &FFI_ArrowSchema::try_from(handle.schema.as_ref()).map_err(|err| {
            FfiFailure::new(
                ARROW_IMPORT_FAILED,
                format!("Failed to project writer schema to FFI: {err}"),
            )
        })?,
    )
    .map_err(|err| {
        FfiFailure::new(
            ARROW_IMPORT_FAILED,
            format!("Failed to import Arrow batch: {err}"),
        )
    })?;

    let struct_array = StructArray::from(array_data);
    let batch = RecordBatch::from(struct_array);
    handle.writer.write(&batch).map_err(|err| {
        FfiFailure::new(
            PARQUET_ENCODE_FAILED,
            format!("Parquet encoding failed: {err}"),
        )
    })
}

unsafe fn finish_file_writer(writer: *mut c_void) -> Result<(), FfiFailure> {
    if writer.is_null() {
        return Err(FfiFailure::new(
            INVALID_ARGUMENT,
            "Parquet file writer pointer is null.",
        ));
    }

    let handle = &mut *(writer as *mut FileWriterHandle);
    if handle.finished {
        return Ok(());
    }

    handle.writer.finish().map_err(|err| {
        FfiFailure::new(
            PARQUET_ENCODE_FAILED,
            format!("Parquet finalization failed: {err}"),
        )
    })?;

    handle.writer.inner_mut().close()?;
    handle.finished = true;

    Ok(())
}

fn build_writer_properties(
    options: *const ParquetWriteOptionsFFI,
) -> Result<WriterProperties, FfiFailure> {
    let options = unsafe {
        options.as_ref().ok_or_else(|| {
            FfiFailure::new(INVALID_ARGUMENT, "ParquetWriteOptions pointer is null.")
        })?
    };

    let compression = map_compression(options.compression)?;
    let mut max_row_group_row_count = normalize_limit(options.max_row_group_rows)?;
    let max_row_group_bytes = normalize_limit(options.max_row_group_bytes)?;

    if max_row_group_row_count.is_none() && max_row_group_bytes.is_none() {
        // Leaving both limits unset lets parquet-rs buffer arbitrarily large row groups. This
        // library is optimized for streaming engines and bounded memory, so prefer a moderate
        // default row-group size unless the caller explicitly overrides it.
        max_row_group_row_count = Some(DEFAULT_MAX_ROW_GROUP_ROW_COUNT);
    }

    let mut builder = WriterProperties::builder()
        .set_compression(compression)
        .set_dictionary_enabled(options.enable_dictionary_encoding != 0)
        .set_statistics_enabled(map_statistics_level(options.statistics_level)?)
        .set_max_row_group_row_count(max_row_group_row_count)
        .set_max_row_group_bytes(max_row_group_bytes);

    if options.native_write_batch_size >= 0 {
        if options.native_write_batch_size == 0 {
            return Err(FfiFailure::new(
                INVALID_ARGUMENT,
                "Native write batch size must be greater than zero when specified.",
            ));
        }

        builder = builder.set_write_batch_size(options.native_write_batch_size as usize);
    }

    builder = builder.set_created_by(
        read_optional_string(options.created_by)?
            .unwrap_or_else(|| "ParquetRsForDotnet".to_string()),
    );

    let metadata = read_metadata(options.metadata, options.metadata_count)?;
    if !metadata.is_empty() {
        builder = builder.set_key_value_metadata(Some(metadata));
    }

    Ok(builder.build())
}

fn normalize_limit(value: i64) -> Result<Option<usize>, FfiFailure> {
    if value < 0 {
        return Ok(None);
    }

    if value == 0 {
        return Err(FfiFailure::new(
            INVALID_ARGUMENT,
            "Parquet row group limits must be greater than zero when specified.",
        ));
    }

    Ok(Some(value as usize))
}

fn map_compression(value: i32) -> Result<Compression, FfiFailure> {
    match value {
        0 => Ok(Compression::UNCOMPRESSED),
        1 => Ok(Compression::SNAPPY),
        2 => Ok(Compression::GZIP(Default::default())),
        3 => Ok(Compression::LZ4_RAW),
        4 => Ok(Compression::BROTLI(Default::default())),
        5 => Ok(Compression::ZSTD(Default::default())),
        _ => Err(FfiFailure::new(
            INVALID_ARGUMENT,
            format!("Unsupported compression value '{value}'."),
        )),
    }
}

fn map_statistics_level(value: i32) -> Result<EnabledStatistics, FfiFailure> {
    match value {
        0 => Ok(EnabledStatistics::None),
        1 => Ok(EnabledStatistics::Chunk),
        2 => Ok(EnabledStatistics::Page),
        _ => Err(FfiFailure::new(
            INVALID_ARGUMENT,
            format!("Unsupported statistics level '{value}'."),
        )),
    }
}

fn read_optional_string(value: *const c_char) -> Result<Option<String>, FfiFailure> {
    if value.is_null() {
        return Ok(None);
    }

    let text = unsafe { CStr::from_ptr(value) }
        .to_str()
        .map_err(|_| FfiFailure::new(INVALID_ARGUMENT, "Native UTF-8 string is invalid."))?;

    Ok(Some(text.to_string()))
}

fn read_metadata(
    metadata: *const NativeKeyValuePair,
    count: i32,
) -> Result<Vec<KeyValue>, FfiFailure> {
    if metadata.is_null() || count <= 0 {
        return Ok(Vec::new());
    }

    let items = unsafe { std::slice::from_raw_parts(metadata, count as usize) };
    let mut result = Vec::with_capacity(items.len());
    for item in items {
        result.push(KeyValue {
            key: read_optional_string(item.key)?.unwrap_or_default(),
            value: read_optional_string(item.value)?,
        });
    }

    Ok(result)
}

fn clear_error(error: *mut NativeError) {
    unsafe {
        if let Some(error) = error.as_mut() {
            if !error.message.is_null() {
                let _ = CString::from_raw(error.message);
            }

            error.code = SUCCESS;
            error.message = ptr::null_mut();
        }
    }
}

fn set_error(error: *mut NativeError, code: i32, message: impl Into<String>) -> i32 {
    unsafe {
        if let Some(error) = error.as_mut() {
            let c_string = CString::new(message.into())
                .unwrap_or_else(|_| CString::new("native error").unwrap());
            error.code = code;
            error.message = c_string.into_raw();
        }
    }

    code
}

fn abort_sink(sink: *const ParquetOutputSink) {
    unsafe {
        if let Some(sink) = sink.as_ref() {
            if let Some(callback) = sink.abort {
                let _ = callback(sink.context);
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::{Arc, Mutex};

    use arrow::array::{Int32Array, RecordBatch, StringArray, StructArray};
    use arrow::datatypes::{DataType, Field, Schema};
    use arrow::ffi::to_ffi;
    use arrow_array::Array;
    use arrow_schema::ffi::FFI_ArrowSchema;

    #[test]
    fn normalize_limit_returns_none_for_negative_values() {
        assert_eq!(normalize_limit(-1).unwrap(), None);
    }

    #[test]
    fn normalize_limit_rejects_zero() {
        let error = normalize_limit(0).unwrap_err();
        assert_eq!(error.code, INVALID_ARGUMENT);
    }

    #[test]
    fn map_compression_maps_known_values() {
        assert!(matches!(
            map_compression(0).unwrap(),
            Compression::UNCOMPRESSED
        ));
        assert!(matches!(map_compression(1).unwrap(), Compression::SNAPPY));
        assert!(matches!(map_compression(5).unwrap(), Compression::ZSTD(_)));
    }

    #[test]
    fn sink_writer_handles_partial_writes() {
        let state = Box::new(TestSinkState::default());
        let raw_state = Box::into_raw(state);
        let sink = test_sink(raw_state, false, Some(3));
        let mut writer = SinkWriter::new(&sink).unwrap();

        writer.write_all(b"abcdef").unwrap();

        let state = unsafe { Box::from_raw(raw_state) };
        assert_eq!(state.bytes.lock().unwrap().as_slice(), b"abcdef");
    }

    #[test]
    fn sink_writer_surfaces_write_errors() {
        let state = Box::new(TestSinkState::default());
        let raw_state = Box::into_raw(state);
        let sink = test_sink(raw_state, true, None);
        let mut writer = SinkWriter::new(&sink).unwrap();

        let error = writer.write_all(b"abc").unwrap_err();
        assert_eq!(error.kind(), io::ErrorKind::Other);

        let state = unsafe { Box::from_raw(raw_state) };
        assert_eq!(
            state.error.lock().unwrap().as_deref(),
            Some("forced sink failure")
        );
    }

    #[test]
    fn parquet_file_writer_entrypoint_writes_parquet_bytes() {
        let schema = Arc::new(Schema::new(vec![
            Field::new("id", DataType::Int32, true),
            Field::new("name", DataType::Utf8, true),
        ]));
        let batch = RecordBatch::try_new(
            Arc::clone(&schema),
            vec![
                Arc::new(Int32Array::from(vec![Some(1), None, Some(3)])),
                Arc::new(StringArray::from(vec![Some("alpha"), None, Some("gamma")])),
            ],
        )
        .unwrap();

        let state = Box::new(TestSinkState::default());
        let raw_state = Box::into_raw(state);
        let sink = test_sink(raw_state, false, None);
        let options = ParquetWriteOptionsFFI {
            compression: 5,
            enable_dictionary_encoding: 1,
            statistics_level: 1,
            native_write_batch_size: -1,
            max_row_group_rows: -1,
            max_row_group_bytes: -1,
            created_by: ptr::null(),
            metadata: ptr::null(),
            metadata_count: 0,
        };
        let mut error = NativeError {
            code: 0,
            message: ptr::null_mut(),
        };
        let mut ffi_schema = FFI_ArrowSchema::try_from(schema.as_ref()).unwrap();
        let mut writer_ptr: *mut c_void = ptr::null_mut();

        let result = unsafe {
            parquet_file_writer_create(
                &mut ffi_schema,
                &options,
                &sink,
                &mut writer_ptr,
                &mut error,
            )
        };

        assert_eq!(result, SUCCESS);
        assert!(!writer_ptr.is_null());

        let struct_array = StructArray::from(batch.clone());
        let struct_array_data = struct_array.to_data();
        let (ffi_batch, _ffi_batch_schema) = to_ffi(&struct_array_data).unwrap();
        let mut ffi_batch = ffi_batch;

        let result =
            unsafe { parquet_file_writer_write_batch(writer_ptr, &mut ffi_batch, &mut error) };
        assert_eq!(result, SUCCESS);

        let result = unsafe { parquet_file_writer_finish(writer_ptr, &mut error) };
        assert_eq!(result, SUCCESS);

        unsafe { parquet_file_writer_dispose(writer_ptr) };

        let state = unsafe { Box::from_raw(raw_state) };
        let bytes = state.bytes.lock().unwrap().clone();
        assert!(bytes.starts_with(b"PAR1"));
        assert!(bytes.ends_with(b"PAR1"));
    }

    #[test]
    fn build_writer_properties_includes_created_by_and_metadata() {
        let created_by = CString::new("unit-test").unwrap();
        let key = CString::new("source").unwrap();
        let value = CString::new("rust-test").unwrap();
        let metadata = [NativeKeyValuePair {
            key: key.as_ptr(),
            value: value.as_ptr(),
        }];

        let options = ParquetWriteOptionsFFI {
            compression: 5,
            enable_dictionary_encoding: 1,
            statistics_level: 1,
            native_write_batch_size: -1,
            max_row_group_rows: -1,
            max_row_group_bytes: -1,
            created_by: created_by.as_ptr(),
            metadata: metadata.as_ptr(),
            metadata_count: metadata.len() as i32,
        };

        let props = build_writer_properties(&options).unwrap();
        assert_eq!(props.created_by(), "unit-test");
        let metadata_binding = props.key_value_metadata();
        let metadata = metadata_binding.as_ref().unwrap();
        assert_eq!(metadata[0].key, "source");
        assert_eq!(metadata[0].value.as_deref(), Some("rust-test"));
    }

    #[derive(Default)]
    struct TestSinkState {
        bytes: Mutex<Vec<u8>>,
        error: Mutex<Option<String>>,
        partial_write_size: Mutex<Option<usize>>,
        fail_write: Mutex<bool>,
    }

    fn test_sink(
        raw_state: *mut TestSinkState,
        fail_write: bool,
        partial_write_size: Option<usize>,
    ) -> ParquetOutputSink {
        unsafe {
            (*raw_state).fail_write = Mutex::new(fail_write);
            (*raw_state).partial_write_size = Mutex::new(partial_write_size);
        }

        ParquetOutputSink {
            write: Some(test_write),
            flush: Some(test_flush),
            close: Some(test_close),
            abort: Some(test_abort),
            get_last_error: Some(test_get_last_error),
            context: raw_state.cast(),
        }
    }

    unsafe extern "C" fn test_write(
        context: *mut c_void,
        data: *const u8,
        len: usize,
        written: *mut usize,
    ) -> i32 {
        let state = &*(context as *mut TestSinkState);
        if *state.fail_write.lock().unwrap() {
            *state.error.lock().unwrap() = Some("forced sink failure".to_string());
            return SINK_WRITE_FAILED;
        }

        let partial = *state.partial_write_size.lock().unwrap();
        let slice = std::slice::from_raw_parts(data, partial.unwrap_or(len).min(len));
        state.bytes.lock().unwrap().extend_from_slice(slice);
        *written = slice.len();
        SUCCESS
    }

    unsafe extern "C" fn test_flush(_context: *mut c_void) -> i32 {
        SUCCESS
    }

    unsafe extern "C" fn test_close(_context: *mut c_void) -> i32 {
        SUCCESS
    }

    unsafe extern "C" fn test_abort(context: *mut c_void) -> i32 {
        let state = &*(context as *mut TestSinkState);
        *state.error.lock().unwrap() = Some("aborted".to_string());
        SUCCESS
    }

    unsafe extern "C" fn test_get_last_error(context: *mut c_void) -> *const c_char {
        let state = &*(context as *mut TestSinkState);
        if let Some(message) = state.error.lock().unwrap().clone() {
            CString::new(message).unwrap().into_raw()
        } else {
            ptr::null()
        }
    }
}
