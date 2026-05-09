# How To Debug Native Loading

## Expected Package Layout

```text
runtimes/win-x64/native/parquet_rs_for_dotnet.dll
runtimes/linux-x64/native/libparquet_rs_for_dotnet.so
```

For package generation commands and expected `.nupkg` contents, see `docs/how-to/package-and-native-assets.md`.

## Checks

- Confirm the consuming app targets a supported RID.
- Confirm the native file is copied to the output or available through runtime asset resolution.
- On .NET Framework, confirm the Windows DLL is present beside the app or resolvable by the loader.
- Set `PARQUET_RS_FOR_DOTNET_NATIVE_DIR` to a directory containing the native library when debugging custom extraction layouts.

## Common Causes

- Package was built without Linux cross-build but deployed to Linux.
- Consuming app uses an unsupported RID.
- Native asset copy was disabled or customized by the host build.
