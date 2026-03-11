using BenchmarkDotNet.Running;
using ParquetRsForDotnet.Benchmarks;

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(args);
