using Avalonia.Animation.Easings;
using Avalonia.Animation;
using Avalonia.Collections;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Labs.Gif;
using Avalonia.Layout;
using Avalonia.Logging;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia;
using Ludex.Services;
using Ludex.UI;
using Microsoft.Win32;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Ludex.Services;

public static class Autostart
{
	private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

	private const string ValueName = "Ludex";

	private const string LegacyValueName = "GameVault";

	public static bool IsSupported => OperatingSystem.IsWindows();

	public static bool IsEnabled()
	{
		if (!IsSupported)
		{
			return false;
		}
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: false);
			if (registryKey == null)
			{
				return false;
			}
			return registryKey.GetValue("Ludex") is string { Length: >0 } || (registryKey.GetValue("GameVault") is string text2 && text2.Length > 0);
		}
		catch (Exception ex)
		{
			AppPaths.Log("Autostart read failed: " + ex.Message);
			return false;
		}
	}

	public static bool Set(bool enabled)
	{
		if (!IsSupported)
		{
			return false;
		}
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
			if (registryKey == null)
			{
				return false;
			}
			if (enabled)
			{
				string processPath = Environment.ProcessPath;
				if (string.IsNullOrWhiteSpace(processPath))
				{
					return false;
				}
				registryKey.SetValue("Ludex", "\"" + processPath + "\"");
				registryKey.DeleteValue("GameVault", throwOnMissingValue: false);
			}
			else
			{
				registryKey.DeleteValue("Ludex", throwOnMissingValue: false);
				registryKey.DeleteValue("GameVault", throwOnMissingValue: false);
			}
			return IsEnabled() == enabled;
		}
		catch (Exception ex)
		{
			AppPaths.Log("Autostart write failed: " + ex.Message);
			return false;
		}
	}
}
