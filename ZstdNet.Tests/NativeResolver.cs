using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DoNotParallelize]

namespace ZstdNet.Tests;

[TestClass]
// Native dependencies are not added to the deps.json file via ProjectReference
// https://github.com/dotnet/sdk/issues/10575
public class NativeResolver
{
	[AssemblyInitialize]
	public static void SetLibZstdResolver(TestContext _)
	{
		var ext = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dll" : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "dylib" : "so";
		NativeLibrary.SetDllImportResolver(typeof(DictBuilder).Assembly, (name, _, _) => name == "libzstd"
			? NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native", $"libzstd.{ext}"))
			: IntPtr.Zero);
	}
}
