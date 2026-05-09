# How To Package Native Assets

## Scenario

Use this guide when generating a local NuGet package or validating that the package contains the expected managed and native assets.

## Windows-Only Package

Build and pack the managed assemblies plus the Windows native library:

```powershell
dotnet pack src/ParquetRsForDotnet.csproj -c Release
```

Expected package outputs:

```text
src/bin/Release/ParquetRsForDotnet.0.1.0.nupkg
src/bin/Release/ParquetRsForDotnet.0.1.0.snupkg
```

Expected native asset in the `.nupkg`:

```text
runtimes/win-x64/native/parquet_rs_for_dotnet.dll
```

## Windows Plus Linux Package

Install the cross-build toolchain once:

```powershell
winget install zig.zig
cargo install cargo-zigbuild
rustup target add x86_64-unknown-linux-gnu
```

Then pack with Linux cross-build enabled:

```powershell
dotnet pack src/ParquetRsForDotnet.csproj -c Release -p:CrossBuildLinux=true
```

Expected native assets in the `.nupkg`:

```text
runtimes/win-x64/native/parquet_rs_for_dotnet.dll
runtimes/linux-x64/native/libparquet_rs_for_dotnet.so
```

## Expected Public Package Artifacts

The package should contain these stable public-contract artifacts:

```text
README.md
api/public-api.md
api/semantic-index.json
docs/ai/bootstrap.md
lib/net8.0/ParquetRsForDotnet.dll
lib/netstandard2.0/ParquetRsForDotnet.dll
```

Repo-only docs under `docs/architecture`, `docs/how-to`, and `docs/adr` are intentionally not packed unless explicitly listed in the project file.

## Verify Package Contents

From PowerShell:

```powershell
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::OpenRead("src/bin/Release/ParquetRsForDotnet.0.1.0.nupkg").Entries |
    Select-Object -ExpandProperty FullName
```

## Common Failures

### Linux `.so` Missing

Cause: package was built without `-p:CrossBuildLinux=true`, or Linux cross-build failed.

Fix: run the cross-build command and confirm `cargo-zigbuild` and Zig are available.

### Zig Not Found On Windows

Cause: Zig was installed but is not on the shell `PATH`.

Fix: add the WinGet links directory to `PATH` for the current shell, then rerun pack:

```bash
export PATH="$PATH:/c/Users/<user>/AppData/Local/Microsoft/WinGet/Links"
```

### Wrong Native Asset Layout

Cause: native files were copied beside managed assemblies but not packed under `runtimes/<rid>/native/`.

Fix: inspect `src/ParquetRsForDotnet.csproj` pack items and the `IncludeLinuxNativeLibrary` target.

### Downstream Native Load Failure

Cause: consuming app does not copy or resolve the RID-specific native asset.

Fix: inspect the consuming app output for the native library and verify the app targets a supported RID.
