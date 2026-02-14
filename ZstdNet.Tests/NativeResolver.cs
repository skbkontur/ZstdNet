using System;
using System.IO;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace ZstdNet.Tests;

[SetUpFixture]
// Native dependencies are not added to the deps.json file via ProjectReference
// https://github.com/dotnet/sdk/issues/10575
public class NativeResolver
{
	[OneTimeSetUp]
	public static void SetLibZstdResolver()
	{
		var ext = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dll" : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "dylib" : "so";
		NativeLibrary.SetDllImportResolver(typeof(DictBuilder).Assembly, (name, _, _) => name == "libzstd"
			? NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native", $"libzstd.{ext}"))
			: IntPtr.Zero);
	}
}
