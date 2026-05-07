using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;

namespace ParquetRsForDotnet
{
    /// <summary>
    /// Provides small target-framework shims used to keep the main code paths shared between
    /// <c>net8.0</c> and <c>netstandard2.0</c> builds.
    /// </summary>
    /// <remarks>
    /// This type intentionally stays internal and narrow: it only wraps APIs that are missing
    /// from one target framework, or APIs whose names would otherwise require scattered
    /// preprocessor conditionals across public, read, write, and interop code.
    /// </remarks>
    internal static class TargetFrameworkCompat
    {
        /// <summary>
        /// Gets the Unix epoch value for target frameworks where <see cref="DateTime.UnixEpoch" /> is unavailable.
        /// </summary>
        public static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Throws <see cref="ArgumentNullException" /> when a required reference is <see langword="null" />.
        /// </summary>
        /// <typeparam name="T">The reference type being checked.</typeparam>
        /// <param name="value">The value to validate.</param>
        /// <param name="parameterName">The caller argument name supplied by the compiler when available.</param>
        public static void ThrowIfNull<T>(T? value, [CallerArgumentExpression("value")] string? parameterName = null)
            where T : class
        {
            if (value is null)
            {
                throw new ArgumentNullException(parameterName ?? "value");
            }
        }

        /// <summary>
        /// Throws <see cref="ArgumentException" /> when a required string is <see langword="null" />, empty, or whitespace.
        /// </summary>
        /// <param name="value">The string value to validate.</param>
        /// <param name="parameterName">The caller argument name supplied by the compiler when available.</param>
        public static void ThrowIfNullOrWhiteSpace(string? value, [CallerArgumentExpression("value")] string? parameterName = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", parameterName ?? "value");
            }
        }

        /// <summary>
        /// Throws <see cref="ObjectDisposedException" /> when an object has already been disposed.
        /// </summary>
        /// <param name="disposed">Whether the owning object has been disposed.</param>
        /// <param name="instance">The object instance used to populate the exception name.</param>
        public static void ThrowIfDisposed(bool disposed, object instance)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(instance.GetType().FullName);
            }
        }

        /// <summary>
        /// Allocates a null-terminated UTF-8 string in COM task memory for native interop.
        /// </summary>
        /// <param name="value">The managed string to marshal, or <see langword="null" />.</param>
        /// <returns>A pointer that must be released with <see cref="Marshal.FreeCoTaskMem" />, or <see cref="IntPtr.Zero" />.</returns>
        public static IntPtr StringToCoTaskMemUtf8(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return IntPtr.Zero;
            }

            // netstandard2.0 does not expose Marshal.StringToCoTaskMemUTF8, so allocate
            // an explicit null-terminated UTF-8 buffer for every native string input.
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            IntPtr pointer = Marshal.AllocCoTaskMem(bytes.Length + 1);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            Marshal.WriteByte(pointer, bytes.Length, 0);
            return pointer;
        }

        /// <summary>
        /// Converts a null-terminated UTF-8 native string pointer to a managed string.
        /// </summary>
        /// <param name="value">The native UTF-8 string pointer.</param>
        /// <returns>The decoded managed string, or <see langword="null" /> when <paramref name="value" /> is zero.</returns>
        public static string? PtrToStringUtf8(IntPtr value)
        {
            if (value == IntPtr.Zero)
            {
                return null;
            }

            int length = 0;
            while (Marshal.ReadByte(value, length) != 0)
            {
                length++;
            }

            if (length == 0)
            {
                return string.Empty;
            }

            // netstandard2.0 does not expose Marshal.PtrToStringUTF8. Native error
            // messages are short and null-terminated, so a single copy is sufficient.
            byte[] bytes = new byte[length];
            Marshal.Copy(value, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Returns whether the current process is running on Windows.
        /// </summary>
        public static bool IsWindows()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        /// <summary>
        /// Returns whether the current process is running on macOS.
        /// </summary>
        public static bool IsMacOS()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        }
    }
}

#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    // These compiler-surface polyfills keep modern C# syntax usable when the
    // assembly targets netstandard2.0; they are not part of the public API.
    internal static class IsExternalInit
    {
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class CallerArgumentExpressionAttribute : Attribute
    {
        public CallerArgumentExpressionAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }

        public string ParameterName { get; }
    }
}
#endif
