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

public static class Shell
{
	public static bool LaunchGame(string path, out string error, bool asAdmin = false)
	{
		error = "";
		try
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				error = "empty path";
				return false;
			}
			if (Platform.IsBundle(path))
			{
				if (!Directory.Exists(path))
				{
					error = "bundle not found";
					return false;
				}
				Process.Start(new ProcessStartInfo("open", "\"" + path + "\"")
				{
					UseShellExecute = false
				});
				return true;
			}
			if (!File.Exists(path))
			{
				error = "exe not found";
				return false;
			}
			if (!Platform.IsWindows)
			{
				Platform.MakeExecutable(path);
			}
			ProcessStartInfo processStartInfo = new ProcessStartInfo
			{
				FileName = path,
				WorkingDirectory = (System.IO.Path.GetDirectoryName(path) ?? ""),
				UseShellExecute = true
			};
			if (asAdmin && Platform.SupportsElevation)
			{
				processStartInfo.Verb = "runas";
			}
			Process.Start(processStartInfo);
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			AppPaths.Log("LaunchGame failed: " + ex);
			return false;
		}
	}

	public static void OpenFolder(string path)
	{
		try
		{
			if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
			{
				if (Platform.IsWindows)
				{
					Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"")
					{
						UseShellExecute = true
					});
				}
				else if (Platform.IsMac)
				{
					Process.Start(new ProcessStartInfo("open", "\"" + path + "\"")
					{
						UseShellExecute = false
					});
				}
				else
				{
					Process.Start(new ProcessStartInfo("xdg-open", "\"" + path + "\"")
					{
						UseShellExecute = false
					});
				}
			}
		}
		catch (Exception ex)
		{
			AppPaths.Log("OpenFolder failed: " + ex.Message);
		}
	}

	public static void OpenUrl(string url)
	{
		try
		{
			if (Platform.IsWindows)
			{
				Process.Start(new ProcessStartInfo(url)
				{
					UseShellExecute = true
				});
			}
			else
			{
				Process.Start(new ProcessStartInfo(Platform.IsMac ? "open" : "xdg-open", "\"" + url + "\"")
				{
					UseShellExecute = false
				});
			}
		}
		catch (Exception ex)
		{
			AppPaths.Log("OpenUrl failed: " + ex.Message);
			try
			{
				Process.Start(new ProcessStartInfo(url)
				{
					UseShellExecute = true
				});
			}
			catch (Exception ex2)
			{
				AppPaths.Log("OpenUrl fallback failed: " + ex2.Message);
			}
		}
	}
}
