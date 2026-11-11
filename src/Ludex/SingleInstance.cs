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

namespace Ludex;

internal static class SingleInstance
{
	private const string MutexName = "Local\\Ludex.SingleInstance";

	private const string ShowEventName = "Local\\Ludex.ShowWindow";

	private static Mutex? _mutex;

	private static EventWaitHandle? _showEvent;

	private static RegisteredWaitHandle? _wait;

	public static bool IsFirst { get; private set; } = true;


	public static bool Claim()
	{
		try
		{
			_mutex = new Mutex(initiallyOwned: true, "Local\\Ludex.SingleInstance", out var createdNew);
			IsFirst = createdNew;
			if (!createdNew)
			{
				SignalExisting();
				return false;
			}
			return true;
		}
		catch (Exception ex)
		{
			AppPaths.Log("SingleInstance: " + ex.Message);
			IsFirst = true;
			return true;
		}
	}

	public static void Listen(Action show)
	{
		Action show2 = show;
		try
		{
			_showEvent = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, "Local\\Ludex.ShowWindow");
			_wait = ThreadPool.RegisterWaitForSingleObject(_showEvent, delegate
			{
				Dispatcher.UIThread.Post(show2, default(DispatcherPriority));
			}, null, -1, executeOnlyOnce: false);
		}
		catch (Exception ex)
		{
			AppPaths.Log("SingleInstance listen: " + ex.Message);
		}
	}

	private static void SignalExisting()
	{
		try
		{
			if (EventWaitHandle.TryOpenExisting("Local\\Ludex.ShowWindow", out EventWaitHandle result))
			{
				using (result)
				{
					result.Set();
					return;
				}
			}
		}
		catch (Exception ex)
		{
			AppPaths.Log("SingleInstance signal: " + ex.Message);
		}
	}
}
