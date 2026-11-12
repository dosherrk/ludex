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

public sealed class SettingsPanel : Border
{
	private readonly AppSettings _original;

	private readonly List<string> _folders;

	private StackPanel _folderRows;

	private int _selectedFolder = -1;

	private TextBox _keyBox;

	private TextBox _ignoreBox;

	private Dropdown _langCombo;

	private Dropdown _engineCombo;

	private Toggle _autoScan;

	private Toggle _closeToTray;

	private Toggle _autoDetect;

	private StackPanel _foldersBlock;

	private Border _foldersBadge;

	private StackPanel? _foldersTitleRow;

	private Button _scanButton;

	private bool _autoScanGames;

	private bool _closeOnTray;

	private Toggle _autoStart;

	private Toggle _autoUpdate;

	private TextBlock _folderCount;

	private string _language;

	private string _searchEngine;

	private string _theme;

	private string _keyText;

	private string _ignoreText;

	private bool _scanOnStartup;

	private bool _runAtStartup;

	private bool _checkUpdates;

	public bool ScanRequested { get; private set; }

	public event Action? CleanupRequested;

	public event Action? Saved;

	public event Action? Closed;

	public event Action? AppearanceChanged;

	public event Action? WallpaperChanged;

	public event Action? UpdateCheckRequested;

	public event Action? HiddenListRequested;

	public SettingsPanel(AppSettings settings)
	{
		_original = settings;
		_folders = new List<string>(settings.Folders);
		_language = settings.Language;
		_searchEngine = settings.SearchEngine;
		_theme = settings.Theme;
		_keyText = settings.SteamGridDbKey ?? "";
		_ignoreText = string.Join(", ", settings.ExtraIgnoreExe);
		_scanOnStartup = settings.AutoScanOnStartup;
		_closeOnTray = settings.CloseToTray;
		_runAtStartup = Autostart.IsEnabled();
		_checkUpdates = settings.CheckUpdatesOnStartup;
		_autoScanGames = settings.AutoScan;
		((Layoutable)this).MaxWidth = 860.0;
		((Layoutable)this).MaxHeight = 720.0;
		((Border)this).Background = Skin.Panel;
		((Border)this).CornerRadius = new CornerRadius(14.0);
		this.RenderTransformOrigin = new RelativePoint(0.5, 0.5, (RelativeUnit)0);
		Transitions val = new Transitions();
		((AvaloniaList<ITransition>)val).Add((ITransition)new TransformOperationsTransition
		{
			Property = (AvaloniaProperty)(object)Visual.RenderTransformProperty,
			Duration = TimeSpan.FromMilliseconds(190.0),
			Easing = (Easing)new CubicEaseOut()
		});
		((Animatable)this).Transitions = val;
		BoxShadow val2 = new BoxShadow
		{
			OffsetX = 0.0,
			OffsetY = 14.0,
			Blur = 44.0,
			Color = Color.Parse("#AA000000"),
		};
		((Border)this).BoxShadow = new BoxShadows(val2);
		BuildContent();
	}

	private bool settings_hasHidden()
	{
		return _original.HiddenGames.Count > 0;
	}

	private void BuildContent()
	{
		DockPanel val = new DockPanel
		{
			Margin = new Thickness(26.0, 22.0, 22.0, 0.0)
		};
		Border val2 = Ui.Clickable((Control)(object)Ui.Icon("M1,1 L11,11 M11,1 L1,11", Skin.TextMuted, 12.0, 12.0, stroke: true), (Action?)delegate
		{
			this.Closed?.Invoke();
		}, Skin.PanelAlt, Skin.CardHover, (CornerRadius?)new CornerRadius(6.0), (Thickness?)new Thickness(10.0, 10.0), (IBrush?)null, (Thickness?)null);
		((Layoutable)val2).VerticalAlignment = (VerticalAlignment)1;
		DockPanel.SetDock((Control)(object)val2, (Dock)2);
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val2);
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)Ui.T(Loc.T("settings.title"), 21.0, Skin.Text, (FontWeight)700, (TextWrapping)0));
		StackPanel val3 = new StackPanel
		{
			Margin = new Thickness(26.0, 18.0, 26.0, 8.0),
			Spacing = 16.0
		};
		StackPanel val4 = (_foldersBlock = new StackPanel
		{
			Spacing = 16.0
		});
		Grid val5 = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("*,Auto")
		};
		StackPanel val6 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 9.0,
			VerticalAlignment = (VerticalAlignment)2
		};
		((AvaloniaList<Control>)(object)((Panel)val6).Children).Add((Control)(object)Ui.T(Loc.T("settings.folders"), 14.0, Skin.Text, (FontWeight)600, (TextWrapping)0));
		_foldersTitleRow = val6;
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)_foldersTitleRow);
		Border val7 = Ui.GhostButton("+  " + Loc.T("settings.addFolder"), delegate
		{
			AddFoldersAsync();
		});
		((Layoutable)val7).HorizontalAlignment = (HorizontalAlignment)3;
		Grid.SetColumn((Control)(object)val7, 1);
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)val7);
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)val5);
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)Ui.T(Loc.T("settings.foldersHint"), 11.5, Skin.TextDim, null, (TextWrapping)1));
		_folderRows = new StackPanel
		{
			Spacing = 2.0,
			Margin = new Thickness(5.0)
		};
		ScrollViewer val8 = new ScrollViewer
		{
			Height = 132.0,
			Content = _folderRows,
			VerticalScrollBarVisibility = (ScrollBarVisibility)1,
			Background = (IBrush)(object)Brushes.Transparent
		};
		Ui.SmoothScroll(val8, 70.0);
		Border val9 = new Border
		{
			Height = 132.0,
			Background = Skin.PanelAlt,
			BorderBrush = Skin.Border,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(8.0),
			Child = (Control)(object)val8,
			ClipToBounds = true
		};
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)val9);
		RefreshFolderRows();
		StackPanel val10 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 10.0
		};
		((AvaloniaList<Control>)(object)((Panel)val10).Children).Add((Control)(object)Ui.GhostButton(Loc.T("btn.removeSel"), RemoveSelected));
		_folderCount = Ui.T((_folders.Count == 0) ? Loc.T("settings.noFolders") : _folders.Count.ToString(), 11.5, Skin.TextDim, null, (TextWrapping)0);
		((Layoutable)_folderCount).Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
		((AvaloniaList<Control>)(object)((Panel)val10).Children).Add((Control)(object)_folderCount);
		if (settings_hasHidden())
		{
			Border val11 = Ui.GhostButton(Loc.F("settings.hiddenList", _original.HiddenGames.Count), delegate
			{
				this.HiddenListRequested?.Invoke();
			});
			((Layoutable)val11).Margin = new Thickness(12.0, 0.0, 0.0, 0.0);
			((AvaloniaList<Control>)(object)((Panel)val10).Children).Add((Control)(object)val11);
		}
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)val10);
		if (settings_hasHidden())
		{
			((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)Ui.T(Loc.T("settings.restoreHiddenHint"), 11.0, Skin.TextDim, null, (TextWrapping)1));
		}
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)val4);
		BuildAutoScanSection(val3);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.Divider());
		WrapPanel val12 = new WrapPanel
		{
			Orientation = (Orientation)0
		};
		StackPanel val13 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 9.0
		};
		((AvaloniaList<Control>)(object)((Panel)val13).Children).Add((Control)(object)Ui.Icon("M12,22c5.52,0,10-4.48,10-10S17.52,2,12,2S2,6.48,2,12c0,3.31,2.69,6,6,6h1.5c0.83,0,1.5,0.67,1.5,1.5c0,0.28-0.08,0.54-0.21,0.76C10.4,20.88,11.13,22,12,22z M6.5,11C5.67,11,5,10.33,5,9.5S5.67,8,6.5,8S8,8.67,8,9.5S7.33,11,6.5,11z M9.5,7C8.67,7,8,6.33,8,5.5S8.67,4,9.5,4S11,4.67,11,5.5S10.33,7,9.5,7z M14.5,7C13.67,7,13,6.33,13,5.5S13.67,4,14.5,4S16,4.67,16,5.5S15.33,7,14.5,7z M17.5,11c-0.83,0-1.5-0.67-1.5-1.5S16.67,8,17.5,8S19,8.67,19,9.5S18.33,11,17.5,11z", Skin.Accent, 17.0, 17.0));
		((AvaloniaList<Control>)(object)((Panel)val13).Children).Add((Control)(object)Ui.T(Loc.T("settings.customize"), 13.0, Skin.Text, (FontWeight)500, (TextWrapping)0));
		Border val14 = Ui.Clickable((Control)val13, (Action?)ShowCustomize, Skin.PanelAlt, Skin.CardHover, (CornerRadius?)new CornerRadius(6.0), (Thickness?)new Thickness(14.0, 9.0), (IBrush?)null, (Thickness?)null);
		((Layoutable)val14).Margin = new Thickness(0.0, 0.0, 26.0, 8.0);
		((AvaloniaList<Control>)(object)((Panel)val12).Children).Add((Control)(object)val14);
		TextBlock val15 = Ui.T(Loc.T("settings.language"), 13.0, Skin.TextMuted, null, (TextWrapping)0);
		((Layoutable)val15).Margin = new Thickness(0.0, 0.0, 10.0, 8.0);
		((AvaloniaList<Control>)(object)((Panel)val12).Children).Add((Control)(object)val15);
		_langCombo = new Dropdown(new string[2] { "Русский", "English" }, (_language == "en") ? 1 : 0, 150.0, 34.0, 13.0);
		((Layoutable)_langCombo).Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
		_langCombo.SelectionChanged += delegate(int index)
		{
			_language = ((index == 1) ? "en" : "ru");
		};
		((AvaloniaList<Control>)(object)((Panel)val12).Children).Add((Control)(object)_langCombo);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)val12);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.T(Loc.T("settings.customizeHint"), 11.0, Skin.TextDim, null, (TextWrapping)1));
		StackPanel val16 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 10.0
		};
		TextBlock val17 = Ui.T(Loc.T("settings.webSearch"), 13.0, Skin.TextMuted, null, (TextWrapping)0);
		((Layoutable)val17).VerticalAlignment = (VerticalAlignment)2;
		((AvaloniaList<Control>)(object)((Panel)val16).Children).Add((Control)(object)val17);
		string[] items = new string[3]
		{
			Loc.T("settings.engineGoogle"),
			Loc.T("settings.engineYandex"),
			Loc.T("settings.engineDdg")
		};
		string searchEngine = _searchEngine;
		int selectedIndex = ((searchEngine == "yandex") ? 1 : ((searchEngine == "ddg") ? 2 : 0));
		_engineCombo = new Dropdown(items, selectedIndex, 170.0, 34.0, 13.0);
		_engineCombo.SelectionChanged += delegate(int index)
		{
			_searchEngine = index switch
			{
				1 => "yandex", 
				2 => "ddg", 
				_ => "google", 
			};
		};
		((AvaloniaList<Control>)(object)((Panel)val16).Children).Add((Control)(object)_engineCombo);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)val16);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.T(Loc.T("settings.webSearchHint"), 11.0, Skin.TextDim, null, (TextWrapping)1));
		// Показываем, какой браузер Ludex нашёл сам: ссылки открываются в нём,
		// а не «где-то», куда их отправит система.
		Browser.Info browser = Browser.Detect();
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.T(
			browser.Found ? Loc.F("settings.browser", browser.Name) : Loc.T("settings.browserUnknown"),
			11.0, Skin.TextDim, null, (TextWrapping)1));
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.T(Loc.T("settings.sgdb"), 13.0, Skin.Text, (FontWeight)600, (TextWrapping)0));
		_keyBox = new TextBox
		{
			Text = _keyText,
			FontSize = 12.5,
			Height = 34.0,
			Background = Skin.PanelAlt,
			Foreground = Skin.Text,
			BorderBrush = Skin.Border,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(6.0),
			Padding = new Thickness(10.0, 0.0, 10.0, 0.0),
			VerticalContentAlignment = (VerticalAlignment)2,
			Watermark = "0123456789abcdef0123456789abcdef"
		};
		Ui.AnimateTextBox(_keyBox);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)_keyBox);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.T(Loc.T("settings.sgdbHint"), 11.0, Skin.TextDim, null, (TextWrapping)1));
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.T(Loc.T("settings.ignore"), 13.0, Skin.Text, (FontWeight)600, (TextWrapping)0));
		_ignoreBox = new TextBox
		{
			Text = _ignoreText,
			FontSize = 12.5,
			Height = 34.0,
			Background = Skin.PanelAlt,
			Foreground = Skin.Text,
			BorderBrush = Skin.Border,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(6.0),
			Padding = new Thickness(10.0, 0.0, 10.0, 0.0),
			VerticalContentAlignment = (VerticalAlignment)2,
			Watermark = "launcher, start, play"
		};
		Ui.AnimateTextBox(_ignoreBox);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)_ignoreBox);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.T(Loc.T("settings.ignoreHint"), 11.0, Skin.TextDim, null, (TextWrapping)1));
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.Divider());
		_autoScan = new Toggle(Loc.T("settings.autoscan"), _scanOnStartup, delegate(bool v)
		{
			_scanOnStartup = v;
		});
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)_autoScan);
		_autoStart = new Toggle(Loc.T("settings.autostart"), _runAtStartup, delegate(bool v)
		{
			_runAtStartup = v;
		}, Autostart.IsSupported);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)_autoStart);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.T(Autostart.IsSupported ? Loc.T("settings.autostartHint") : Loc.T("settings.autostartUnsupported"), 11.0, Skin.TextDim, null, (TextWrapping)1));
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.T(Loc.T("settings.tray.title"), 12.5, Skin.Text, (FontWeight)600, (TextWrapping)0));
		_closeToTray = new Toggle(Loc.T("settings.tray.closeToTray"), _closeOnTray, delegate(bool v)
		{
			_closeOnTray = v;
		});
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)_closeToTray);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.T(Loc.T("settings.tray.hint"), 11.0, Skin.TextDim, null, (TextWrapping)1));
		_autoUpdate = new Toggle(Loc.T("settings.autoupdate"), _checkUpdates, delegate(bool v)
		{
			_checkUpdates = v;
		});
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)_autoUpdate);
		Border val18 = Ui.GhostButton(Loc.T("settings.checkNow"), delegate
		{
			this.UpdateCheckRequested?.Invoke();
		});
		((Layoutable)val18).HorizontalAlignment = (HorizontalAlignment)1;
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)val18);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.T(Loc.T("settings.autoupdateHint"), 11.5, Skin.TextDim, null, (TextWrapping)1));
		Border val19 = Ui.GhostButton(Loc.T("settings.openData"), delegate
		{
			Shell.OpenFolder(AppPaths.Root);
		});
		((Layoutable)val19).HorizontalAlignment = (HorizontalAlignment)1;
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)val19);
		StackPanel val20 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 10.0,
			HorizontalAlignment = (HorizontalAlignment)3,
			Margin = new Thickness(26.0, 6.0, 26.0, 22.0)
		};
		((AvaloniaList<Control>)(object)((Panel)val20).Children).Add((Control)(object)Ui.GhostButton(Loc.T("btn.cancel"), delegate
		{
			this.Closed?.Invoke();
		}));
		((AvaloniaList<Control>)(object)((Panel)val20).Children).Add((Control)(object)Ui.GhostButton(Loc.T("btn.scan"), delegate
		{
			ScanRequested = true;
			Save();
		}));
		((AvaloniaList<Control>)(object)((Panel)val20).Children).Add((Control)(object)Ui.PrimaryButton(Loc.T("btn.save"), Save));
		DockPanel val21 = new DockPanel();
		DockPanel.SetDock((Control)(object)val, (Dock)3);
		((AvaloniaList<Control>)(object)((Panel)val21).Children).Add((Control)(object)val);
		DockPanel.SetDock((Control)(object)val20, (Dock)1);
		((AvaloniaList<Control>)(object)((Panel)val21).Children).Add((Control)(object)val20);
		ScrollViewer val22 = new ScrollViewer
		{
			Content = val3,
			VerticalScrollBarVisibility = (ScrollBarVisibility)1,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		Ui.SmoothScroll(val22);
		((AvaloniaList<Control>)(object)((Panel)val21).Children).Add((Control)(object)val22);
		((Decorator)this).Child = (Control)(object)val21;
	}

	private void Rebuild()
	{
		TextBox keyBox = _keyBox;
		_keyText = ((keyBox != null) ? keyBox.Text : null) ?? _keyText;
		TextBox ignoreBox = _ignoreBox;
		_ignoreText = ((ignoreBox != null) ? ignoreBox.Text : null) ?? _ignoreText;
		_theme = _original.Theme;
		((Border)this).Background = Skin.Panel;
		Control child = ((Decorator)this).Child;
		BuildContent();
		if (child is CustomizePanel)
		{
			Visual child2 = (Visual)(object)((Decorator)this).Child;
			if (child2 != null)
			{
				Ui.FadeSlideIn(child2, 0, 190, 6.0);
			}
		}
	}

	private void SwapChild(Control next)
	{
		((Decorator)this).Child = next;
		Ui.FadeSlideIn((Visual)(object)next, 0, 200, 8.0);
	}

	private void ShowCustomize()
	{
		CustomizePanel customizePanel = new CustomizePanel(_original);
		customizePanel.AppearanceChanged += delegate
		{
			this.AppearanceChanged?.Invoke();
		};
		customizePanel.WallpaperChanged += delegate
		{
			this.WallpaperChanged?.Invoke();
		};
		customizePanel.Closed += delegate
		{
			Rebuild();
			this.AppearanceChanged?.Invoke();
		};
		SwapChild((Control)(object)customizePanel);
	}

	private void RefreshFolders()
	{
		RefreshFolderRows();
		_folderCount.Text = ((_folders.Count == 0) ? Loc.T("settings.noFolders") : _folders.Count.ToString());
	}

	private void RefreshFolderRows()
	{
		if (_folderRows == null)
		{
			return;
		}
		((AvaloniaList<Control>)(object)((Panel)_folderRows).Children).Clear();
		if (_folders.Count == 0)
		{
			TextBlock val = Ui.T(Loc.T("settings.noFolders"), 12.0, Skin.TextDim, null, (TextWrapping)0);
			((Layoutable)val).Margin = new Thickness(8.0, 8.0, 8.0, 8.0);
			((AvaloniaList<Control>)(object)((Panel)_folderRows).Children).Add((Control)(object)val);
			return;
		}
		for (int i = 0; i < _folders.Count; i++)
		{
			int index = i;
			TextBlock val2 = Ui.T(_folders[i], 12.5, Skin.Text, (FontWeight)400, (TextWrapping)0);
			((Layoutable)val2).Margin = new Thickness(9.0, 7.0, 9.0, 7.0);
			Border val3 = new Border
			{
				Child = (Control)(object)val2
			};
			IBrush background = (IBrush)((index == _selectedFolder) ? ((object)Skin.AccentSoft) : ((object)Brushes.Transparent));
			val3.Background = background;
			val3.CornerRadius = new CornerRadius(6.0);
			((InputElement)val3).Cursor = new Cursor((StandardCursorType)9);
			Transitions val4 = new Transitions();
			((AvaloniaList<ITransition>)val4).Add((ITransition)new BrushTransition
			{
				Property = (AvaloniaProperty)(object)Border.BackgroundProperty,
				Duration = TimeSpan.FromMilliseconds(140.0)
			});
			((Animatable)val3).Transitions = val4;
			Border row = val3;
			((InputElement)row).PointerEntered += delegate
			{
				if (_selectedFolder != index)
				{
					row.Background = Skin.CardHover;
				}
			};
			((InputElement)row).PointerExited += delegate
			{
				Border obj = row;
				IBrush background2 = (IBrush)((_selectedFolder == index) ? ((object)Skin.AccentSoft) : ((object)Brushes.Transparent));
				obj.Background = background2;
			};
			((InputElement)row).PointerReleased += delegate(object? _, PointerReleasedEventArgs e)
			{
				if ((int)e.InitialPressMouseButton == 1)
				{
					((RoutedEventArgs)e).Handled = true;
					_selectedFolder = index;
					RefreshFolderRows();
				}
			};
			((AvaloniaList<Control>)(object)((Panel)_folderRows).Children).Add((Control)(object)row);
		}
	}

	private async Task AddFoldersAsync()
	{
		TopLevel topLevel = TopLevel.GetTopLevel((Visual)(object)this);
		if (topLevel == null)
		{
			return;
		}
		foreach (IStorageFolder item in await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
		{
			Title = Loc.T("settings.addFolder"),
			AllowMultiple = true
		}))
		{
			string path = StorageProviderExtensions.TryGetLocalPath((IStorageItem)(object)item);
			if (!string.IsNullOrEmpty(path))
			{
				path = path.TrimEnd('\\', '/');
				if (!_folders.Any((string f) => string.Equals(f, path, StringComparison.OrdinalIgnoreCase)))
				{
					_folders.Add(path);
				}
			}
		}
		RefreshFolders();
	}

	private void RemoveSelected()
	{
		if (_selectedFolder >= 0 && _selectedFolder < _folders.Count)
		{
			_folders.RemoveAt(_selectedFolder);
			if (_selectedFolder >= _folders.Count)
			{
				_selectedFolder = _folders.Count - 1;
			}
			RefreshFolders();
		}
	}

	private void BuildAutoScanSection(StackPanel body)
	{
		_foldersBadge = new Border
		{
			Background = Skin.Accent,
			CornerRadius = new CornerRadius(5.0),
			Padding = new Thickness(7.0, 2.0, 7.0, 3.0),
			IsVisible = false,
			VerticalAlignment = (VerticalAlignment)2,
			Child = (Control)(object)Ui.T(Loc.T("settings.autodetect.badge"), 10.5, Skin.OnAccent, (FontWeight)600, (TextWrapping)0)
		};
		StackPanel? foldersTitleRow = _foldersTitleRow;
		if (foldersTitleRow != null)
		{
			((AvaloniaList<Control>)(object)((Panel)foldersTitleRow).Children).Add((Control)(object)_foldersBadge);
		}
		((AvaloniaList<Control>)(object)((Panel)body).Children).Add((Control)(object)Ui.Divider());
		_autoDetect = new Toggle(Loc.T("settings.autodetect"), _autoScanGames, delegate(bool value)
		{
			_autoScanGames = value;
			ApplyAutoDetect();
			ScanRequested = true;
		});
		((AvaloniaList<Control>)(object)((Panel)body).Children).Add((Control)(object)_autoDetect);
		((AvaloniaList<Control>)(object)((Panel)body).Children).Add((Control)(object)Ui.T(Loc.T("settings.autodetectHint"), 11.0, Skin.TextDim, null, (TextWrapping)1));
		Border val = Ui.GhostButton(Loc.T("settings.cleanup"), delegate
		{
			this.CleanupRequested?.Invoke();
		}, Skin.TextMuted);
		((Layoutable)val).HorizontalAlignment = (HorizontalAlignment)1;
		((AvaloniaList<Control>)(object)((Panel)body).Children).Add((Control)(object)val);
		((AvaloniaList<Control>)(object)((Panel)body).Children).Add((Control)(object)Ui.T(Loc.T("settings.cleanupHint"), 11.0, Skin.TextDim, null, (TextWrapping)1));
		ApplyAutoDetect();
	}

	private void ApplyAutoDetect()
	{
		if (_foldersBlock != null)
		{
			bool autoScanGames = _autoScanGames;
			_foldersBlock.Opacity = (autoScanGames ? 0.4 : 1.0);
			((InputElement)_foldersBlock).IsHitTestVisible = !autoScanGames;
			if (_scanButton != null)
			{
				((InputElement)_scanButton).IsEnabled = true;
			}
			if (_foldersBadge != null)
			{
				_foldersBadge.IsVisible = autoScanGames;
			}
		}
	}

	private void Save()
	{
		_original.Folders = new List<string>(_folders);
		_original.Language = _language;
		_original.SearchEngine = _searchEngine;
		_original.Theme = _theme;
		_original.SteamGridDbKey = (string.IsNullOrWhiteSpace(_keyBox.Text) ? null : _keyBox.Text.Trim());
		_original.AutoScanOnStartup = _autoScan.Value;
		_original.CloseToTray = _closeToTray.Value;
		_original.CheckUpdatesOnStartup = _autoUpdate.Value;
		_original.AutoScan = _autoDetect.Value;
		_original.ExtraIgnoreExe = (from x in (_ignoreBox.Text ?? "").Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
			select x.Trim() into x
			where x.Length > 0
			select x).ToList();
		if (Autostart.IsSupported && Autostart.IsEnabled() != _runAtStartup && !Autostart.Set(_runAtStartup))
		{
			AppPaths.Log("Autostart toggle failed");
		}
		this.Saved?.Invoke();
	}
}
