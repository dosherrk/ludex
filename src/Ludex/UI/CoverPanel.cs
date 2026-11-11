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

namespace Ludex.UI;

public sealed class CoverPanel : Border
{
	private readonly string _gameId;

	private readonly string? _sgdbKey;

	private readonly WrapPanel _results = new WrapPanel
	{
		Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
	};

	private readonly TextBlock _status;

	private readonly TextBox _query;

	private TextBox _urlBox;

	private TextBox _appIdBox;

	private CancellationTokenSource? _cts;

	private int? _appId;

	public string? ChosenCoverPath { get; private set; }

	public bool ChosenIsPortrait { get; private set; }

	public bool ResetRequested { get; private set; }

	public event Action? Closed;

	public CoverPanel(string gameTitle, string? sgdbKey, string gameId, int? appId = null)
	{
		_gameId = gameId;
		_sgdbKey = sgdbKey;
		_appId = appId;
		((Layoutable)this).MaxWidth = 980.0;
		((Layoutable)this).MaxHeight = 760.0;
		((Border)this).Background = Skin.Panel;
		((Border)this).CornerRadius = new CornerRadius(14.0);
		BoxShadow val = new BoxShadow
		{
			OffsetX = 0.0,
			OffsetY = 14.0,
			Blur = 44.0,
			Color = Color.Parse("#AA000000")
		};
		((Border)this).BoxShadow = new BoxShadows(val);
		_query = new TextBox
		{
			Text = gameTitle,
			FontSize = 14.0,
			Height = 38.0,
			Background = Skin.PanelAlt,
			Foreground = Skin.Text,
			BorderBrush = Skin.Border,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(6.0),
			Padding = new Thickness(10.0, 0.0, 10.0, 0.0),
			VerticalContentAlignment = (VerticalAlignment)2
		};
		Ui.AnimateTextBox(_query);
		((InputElement)_query).KeyDown += delegate(object? _, KeyEventArgs e)
		{
			if ((int)e.Key == 6)
			{
				_ = SearchAsync();
			}
		};
		_status = Ui.T("", 12.0, Skin.TextMuted, null, (TextWrapping)1);
		Grid val2 = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto")
		};
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)_query);
		Border val3 = Ui.PrimaryButton(Loc.T("cover.searchBtn"), delegate
		{
			SearchAsync();
		});
		((Layoutable)val3).Margin = new Thickness(10.0, 0.0, 0.0, 0.0);
		Grid.SetColumn((Control)(object)val3, 1);
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)val3);
		Border val4 = Ui.GhostButton(Loc.T("btn.browseFile"), delegate
		{
			PickFileAsync();
		});
		((Layoutable)val4).Margin = new Thickness(10.0, 0.0, 0.0, 0.0);
		Grid.SetColumn((Control)(object)val4, 2);
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)val4);
		StackPanel val5 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 10.0,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		_urlBox = new TextBox
		{
			Watermark = Loc.T("cover.urlWatermark"),
			FontSize = 12.5,
			Height = 34.0,
			Background = Skin.PanelAlt,
			Foreground = Skin.Text,
			BorderBrush = Skin.Border,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(6.0),
			Padding = new Thickness(10.0, 0.0, 10.0, 0.0),
			VerticalContentAlignment = (VerticalAlignment)2,
			Width = 420.0
		};
		Ui.AnimateTextBox(_urlBox);
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)_urlBox);
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)Ui.GhostButton(Loc.T("cover.urlApply"), delegate
		{
			UseUrlAsync();
		}));
		_appIdBox = new TextBox
		{
			Watermark = Loc.T("cover.appIdWatermark"),
			FontSize = 12.5,
			Height = 34.0,
			Width = 150.0,
			Background = Skin.PanelAlt,
			Foreground = Skin.Text,
			BorderBrush = Skin.Border,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(6.0),
			Padding = new Thickness(10.0, 0.0, 10.0, 0.0),
			VerticalContentAlignment = (VerticalAlignment)2
		};
		Ui.AnimateTextBox(_appIdBox);
		if (_appId.HasValue)
		{
			_appIdBox.Text = _appId.Value.ToString();
		}
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)_appIdBox);
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)Ui.GhostButton(Loc.T("cover.appIdApply"), delegate
		{
			if (int.TryParse(_appIdBox.Text?.Trim(), out var result) && result > 0)
			{
				_appId = result;
				SearchAsync();
			}
		}));
		ScrollViewer val6 = new ScrollViewer
		{
			Content = _results,
			VerticalScrollBarVisibility = (ScrollBarVisibility)1,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		Ui.SmoothScroll(val6, 120.0);
		StackPanel val7 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 10.0,
			HorizontalAlignment = (HorizontalAlignment)3,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		((AvaloniaList<Control>)(object)((Panel)val7).Children).Add((Control)(object)Ui.GhostButton(Loc.T("btn.resetCover"), delegate
		{
			ResetRequested = true;
			Close();
		}));
		((AvaloniaList<Control>)(object)((Panel)val7).Children).Add((Control)(object)Ui.GhostButton(Loc.T("btn.cancel"), Close));
		DockPanel root = new DockPanel
		{
			Margin = new Thickness(22.0)
		};
		StackPanel val8 = new StackPanel();
		((AvaloniaList<Control>)(object)((Panel)val8).Children).Add((Control)(object)val2);
		((AvaloniaList<Control>)(object)((Panel)val8).Children).Add((Control)(object)val5);
		StackPanel val9 = val8;
		DockPanel.SetDock((Control)(object)val9, (Dock)3);
		((AvaloniaList<Control>)(object)((Panel)root).Children).Add((Control)(object)val9);
		DockPanel.SetDock((Control)(object)_status, (Dock)3);
		((Layoutable)_status).Margin = new Thickness(0.0, 12.0, 0.0, 0.0);
		((AvaloniaList<Control>)(object)((Panel)root).Children).Add((Control)(object)_status);
		DockPanel.SetDock((Control)(object)val7, (Dock)1);
		((AvaloniaList<Control>)(object)((Panel)root).Children).Add((Control)(object)val7);
		((AvaloniaList<Control>)(object)((Panel)root).Children).Add((Control)(object)val6);
		((Decorator)this).Child = (Control)(object)root;
		this.AttachedToVisualTree += delegate
		{
			Ui.FadeSlideIn((Visual)(object)root);
			((InputElement)_query).Focus((NavigationMethod)0, (KeyModifiers)0);
			SearchAsync();
		};
		((InputElement)this).KeyDown += delegate(object? _, KeyEventArgs e)
		{
			if ((int)e.Key == 13)
			{
				((RoutedEventArgs)e).Handled = true;
				Close();
			}
		};
	}

	public void Cancel()
	{
		Close();
	}

	private void Close()
	{
		_cts?.Cancel();
		this.Closed?.Invoke();
	}

	private async Task PickFileAsync()
	{
		TopLevel topLevel = TopLevel.GetTopLevel((Visual)(object)this);
		if (topLevel == null)
		{
			return;
		}
		IStorageProvider storageProvider = topLevel.StorageProvider;
		FilePickerOpenOptions val = new FilePickerOpenOptions();
		((PickerOptions)val).Title = Loc.T("cover.title");
		val.AllowMultiple = false;
		FilePickerFileType[] array = new FilePickerFileType[1];
		FilePickerFileType val2 = new FilePickerFileType("Images");
		val2.Patterns = new string[5] { "*.jpg", "*.jpeg", "*.png", "*.webp", "*.bmp" };
		array[0] = val2;
		val.FileTypeFilter = (IReadOnlyList<FilePickerFileType>)(object)array;
		IStorageFile val3 = (await storageProvider.OpenFilePickerAsync(val)).FirstOrDefault();
		if (val3 == null)
		{
			return;
		}
		string text = StorageProviderExtensions.TryGetLocalPath((IStorageItem)(object)val3);
		if (!string.IsNullOrEmpty(text))
		{
			(int w, int h) tuple = ImageSize(text);
			int item = tuple.w;
			int item2 = tuple.h;
			string text2 = Artwork.SaveLocalCover(_gameId, text, item2 >= item);
			if (text2 != null)
			{
				ChosenCoverPath = text2;
				ChosenIsPortrait = item2 >= item;
				Close();
			}
			else
			{
				_status.Text = Loc.T("cover.noresults");
			}
		}
	}

	private static (int w, int h) ImageSize(string path)
	{
		try
		{
			Bitmap val = new Bitmap(path);
			try
			{
				Size size = val.Size;
				int item = (int)((Size)size).Width;
				size = val.Size;
				return (w: item, h: (int)((Size)size).Height);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		catch
		{
			return (w: 600, h: 900);
		}
	}

	private async Task UseUrlAsync()
	{
		string text = _urlBox.Text?.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		if (!text.StartsWith("http", StringComparison.OrdinalIgnoreCase))
		{
			text = "https://" + text;
		}
		_status.Text = Loc.T("cover.searching");
		byte[] array = await Artwork.FetchBytesAsync(text);
		if (array == null)
		{
			_status.Text = Loc.T("cover.badUrl");
			return;
		}
		bool flag;
		try
		{
			Bitmap val = new Bitmap((Stream)new MemoryStream(array));
			try
			{
				Size size = val.Size;
				double height = ((Size)size).Height;
				size = val.Size;
				flag = height >= ((Size)size).Width;
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		catch
		{
			_status.Text = Loc.T("cover.badUrl");
			return;
		}
		string text2 = Artwork.SaveCover(_gameId, array, flag);
		if (text2 == null)
		{
			_status.Text = Loc.T("cover.badUrl");
			return;
		}
		ChosenCoverPath = text2;
		ChosenIsPortrait = flag;
		Close();
	}

	private async Task SearchAsync()
	{
		_cts?.Cancel();
		_cts = new CancellationTokenSource();
		CancellationToken ct = _cts.Token;
		((AvaloniaList<Control>)(object)((Panel)_results).Children).Clear();
		string query = _query.Text?.Trim();
		if (string.IsNullOrWhiteSpace(query) && !_appId.HasValue)
		{
			return;
		}
		_status.Text = Loc.T("cover.searching");
		List<ArtCandidate> candidates3 = new List<ArtCandidate>();
		if (_appId.HasValue)
		{
			List<ArtCandidate> list = candidates3;
			list.AddRange(await Artwork.SteamCandidatesAsync(_appId.Value, query ?? "", ct));
		}
		if (!string.IsNullOrWhiteSpace(query))
		{
			try
			{
				List<ArtCandidate> list = candidates3;
				list.AddRange(await Artwork.SearchAsync(query, _sgdbKey, ct));
			}
			catch
			{
			}
		}
		if (ct.IsCancellationRequested)
		{
			return;
		}
		candidates3 = (from c in candidates3
			where !string.IsNullOrWhiteSpace(c.Url)
			group c by c.Url into g
			select g.First() into c
			orderby c.Portrait descending
			select c).ToList();
		if (candidates3.Count == 0)
		{
			_status.Text = Loc.T("cover.noresults");
			return;
		}
		_status.Text = Loc.F("cover.tipPortrait") + " · " + Loc.T("cover.tipWide");
		SemaphoreSlim semaphore = new SemaphoreSlim(4);
		int shown = 0;
		await Task.WhenAll(candidates3.Take(14).Select((Func<ArtCandidate, Task>)async delegate(ArtCandidate candidate)
		{
			ArtCandidate candidate2 = candidate;
			await semaphore.WaitAsync(ct);
			try
			{
				byte[] bytes = await Artwork.FetchBytesAsync(candidate2.Url, ct);
				if (bytes != null && !ct.IsCancellationRequested)
				{
					await Dispatcher.UIThread.InvokeAsync((Action)delegate
					{
						if (!ct.IsCancellationRequested)
						{
							Bitmap source;
							try
							{
								source = new Bitmap((Stream)new MemoryStream(bytes));
							}
							catch
							{
								return;
							}
							double width = 150.0;
							double height = 225.0;
							Border val = Ui.Clickable((Control)new Image
							{
								Source = (IImage)(object)source,
								Stretch = (Stretch)3,
								Width = width,
								Height = height
							}, (Action?)delegate
							{
								string text = Artwork.SaveCover(_gameId, bytes, candidate2.Portrait);
								if (text != null)
								{
									ChosenCoverPath = text;
									ChosenIsPortrait = candidate2.Portrait;
									Close();
								}
							}, Skin.Card, Skin.Accent, (CornerRadius?)new CornerRadius(6.0), (Thickness?)new Thickness(0.0), (IBrush?)null, (Thickness?)null);
							((Layoutable)val).Margin = new Thickness(0.0, 0.0, 12.0, 12.0);
							((Layoutable)val).Width = width;
							((Layoutable)val).Height = height;
							val.ClipToBounds = true;
							string value = ((candidate2.Source == "steamgriddb") ? "SteamGridDB" : "Steam");
							TextBlock val2 = Ui.T($"{value} · {candidate2.Width}×{candidate2.Height}", 10.0, Skin.TextDim, null, (TextWrapping)0);
							((Layoutable)val2).Margin = new Thickness(0.0, 4.0, 12.0, 12.0);
							StackPanel val3 = new StackPanel
							{
								Width = width
							};
							((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)val);
							((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)val2);
							((Layoutable)val3).Margin = new Thickness(0.0, 0.0, 12.0, 12.0);
							((AvaloniaList<Control>)(object)((Panel)_results).Children).Add((Control)(object)val3);
							int num = shown;
							shown = num + 1;
							_status.Text = string.Format("{0} · {1}", Loc.T("cover.tipPortrait"), shown);
						}
					}, DispatcherPriority.Background);
				}
			}
			finally
			{
				semaphore.Release();
			}
		}));
		if (shown == 0 && !ct.IsCancellationRequested)
		{
			_status.Text = Loc.T("cover.noresults");
		}
	}
}
