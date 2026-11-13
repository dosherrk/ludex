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
using Path = Avalonia.Controls.Shapes.Path;

namespace Ludex.UI;

public sealed class MainWindow : Window
{
	private const double CardMinWidth = 164.0;

	private const double CardMaxWidth = 216.0;

	private const double CardGap = 18.0;

	// Порционная отрисовка библиотеки: строим первые карточки, остальные — по прокрутке.
	private List<GameEntry> _renderList = new List<GameEntry>();
	private int _renderedCount;
	private DispatcherTimer? _renderTimer;
	private const int InitialCards = 24;
	private const int CardsPerPortion = 32;
	private double _cardWidth = 176.0;

	private double _cardHeight = 264.0;

	private readonly AppSettings _settings;

	private readonly List<GameEntry> _games;

	private readonly Dictionary<string, Image> _covers = new Dictionary<string, Image>();

	private readonly Dictionary<string, Control> _placeholders = new Dictionary<string, Control>();

	private readonly Dictionary<string, TextBlock> _titles = new Dictionary<string, TextBlock>();

	private readonly Dictionary<string, TextBlock> _subtitles = new Dictionary<string, TextBlock>();

	private string _search = "";

	private bool _favoritesOnly;

	private bool _scanning;

	private CancellationTokenSource? _workCts;

	private WrapPanel _grid = new WrapPanel();

	private ScrollViewer _libraryScroll;

	private DispatcherTimer? _resizeTimer;

	private Control _emptyState;

	private Border _detailLayer;

	private TextBlock _countLabel;

	private Border _scanButton;

	private TextBox? _searchBox;

	private Border? _favChip;

	private TextBlock? _favChipLabel;

	private Border? _allChip;

	private TextBlock? _allLabel;

	private TextBlock? _emptyTitle;

	private TextBlock? _emptyHint;

	private DispatcherTimer? _searchTimer;

	private Grid _libraryHost;

	private Grid _recsHost;

	private Control? _recsView;

	private Border? _recChip;

	private TextBlock? _recLabel;

	private TextBlock? _sectionLabel;

	private bool _recsVisible;

	private int _recsFound;

	private Border _settingsLayer;

	private Border _launchLayer;

	private Border _revealHost;

	private Toast _processToast;

	private Toast _eventToast;

	private DispatcherTimer? _eventTimer;

	private DispatcherTimer? _toastWatchdog;

	private DispatcherTimer? _catalogNoteTimer;

	private IDisposable? _statusTimer;

	private TrayIcon? _trayIcon;

	private TrayMenu? _trayMenu;

	private bool _hiddenToTray;

	private bool _exiting;

	private string? _statusPending;

	private bool _statusSwitching;

	private readonly List<(string Key, string Text)> _processes = new List<(string, string)>();

	private CancellationTokenSource? _artCts;

	private SettingsPanel? _settingsPanel;

	private IntegrationsPanel? _integrationsPanel;

	private HiddenPanel? _hiddenPanel;

	private Path? _maximizePath;

	private Grid _wallpaperLayer;

	private DispatcherTimer? _launchTimer;

	private Stream? _wallpaperStream;

	private bool _appearanceDirty;

	private readonly Dictionary<string, Border> _starButtons = new Dictionary<string, Border>();

	private readonly Dictionary<string, Path> _starIcons = new Dictionary<string, Path>();

	private List<string>? _libraryIndex;

	private int _libraryIndexStamp = -1;

	private List<string> LibraryIndex
	{
		get
		{
			int count = _games.Count;
			if (_libraryIndex != null && _libraryIndexStamp == count)
			{
				return _libraryIndex;
			}
			List<string> list = new List<string>(_games.Count * 2);
			foreach (GameEntry game in _games)
			{
				string text = NormalizeTitle(game.Title);
				if (text.Length >= 3)
				{
					list.Add(text);
				}
				if (!string.IsNullOrEmpty(game.SteamName))
				{
					string text2 = NormalizeTitle(game.SteamName);
					if (text2.Length >= 3)
					{
						list.Add(text2);
					}
				}
			}
			_libraryIndex = list;
			_libraryIndexStamp = count;
			return list;
		}
	}

	public MainWindow()
	{
		System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
		Mark = phase => AppPaths.Log($"[запуск] {clock.ElapsedMilliseconds} мс — {phase}");

		_settings = Storage.LoadSettings();
		Loc.Lang = _settings.Language;
		Skin.Apply(_settings.Theme, _settings.Accent, _settings.Transparent);
		_games = Storage.LoadGames();
		Mark($"настройки и библиотека ({_games.Count} игр)");
		int num = PrunePhantomGames();
		if (num > 0)
		{
			Storage.SaveGames(_games);
			AppPaths.Log($"Убрано несуществующих игр: {num}");
		}
		int num2 = RemoveDuplicates();
		if (num2 > 0)
		{
			Storage.SaveGames(_games);
			AppPaths.Log($"Убрано дублей: {num2}");
		}
		_search = _settings.LastSearch ?? "";
		if (!_settings.LaunchersInitialized)
		{
			_settings.LaunchersInitialized = true;
			try
			{
				LauncherState launcherState = Launchers.ScanOne("steam");
				if (launcherState.Installed && launcherState.Count > 0)
				{
					_settings.Launchers.Add("steam");
				}
			}
			catch (Exception ex)
			{
				AppPaths.Log("Steam autodetect: " + ex.Message);
			}
			Storage.SaveSettings(_settings);
		}
		((Window)this).Title = "Ludex";
		((Layoutable)this).Width = Math.Max(900.0, _settings.WindowWidth);
		((Layoutable)this).Height = Math.Max(600.0, _settings.WindowHeight);
		((Layoutable)this).MinWidth = 880.0;
		((Layoutable)this).MinHeight = 560.0;
		((TemplatedControl)this).Background = Skin.Bg;
		((Window)this).WindowStartupLocation = (WindowStartupLocation)1;
		((Window)this).ExtendClientAreaToDecorationsHint = true;
		((Window)this).ExtendClientAreaChromeHints = (ExtendClientAreaChromeHints)0;
		((Window)this).ExtendClientAreaTitleBarHeightHint = -1.0;
		ApplyTransparency();
		if (_settings.WindowMaximized)
		{
			((Window)this).WindowState = (WindowState)2;
		}
		TrySetIcon();
		Mark("подготовка окна");
		BuildUi();
		Mark("BuildUi");
		RenderGrid();
		Mark("RenderGrid (сетка карточек)");
		Mark("конструктор завершён");
		((TopLevel)this).Opened += OnOpened;
		((Window)this).Closing += OnClosing;
		((Control)this).SizeChanged += OnWindowResized;
		((AvaloniaObject)this).PropertyChanged += delegate(object? _, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == (AvaloniaProperty)(object)Window.WindowStateProperty)
			{
				UpdateMaximizeIcon();
			}
		};
	}

	private static Image? LoadBrandImage(double size)
	{
		try
		{
			using Stream stream = AssetLoader.Open(new Uri("avares://Ludex/Assets/icon.png"), (Uri)null);
			Bitmap source = new Bitmap(stream);
			return new Image
			{
				Source = (IImage)(object)source,
				Width = size,
				Height = size,
				Stretch = (Stretch)2
			};
		}
		catch (Exception ex)
		{
			AppPaths.Log("Logo failed: " + ex.Message);
			return null;
		}
	}

	private void TrySetIcon()
	{
		try
		{
			using Stream stream = AssetLoader.Open(new Uri("avares://Ludex/Assets/icon.png"), (Uri)null);
			((Window)this).Icon = new WindowIcon(stream);
		}
		catch (Exception ex)
		{
			AppPaths.Log("Icon failed: " + ex.Message);
		}
	}

	private void BuildUi()
	{
		_covers.Clear();
		_placeholders.Clear();
		_titles.Clear();
		_subtitles.Clear();
		_starButtons.Clear();
		_starIcons.Clear();
		Grid val = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("Auto,*")
		};
		_wallpaperLayer = new Grid
		{
			IsHitTestVisible = false
		};
		Grid.SetColumnSpan((Control)(object)_wallpaperLayer, 2);
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)_wallpaperLayer);
		RefreshWallpaper();
		Mark("UI: обои");
		if (Skin.Transparent)
		{
			Grid val2 = new Grid
			{
				IsHitTestVisible = false
			};
			Grid.SetColumnSpan((Control)(object)val2, 2);
			Controls children = ((Panel)val2).Children;
			Border val3 = new Border();
			RadialGradientBrush val4 = new RadialGradientBrush
			{
				Center = new RelativePoint(0.72, -0.25, (RelativeUnit)0),
				GradientOrigin = new RelativePoint(0.72, -0.4, (RelativeUnit)0),
				RadiusX = new RelativeScalar(0.8, (RelativeUnit)0),
				RadiusY = new RelativeScalar(1.0, (RelativeUnit)0)
			};
			((AvaloniaList<GradientStop>)(object)((GradientBrush)val4).GradientStops).Add(new GradientStop(Skin.AccentColor, 0.0));
			((AvaloniaList<GradientStop>)(object)((GradientBrush)val4).GradientStops).Add(new GradientStop(Color.FromArgb((byte)0, ((Color)Skin.AccentColor).R, ((Color)Skin.AccentColor).G, ((Color)Skin.AccentColor).B), 1.0));
			val3.Background = (IBrush)val4;
			val3.Opacity = (Skin.IsLight ? 0.08 : 0.07);
			((AvaloniaList<Control>)(object)children).Add((Control)val3);
			((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val2);
		}
		Control val5 = BuildSidebar();
		Mark("UI: сайдбар");
		Grid.SetColumn(val5, 0);
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add(val5);
		Grid val6 = new Grid
		{
			RowDefinitions = new RowDefinitions("Auto,*,Auto")
		};
		Grid.SetColumn((Control)(object)val6, 1);
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val6);
		((AvaloniaList<Control>)(object)((Panel)val6).Children).Add(BuildTitleStrip());
		Mark("UI: шапка");
		Grid val7 = new Grid();
		Grid val8 = val7;
		_libraryHost = val7;
		Grid val9 = val8;
		_grid = new WrapPanel
		{
			Orientation = (Orientation)0,
			Margin = new Thickness(2.0)
		};
		_libraryScroll = new ScrollViewer
		{
			Content = _grid,
			Padding = new Thickness(20.0, 4.0, 14.0, 12.0),
			HorizontalScrollBarVisibility = (ScrollBarVisibility)0,
			VerticalScrollBarVisibility = (ScrollBarVisibility)1
		};
		Ui.SmoothScroll(_libraryScroll, 130.0);
		// Страховка для порционной отрисовки: если пользователь прокрутил до низа
		// раньше, чем достроилась очередная порция, достраиваем сразу.
		_libraryScroll.ScrollChanged += delegate
		{
			if (_renderedCount < _renderList.Count && _libraryScroll.Offset.Y + _libraryScroll.Viewport.Height >= _libraryScroll.Extent.Height - 200.0)
			{
				AddCards(_renderList, CardsPerPortion);
			}
		};
		((AvaloniaList<Control>)(object)((Panel)val9).Children).Add((Control)(object)_libraryScroll);
		_emptyState = BuildEmptyState();
		((AvaloniaList<Control>)(object)((Panel)val9).Children).Add(_emptyState);
		Mark("UI: библиотека+пустое состояние");
		_detailLayer = new Border
		{
			Background = Skin.Overlay,
			IsVisible = false
		};
		((InputElement)_detailLayer).PointerPressed += delegate(object? _, PointerPressedEventArgs e)
		{
			if (((RoutedEventArgs)e).Source == _detailLayer)
			{
				_detailLayer.IsVisible = false;
			}
		};
		((AvaloniaList<Control>)(object)((Panel)val9).Children).Add((Control)(object)_detailLayer);
		Grid.SetRow((Control)(object)val9, 1);
		((AvaloniaList<Control>)(object)((Panel)val6).Children).Add((Control)(object)val9);
		_recsHost = new Grid
		{
			IsVisible = false
		};
		_recsView = null;
		Grid.SetRow((Control)(object)_recsHost, 1);
		((AvaloniaList<Control>)(object)((Panel)val6).Children).Add((Control)(object)_recsHost);
		Control val10 = BuildStatusBar();
		Mark("UI: статус-бар");
		Grid.SetRow(val10, 2);
		((AvaloniaList<Control>)(object)((Panel)val6).Children).Add(val10);
		Border val11 = new Border
		{
			Background = Skin.Overlay,
			IsVisible = false
		};
		Transitions val12 = new Transitions();
		((AvaloniaList<ITransition>)val12).Add((ITransition)new DoubleTransition
		{
			Property = (AvaloniaProperty)(object)Visual.OpacityProperty,
			Duration = TimeSpan.FromMilliseconds(200.0),
			Easing = (Easing)new CubicEaseOut()
		});
		((Animatable)val11).Transitions = val12;
		_settingsLayer = val11;
		((InputElement)_settingsLayer).PointerPressed += delegate(object? _, PointerPressedEventArgs e)
		{
			if (((RoutedEventArgs)e).Source == _settingsLayer)
			{
				CloseSettings();
			}
		};
		Grid.SetColumnSpan((Control)(object)_settingsLayer, 2);
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)_settingsLayer);
		Border val13 = new Border
		{
			Background = Skin.Overlay,
			IsVisible = false
		};
		Transitions val14 = new Transitions();
		((AvaloniaList<ITransition>)val14).Add((ITransition)new DoubleTransition
		{
			Property = (AvaloniaProperty)(object)Visual.OpacityProperty,
			Duration = TimeSpan.FromMilliseconds(210.0),
			Easing = (Easing)new CubicEaseOut()
		});
		((Animatable)val13).Transitions = val14;
		_launchLayer = val13;
		((InputElement)_launchLayer).PointerPressed += delegate(object? _, PointerPressedEventArgs e)
		{
			((RoutedEventArgs)e).Handled = true;
		};
		Grid.SetColumnSpan((Control)(object)_launchLayer, 2);
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)_launchLayer);
		_revealHost = new Border
		{
			Child = (Control)(object)val,
			RenderTransformOrigin = RelativePoint.Center,
			ClipToBounds = false
		};
		Mark("UI: слои");
		((ContentControl)this).Content = _revealHost;
		UpdateCount();
		ApplyFavChipState();
		if (_recsVisible)
		{
			EnsureRecsView();
			_libraryHost.IsVisible = false;
			_recsHost.IsVisible = true;
			if (_sectionLabel != null)
			{
				_sectionLabel.Text = Loc.T("nav.recs");
			}
			ShowRecsCount();
		}
		else if (_sectionLabel != null)
		{
			_sectionLabel.Text = Loc.T("nav.library");
		}
	}

	private Control BuildSidebar()
	{
		Border val = new Border
		{
			Width = 244.0,
			Background = Skin.BgDeep
		};
		DockPanel val2 = new DockPanel
		{
			LastChildFill = true
		};
		StackPanel val3 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 10.0,
			Margin = new Thickness(20.0, 22.0, 20.0, 16.0)
		};
		Image val4 = LoadBrandImage(22.0);
		if (val4 != null)
		{
			((Layoutable)val4).VerticalAlignment = (VerticalAlignment)2;
			((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)val4);
		}
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.T(Loc.T("app.name"), 15.0, Skin.Text, (FontWeight)700, (TextWrapping)0));
		DockPanel.SetDock((Control)(object)val3, (Dock)3);
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)val3);
		StackPanel val5 = new StackPanel
		{
			Spacing = 6.0,
			Margin = new Thickness(14.0, 10.0, 14.0, 14.0)
		};
		TextBlock val6 = Ui.T(Loc.T("btn.scan"), 13.0, Skin.OnAccent, (FontWeight)600, (TextWrapping)0);
		((Layoutable)val6).HorizontalAlignment = (HorizontalAlignment)2;
		_scanButton = Ui.Clickable((Control)(object)val6, (Action?)delegate
		{
			StartScanAsync();
		}, Skin.Accent, Skin.AccentHover, (CornerRadius?)new CornerRadius(4.0), (Thickness?)new Thickness(16.0, 11.0), (IBrush?)null, (Thickness?)null);
		((Layoutable)_scanButton).HorizontalAlignment = (HorizontalAlignment)0;
		((Layoutable)_scanButton).MinHeight = 42.0;
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)_scanButton);
		{
			// Ручное добавление: автопоиск ошибается, и человеку нужен способ добавить игру самому.
			TextBlock addLabel = Ui.T(Loc.T("btn.addGame"), 12.5, Skin.TextMuted, FontWeight.Medium, TextWrapping.NoWrap);
			((Layoutable)addLabel).HorizontalAlignment = (HorizontalAlignment)2;
			Border addButton = Ui.Clickable((Control)(object)addLabel, (Action?)delegate
			{
				AddGameManuallyAsync();
			}, Skin.PanelAlt, Skin.CardHover, (CornerRadius?)new CornerRadius(8.0), (Thickness?)new Thickness(16.0, 10.0), (IBrush?)null, (Thickness?)null);
			((Layoutable)addButton).HorizontalAlignment = (HorizontalAlignment)0;
			((Layoutable)addButton).MinHeight = 38.0;
			((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)addButton);
		}
		Border item = SidebarItem(Loc.T("btn.settings"), (Control)(object)Ui.Icon("M19.14,12.94c0.04-0.3,0.06-0.61,0.06-0.94c0-0.32-0.02-0.64-0.07-0.94l2.03-1.58c0.18-0.14,0.23-0.41,0.12-0.61l-1.92-3.32c-0.12-0.22-0.37-0.29-0.59-0.22l-2.39,0.96c-0.5-0.38-1.03-0.7-1.62-0.94L14.4,2.81c-0.04-0.24-0.24-0.41-0.48-0.41h-3.84c-0.24,0-0.43,0.17-0.47,0.41L9.25,5.35C8.66,5.59,8.12,5.92,7.63,6.29L5.24,5.33c-0.22-0.08-0.47,0-0.59,0.22L2.74,8.87C2.62,9.08,2.66,9.34,2.86,9.48l2.03,1.58C4.84,11.36,4.8,11.69,4.8,12s0.02,0.64,0.07,0.94l-2.03,1.58c-0.18,0.14-0.23,0.41-0.12,0.61l1.92,3.32c0.12,0.22,0.37,0.29,0.59,0.22l2.39-0.96c0.5,0.38,1.03,0.7,1.62,0.94l0.36,2.54c0.05,0.24,0.24,0.41,0.48,0.41h3.84c0.24,0,0.44-0.17,0.47-0.41l0.36-2.54c0.59-0.24,1.13-0.56,1.62-0.94l2.39,0.96c0.22,0.08,0.47,0,0.59-0.22l1.92-3.32c0.12-0.22,0.07-0.47-0.12-0.61L19.14,12.94z M12,15.6c-1.98,0-3.6-1.62-3.6-3.6s1.62-3.6,3.6-3.6s3.6,1.62,3.6,3.6S13.98,15.6,12,15.6z", Skin.TextMuted, 16.0, 16.0), ShowSettings).Frame;
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)item);
		Border item2 = SidebarItem(Loc.T("btn.integrations"), (Control)(object)Ui.Icon("M7.5,12.5 L12.5,7.5 M8.6,4.6 L10.4,2.8 A3.9,3.9 0 0 1 15.9,8.3 L14.1,10.1 M5.9,9.9 L4.1,11.7 A3.9,3.9 0 0 0 9.6,17.2 L11.4,15.4", Skin.TextMuted, 16.0, 16.0), ShowIntegrations).Frame;
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)item2);
		TextBlock val7 = Ui.T("v" + Updater.CurrentVersionText, 11.0, Skin.TextDim, null, (TextWrapping)0);
		((Layoutable)val7).Margin = new Thickness(14.0, 6.0, 0.0, 0.0);
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)val7);
		DockPanel.SetDock((Control)(object)val5, (Dock)1);
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)val5);
		StackPanel val8 = new StackPanel
		{
			Spacing = 2.0
		};
		(Border Frame, TextBlock Label) tuple = SidebarItem(Loc.T("nav.recs"), (Control)(object)Ui.Icon("M10,0 L12.1,7.9 L20,10 L12.1,12.1 L10,20 L7.9,12.1 L0,10 L7.9,7.9 Z", Skin.TextMuted, 15.0, 15.0), ShowRecommendations);
		Border item3 = tuple.Frame;
		TextBlock item4 = tuple.Label;
		_recChip = item3;
		_recLabel = item4;
		((Layoutable)_recChip).Margin = new Thickness(8.0, 12.0, 8.0, 10.0);
		((AvaloniaList<Control>)(object)((Panel)val8).Children).Add((Control)(object)_recChip);
		_searchBox = Ui.SearchBox(_search, Loc.T("search.watermark"), delegate(string text)
		{
			_search = text ?? "";
			_settings.LastSearch = _search;
			DispatcherTimer? searchTimer = _searchTimer;
			if (searchTimer != null)
			{
				searchTimer.Stop();
			}
			_searchTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(110.0)
			};
			_searchTimer.Tick += delegate
			{
				DispatcherTimer? searchTimer2 = _searchTimer;
				if (searchTimer2 != null)
				{
					searchTimer2.Stop();
				}
				RenderGrid(animated: true);
			};
			_searchTimer.Start();
		});
		((Layoutable)_searchBox).Height = 40.0;
		((TemplatedControl)_searchBox).CornerRadius = new CornerRadius(4.0);
		((Layoutable)_searchBox).Margin = new Thickness(14.0, 0.0, 14.0, 12.0);
		((AvaloniaList<Control>)(object)((Panel)val8).Children).Add((Control)(object)_searchBox);
		((AvaloniaList<Control>)(object)((Panel)val8).Children).Add((Control)(object)Caption("nav.library"));
		(Border Frame, TextBlock Label) tuple2 = SidebarItem(Loc.T("nav.all"), (Control)(object)Ui.Icon("M0,0 H8.4 V8.4 H0 Z M11.6,0 H20 V8.4 H11.6 Z M0,11.6 H8.4 V20 H0 Z M11.6,11.6 H20 V20 H11.6 Z", Skin.TextMuted, 16.0, 16.0), delegate
		{
			ShowLibrary();
			_favoritesOnly = false;
			ApplyFavChipState();
			RenderGrid(animated: true);
		});
		Border item5 = tuple2.Frame;
		TextBlock item6 = tuple2.Label;
		_allChip = item5;
		_allLabel = item6;
		((AvaloniaList<Control>)(object)((Panel)val8).Children).Add((Control)(object)item5);
		_favChipLabel = Ui.T(Loc.T("filter.fav"), 13.0, Skin.TextMuted, (FontWeight)500, (TextWrapping)0);
		StackPanel val9 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 12.0,
			VerticalAlignment = (VerticalAlignment)2
		};
		((AvaloniaList<Control>)(object)((Panel)val9).Children).Add((Control)(object)Ui.Icon("M10,0 L12.6,6.6 L19.6,7.2 L14.3,11.9 L15.9,18.9 L10,15.3 L4.1,18.9 L5.7,11.9 L0.4,7.2 L7.4,6.6 Z", Skin.TextMuted, 15.0, 15.0));
		((AvaloniaList<Control>)(object)((Panel)val9).Children).Add((Control)(object)_favChipLabel);
		_favChip = Ui.Clickable((Control)(object)val9, (Action?)delegate
		{
			ShowLibrary();
			_favoritesOnly = !_favoritesOnly;
			ApplyFavChipState();
			RenderGrid(animated: true);
		}, (IBrush?)(object)Brushes.Transparent, Skin.PanelAlt, (CornerRadius?)new CornerRadius(4.0), (Thickness?)new Thickness(12.0, 10.0), (IBrush?)null, (Thickness?)null);
		((Layoutable)_favChip).HorizontalAlignment = (HorizontalAlignment)0;
		((Layoutable)_favChip).Margin = new Thickness(8.0, 1.0, 8.0, 1.0);
		((AvaloniaList<Control>)(object)((Panel)val8).Children).Add((Control)(object)_favChip);
		((AvaloniaList<Control>)(object)((Panel)val8).Children).Add((Control)(object)Caption("nav.sort"));
		string[] items = new string[4]
		{
			Loc.T("sort.title"),
			Loc.T("sort.added"),
			Loc.T("sort.played"),
			Loc.T("sort.favorite")
		};
		Dropdown dropdown = new Dropdown(items, _settings.SortMode switch
		{
			"added" => 1, 
			"played" => 2, 
			"favorite" => 3, 
			_ => 0, 
		}, 216.0, 38.0);
		dropdown.SelectionChanged += delegate(int index)
		{
			AppSettings settings = _settings;
			settings.SortMode = index switch
			{
				1 => "added", 
				2 => "played", 
				3 => "favorite", 
				_ => "title", 
			};
			Storage.SaveSettings(_settings);
			RenderGrid(animated: true);
		};
		((Layoutable)dropdown).Margin = new Thickness(8.0, 0.0, 8.0, 0.0);
		((AvaloniaList<Control>)(object)((Panel)val8).Children).Add((Control)(object)dropdown);
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)val8);
		((Decorator)val).Child = (Control)(object)val2;
		return (Control)(object)val;
	}

	private static (Border Frame, TextBlock Label) SidebarItem(string text, Control icon, Action onClick)
	{
		TextBlock val = Ui.T(text, 13.0, Skin.TextMuted, (FontWeight)500, (TextWrapping)0);
		StackPanel val2 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 12.0,
			VerticalAlignment = (VerticalAlignment)2
		};
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add(icon);
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)val);
		Border obj = Ui.Clickable((Control)val2, onClick, (IBrush?)(object)Brushes.Transparent, Skin.PanelAlt, (CornerRadius?)new CornerRadius(4.0), (Thickness?)new Thickness(12.0, 10.0), (IBrush?)null, (Thickness?)null);
		((Layoutable)obj).HorizontalAlignment = (HorizontalAlignment)0;
		((Layoutable)obj).Margin = new Thickness(8.0, 1.0, 8.0, 1.0);
		return (Frame: obj, Label: val);
	}

	private static TextBlock Caption(string key)
	{
		TextBlock obj = Ui.T(Loc.T(key).ToUpperInvariant(), 10.5, Skin.TextDim, (FontWeight)600, (TextWrapping)0);
		((Layoutable)obj).Margin = new Thickness(20.0, 14.0, 20.0, 6.0);
		return obj;
	}

	private Control BuildTitleStrip()
	{
		Border strip = new Border
		{
			Height = 48.0,
			Background = (IBrush)(object)Brushes.Transparent,
			Padding = new Thickness(22.0, 0.0, 6.0, 0.0)
		};
		((InputElement)strip).PointerPressed += delegate(object? _, PointerPressedEventArgs e)
		{
			PointerPoint currentPoint = ((PointerEventArgs)e).GetCurrentPoint((Visual)(object)strip);
			PointerPointProperties properties = ((PointerPoint)currentPoint).Properties;
			if (!((PointerPointProperties)properties).IsLeftButtonPressed || IsInteractive((Control)strip))
			{
				return;
			}
			try
			{
				((Window)this).BeginMoveDrag(e);
			}
			catch
			{
			}
		};
		((InputElement)strip).DoubleTapped += delegate
		{
			ToggleMaximize();
		};
		DockPanel val = new DockPanel
		{
			VerticalAlignment = (VerticalAlignment)2
		};
		StackPanel val2 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 2.0,
			VerticalAlignment = (VerticalAlignment)2
		};
		DockPanel.SetDock((Control)(object)val2, (Dock)2);
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)WindowButton((Control)(object)Ui.Icon("M0,0 H13 V2.4 H0 Z", Skin.TextMuted, 13.0, 12.0), Minimize, Loc.T("win.minimize")));
		_maximizePath = Ui.Icon("M0.6,0.6 H11.4 V11.4 H0.6 Z", Skin.TextMuted, 11.0, 11.0, stroke: true, 1.4);
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)WindowButton((Control)(object)_maximizePath, ToggleMaximize, Loc.T("win.maximize")));
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)WindowButton((Control)(object)Ui.Icon("M1,1 L11,11 M11,1 L1,11", Skin.TextMuted, 11.0, 11.0, stroke: true, 1.5), (Action)base.Close, Loc.T("win.close"), danger: true));
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val2);
		StackPanel val3 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 12.0,
			VerticalAlignment = (VerticalAlignment)2
		};
		_sectionLabel = Ui.T(Loc.T("nav.library"), 15.0, Skin.Text, (FontWeight)600, (TextWrapping)0);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)_sectionLabel);
		_countLabel = Ui.T("", 12.0, Skin.TextDim, null, (TextWrapping)0);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)_countLabel);
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val3);
		((Decorator)strip).Child = (Control)(object)val;
		return (Control)(object)strip;
	}

	private Border WindowButton(Control icon, Action onClick, string tip, bool danger = false)
	{
		IBrush val = (IBrush)(danger ? ((object)new SolidColorBrush(Color.Parse("#E81123"), 1.0)) : ((object)Skin.CardHover));
		IBrush hover = val;
		Border val2 = Ui.Clickable(icon, onClick, (IBrush?)(object)Brushes.Transparent, hover, (CornerRadius?)new CornerRadius(4.0), (Thickness?)new Thickness(0.0), (IBrush?)null, (Thickness?)null);
		((Layoutable)val2).Width = 44.0;
		((Layoutable)val2).Height = 28.0;
		if (danger)
		{
			Path path = (Path)(object)((icon is Path) ? icon : null);
			if (path != null)
			{
				Path obj = path;
				Transitions val3 = new Transitions();
				((AvaloniaList<ITransition>)val3).Add((ITransition)new BrushTransition
				{
					Property = (AvaloniaProperty)(object)Shape.StrokeProperty,
					Duration = TimeSpan.FromMilliseconds(150.0)
				});
				((Animatable)obj).Transitions = val3;
				((InputElement)val2).PointerEntered += delegate
				{
					((Shape)path).Stroke = (IBrush)(object)Brushes.White;
				};
				((InputElement)val2).PointerExited += delegate
				{
					((Shape)path).Stroke = Skin.TextMuted;
				};
			}
		}
		Ui.Tip((Control)(object)val2, tip);
		return val2;
	}

	private void Minimize()
	{
		((Window)this).WindowState = (WindowState)1;
	}

	private void ToggleMaximize()
	{
		((Window)this).WindowState = (WindowState)(((int)((Window)this).WindowState != 2) ? 2 : 0);
	}

	private void UpdateMaximizeIcon()
	{
		if (_maximizePath != null)
		{
			_maximizePath.Data = Geometry.Parse(((int)((Window)this).WindowState == 2) ? "M2.6,0.6 H11.4 V9.4 M0.6,2.6 H9.4 V11.4 H0.6 Z" : "M0.6,0.6 H11.4 V11.4 H0.6 Z");
		}
	}

	private static bool IsInteractive(Control? source)
	{
		Control val = source;
		int num = 0;
		while (val != null && num++ < 14)
		{
			if (val.Tag as string == "clickable")
			{
				return true;
			}
			if (val is TextBox || val is ComboBox || val is Button || val is ListBox || val is ScrollViewer || val is Dropdown)
			{
				return true;
			}
			StyledElement parent = ((StyledElement)val).Parent;
			val = parent as Control;
		}
		return false;
	}

	private Control BuildStatusBar()
	{
		Canvas val = new Canvas
		{
			Height = 0.0,
			ClipToBounds = false
		};
		Toast toast = new Toast(ToastKind.Process);
		toast.IsVisible = false;
		_processToast = toast;
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)_processToast);
		Toast toast2 = new Toast(ToastKind.Event);
		toast2.IsVisible = false;
		_eventToast = toast2;
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)_eventToast);
		_statusPending = null;
		_statusSwitching = false;
		DispatcherTimer? eventTimer = _eventTimer;
		if (eventTimer != null)
		{
			eventTimer.Stop();
		}
		_eventTimer = null;
		((Control)val).SizeChanged += delegate
		{
			LayoutToasts();
		};
		_processToast.Changed += LayoutToasts;
		_eventToast.Changed += LayoutToasts;
		// Нажатие по уведомлению убирает его сразу: гасим таймер, иначе плашка
		// «вернулась» бы через несколько секунд или осталась висеть анимацией.
		_eventToast.Dismissed += delegate
		{
			DispatcherTimer? timer = _eventTimer;
			if (timer != null)
			{
				timer.Stop();
			}
			_eventTimer = null;
		};
		_processToast.Dismissed += delegate
		{
			_processes.Clear();
			DispatcherTimer? timer = _toastWatchdog;
			if (timer != null)
			{
				timer.Stop();
			}
		};
		Grid.SetRow((Control)val, 2);
		return (Control)val;
	}

	private void LayoutToasts()
	{
		if (_processToast == null || _eventToast == null)
		{
			return;
		}
		StyledElement parent = ((StyledElement)_processToast).Parent;
		Control val = (Control)(object)((parent is Control) ? parent : null);
		if (val == null)
		{
			return;
		}
		Rect bounds = val.Bounds;
		double width = ((Rect)bounds).Width;
		if (width <= 0.0)
		{
			return;
		}
		double num = Math.Max(240.0, Math.Min(520.0, width * 0.5));
		((Layoutable)_processToast).Measure(new Size(num, double.PositiveInfinity));
		Size desiredSize = ((Layoutable)_processToast).DesiredSize;
		double num2 = Math.Min(num, Math.Max(0.0, ((Size)desiredSize).Width));
		Canvas.SetLeft((AvaloniaObject)(object)_processToast, Math.Max(16.0, (width - num2) / 2.0));
		((Layoutable)_eventToast).Measure(new Size(num, double.PositiveInfinity));
		desiredSize = ((Layoutable)_eventToast).DesiredSize;
		double num3 = Math.Min(num, Math.Max(0.0, ((Size)desiredSize).Width));
		Canvas.SetLeft((AvaloniaObject)(object)_eventToast, Math.Max(16.0, width - num3 - 20.0));
		Toast[] array = new Toast[2] { _processToast, _eventToast };
		foreach (Toast toast in array)
		{
			Canvas.SetTop((AvaloniaObject)(object)toast, 0.0);
			bounds = toast.Bounds;
			double height;
			if (!(((Rect)bounds).Height > 0.0))
			{
				desiredSize = ((Layoutable)toast).DesiredSize;
				height = ((Size)desiredSize).Height;
			}
			else
			{
				bounds = toast.Bounds;
				height = ((Rect)bounds).Height;
			}
			double num4 = height + 12.0;
			toast.RenderTransform = (ITransform)new TranslateTransform(0.0, 0.0 - num4);
		}
	}

	private Control BuildEmptyState()
	{
		StackPanel val = new StackPanel
		{
			Spacing = 14.0,
			HorizontalAlignment = (HorizontalAlignment)2,
			VerticalAlignment = (VerticalAlignment)2,
			MaxWidth = 560.0
		};
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)new Border
		{
			Width = 84.0,
			Height = 84.0,
			CornerRadius = new CornerRadius(22.0),
			Background = Skin.AccentSoft,
			HorizontalAlignment = (HorizontalAlignment)2,
			Child = (Control)(object)Ui.Icon("M0,0 L15,8.5 L0,17 Z", (IBrush)(object)Brushes.White, 30.0, 34.0)
		});
		_emptyTitle = Ui.T(Loc.T("empty.title"), 20.0, Skin.Text, (FontWeight)600, (TextWrapping)0);
		((Layoutable)_emptyTitle).HorizontalAlignment = (HorizontalAlignment)2;
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)_emptyTitle);
		TextBlock val2 = (_emptyHint = Ui.T(Loc.T("empty.hint"), 13.0, Skin.TextMuted, null, (TextWrapping)1));
		val2.TextAlignment = (TextAlignment)1;
		((Layoutable)val2).HorizontalAlignment = (HorizontalAlignment)2;
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val2);
		Border val3 = Ui.PrimaryButton(Loc.T("empty.btn"), ShowSettings);
		((Layoutable)val3).HorizontalAlignment = (HorizontalAlignment)2;
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val3);
		Grid val4 = new Grid();
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)val);
		return (Control)val4;
	}

	private IEnumerable<GameEntry> VisibleGames()
	{
		IEnumerable<GameEntry> source = _games;
		if (!string.IsNullOrWhiteSpace(_search))
		{
			string s = _search.Trim();
			source = source.Where((GameEntry g) => g.Title.Contains(s, StringComparison.CurrentCultureIgnoreCase) || g.FolderName.Contains(s, StringComparison.OrdinalIgnoreCase) || (g.SteamName ?? "").Contains(s, StringComparison.CurrentCultureIgnoreCase));
		}
		if (_favoritesOnly)
		{
			source = source.Where((GameEntry g) => g.Favorite);
		}
		return (_settings.SortMode switch
		{
			"added" => source.OrderByDescending((GameEntry g) => g.AddedUtc), 
			"played" => source.OrderByDescending((GameEntry g) => g.LastPlayedUtc ?? DateTime.MinValue), 
			"favorite" => source.OrderByDescending((GameEntry g) => g.Favorite).ThenBy<GameEntry, string>((GameEntry g) => g.Title, StringComparer.CurrentCultureIgnoreCase), 
			_ => source.OrderBy<GameEntry, string>((GameEntry g) => g.Title, StringComparer.CurrentCultureIgnoreCase), 
		}).ToList();
	}

	private void RenderGrid()
	{
		RenderGrid(animated: false);
	}

	private void UpdateCardMetrics()
	{
		WrapPanel grid = _grid;
		double num;
		Rect bounds;
		if (grid == null)
		{
			num = 0.0;
		}
		else
		{
			bounds = grid.Bounds;
			num = ((Rect)bounds).Width;
		}
		double num2 = num;
		if (num2 <= 1.0)
		{
			bounds = this.Bounds;
			double num3;
			if (!(((Rect)bounds).Width > 1.0))
			{
				num3 = 1280.0;
			}
			else
			{
				bounds = this.Bounds;
				num3 = ((Rect)bounds).Width;
			}
			num2 = num3 - 32.0;
		}
		num2 -= 2.0;
		int num4 = Math.Max(1, (int)Math.Floor(num2 / 182.0));
		double value = Math.Floor(num2 / (double)num4) - 18.0;
		_cardHeight = Math.Round((_cardWidth = Math.Clamp(value, 164.0, 216.0)) * 1.5);
	}

	private void OnWindowResized(object? sender, SizeChangedEventArgs e)
	{
		DispatcherTimer? resizeTimer = _resizeTimer;
		if (resizeTimer != null)
		{
			resizeTimer.Stop();
		}
		_resizeTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(90.0)
		};
		_resizeTimer.Tick += delegate
		{
			DispatcherTimer? resizeTimer2 = _resizeTimer;
			if (resizeTimer2 != null)
			{
				resizeTimer2.Stop();
			}
			double cardWidth = _cardWidth;
			UpdateCardMetrics();
			if (Math.Abs(cardWidth - _cardWidth) > 0.5)
			{
				RenderGrid(animated: false);
			}
		};
		_resizeTimer.Start();
	}

	private void RenderGrid(bool animated)
	{
		_covers.Clear();
		_placeholders.Clear();
		_titles.Clear();
		_subtitles.Clear();
		((AvaloniaList<Control>)(object)((Panel)_grid).Children).Clear();
		UpdateCardMetrics();
		List<GameEntry> list = VisibleGames().ToList();
		_renderList = list;
		_renderedCount = 0;
		_renderTimer?.Stop();
		_renderTimer = null;
		AddCards(list, Math.Min(list.Count, InitialCards));
		bool isVisible = _emptyState.IsVisible;
		_emptyState.IsVisible = list.Count == 0;
		if (_emptyState.IsVisible && _emptyTitle != null && _emptyHint != null)
		{
			_emptyTitle.Text = Loc.T(_favoritesOnly ? "empty.favTitle" : "empty.title");
			_emptyHint.Text = Loc.T(_favoritesOnly ? "empty.favHint" : "empty.hint");
		}
		if (_emptyState.IsVisible && !isVisible)
		{
			Ui.FadeSlideIn((Visual)(object)_emptyState);
		}
		UpdateCount();
		if (animated && list.Count > 0)
		{
			_grid.RenderTransform = (ITransform)new TranslateTransform(0.0, 14.0);
			WrapPanel grid = _grid;
			Transitions val = new Transitions();
			((AvaloniaList<ITransition>)val).Add((ITransition)new TransformOperationsTransition
			{
				Property = (AvaloniaProperty)(object)Visual.RenderTransformProperty,
				Duration = TimeSpan.FromMilliseconds(240.0),
				Easing = (Easing)new CubicEaseOut()
			});
			((Animatable)grid).Transitions = val;
			Dispatcher.UIThread.Post((Action)delegate
			{
				_grid.RenderTransform = (ITransform)new TranslateTransform(0.0, 0.0);
			}, default(DispatcherPriority));
		}
	}

	/// <summary>
	/// Достраивает очередную порцию карточек. Строить сразу все — это ~2 мс на карточку
	/// (300 игр = 0,6 с на старте), а видно пользователю всё равно 15–20 штук.
	/// </summary>
	private void AddCards(List<GameEntry> list, int count)
	{
		int limit = Math.Min(list.Count, _renderedCount + count);
		if (limit <= _renderedCount)
		{
			return;
		}
		Control? last = null;
		for (int i = _renderedCount; i < limit; i++)
		{
			last = BuildCard(list[i], i);
			((AvaloniaList<Control>)(object)((Panel)_grid).Children).Add(last);
		}
		_renderedCount = limit;
		if (last != null && _renderedCount < list.Count)
		{
			// Продолжаем, как только предыдущая порция реально привязалась к экрану:
			// в обычном режиме это доли секунды, а первый кадр уже показан.
			AttachNextPortion(last);
		}
	}

	private void AttachNextPortion(Control anchor)
	{
		// Первые карточки уже в дереве; остальные достраиваем короткими порциями по таймеру.
		// Так старт не зависит от того, успело ли окно отрисоваться (свёрнутое в трей,
		// заблокированный экран и т. п.), и интерфейс не замирает на полсекунды.
		if (_renderTimer != null)
		{
			return;
		}
		_renderTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(80.0)
		};
		_renderTimer.Tick += delegate
		{
			if (_renderedCount >= _renderList.Count)
			{
				_renderTimer?.Stop();
				_renderTimer = null;
				AppPaths.Log($"[запуск] сетка достроена: {_renderedCount} карточек");
				return;
			}
			AddCards(_renderList, CardsPerPortion);
		};
		_renderTimer.Start();
	}

	private Control BuildCard(GameEntry game, int index)
	{
		GameEntry game2 = game;
		Grid val = new Grid
		{
			Width = _cardWidth,
			Height = _cardHeight,
			ClipToBounds = true
		};
		Bitmap val2 = Ui.LoadCoverThumb(game2.CoverFile, 320);
		Image val3 = new Image
		{
			Source = (IImage)(object)val2,
			Stretch = (Stretch)3,
			HorizontalAlignment = (HorizontalAlignment)0,
			VerticalAlignment = (VerticalAlignment)0
		};
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val3);
		Control val4 = BuildPlaceholder(game2);
		val4.IsVisible = val2 == null;
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add(val4);
		Action openMenu = null;
		Control hover = BuildHoverOverlay(game2, delegate
		{
			openMenu?.Invoke();
		});
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add(hover);
		IBrush brush = (IBrush)(game2.Favorite ? ((object)Skin.Accent) : ((object)Brushes.White));
		Path val5 = Ui.Icon("M10,0 L12.6,6.6 L19.6,7.2 L14.3,11.9 L15.9,18.9 L10,15.3 L4.1,18.9 L5.7,11.9 L0.4,7.2 L7.4,6.6 Z", brush, 12.0, 12.0);
		Border starButton = Ui.Clickable((Control)(object)val5, (Action?)delegate
		{
			ToggleFavorite(game2);
		}, (IBrush?)new SolidColorBrush(Color.Parse("#C4070B11"), 1.0), (IBrush?)new SolidColorBrush(Color.Parse("#E60B111A"), 1.0), (CornerRadius?)new CornerRadius(8.0), (Thickness?)new Thickness(6.0), (IBrush?)null, (Thickness?)null);
		((Layoutable)starButton).Width = 28.0;
		((Layoutable)starButton).Height = 28.0;
		((Layoutable)starButton).HorizontalAlignment = (HorizontalAlignment)3;
		((Layoutable)starButton).VerticalAlignment = (VerticalAlignment)1;
		((Layoutable)starButton).Margin = new Thickness(0.0, 6.0, 6.0, 6.0);
		// Оптическая прозрачность ≠ кликабельность: раньше у невыбранной карточки
		// IsHitTestVisible был false, поэтому «В избранное» не нажималось, а у выбранной
		// звезда горела даже после снятия. Теперь видимостью управляет только hover,
		// а сама кнопка всегда доступна.
		starButton.Opacity = (game2.Favorite ? 1 : 0);
		((InputElement)starButton).IsHitTestVisible = true;
		Border obj = starButton;
		Transitions val6 = new Transitions();
		((AvaloniaList<ITransition>)val6).Add((ITransition)new DoubleTransition
		{
			Property = (AvaloniaProperty)(object)Visual.OpacityProperty,
			Duration = TimeSpan.FromMilliseconds(170.0)
		});
		((AvaloniaList<ITransition>)val6).Add((ITransition)new BrushTransition
		{
			Property = (AvaloniaProperty)(object)Border.BackgroundProperty,
			Duration = TimeSpan.FromMilliseconds(170.0)
		});
		((Animatable)obj).Transitions = val6;
		Ui.Tip((Control)(object)starButton, Loc.T("card.favorite"));
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)starButton);
		StackPanel val7 = new StackPanel
		{
			Margin = new Thickness(12.0, 9.0, 12.0, 11.0),
			Spacing = 3.0
		};
		TextBlock val8 = Ui.T(game2.Title, 12.5, Skin.Text, (FontWeight)600, (TextWrapping)0);
		((Layoutable)val8).MaxWidth = _cardWidth - 24.0;
		val8.TextTrimming = TextTrimming.CharacterEllipsis;
		TextBlock val9 = Ui.T(Subtitle(game2), 11.0, Skin.TextDim, null, (TextWrapping)0);
		((Layoutable)val9).MaxWidth = _cardWidth - 24.0;
		val9.TextTrimming = TextTrimming.CharacterEllipsis;
		((AvaloniaList<Control>)(object)((Panel)val7).Children).Add((Control)(object)val8);
		((AvaloniaList<Control>)(object)((Panel)val7).Children).Add((Control)(object)val9);
		Border val10 = new Border
		{
			Background = Skin.CardPlate,
			CornerRadius = new CornerRadius(0.0, 0.0, 6.0, 6.0),
			BorderThickness = new Thickness(0.0),
			Child = (Control)(object)val7
		};
		Border val11 = new Border
		{
			CornerRadius = new CornerRadius(6.0, 6.0, 0.0, 0.0),
			ClipToBounds = true,
			Child = (Control)(object)val
		};
		StackPanel val12 = new StackPanel();
		((AvaloniaList<Control>)(object)((Panel)val12).Children).Add((Control)(object)val11);
		((AvaloniaList<Control>)(object)((Panel)val12).Children).Add((Control)(object)val10);
		Border card = new Border
		{
			Child = (Control)(object)val12,
			Background = (IBrush)(object)Brushes.Transparent,
			CornerRadius = new CornerRadius(6.0),
			BoxShadow = Skin.CardShadow,
			Margin = new Thickness(0.0, 0.0, 18.0, 18.0),
			Width = _cardWidth,
			Cursor = new Cursor((StandardCursorType)9),
			RenderTransformOrigin = new RelativePoint(0.5, 0.5, (RelativeUnit)0)
		};
		Border obj2 = card;
		Transitions val13 = new Transitions();
		((AvaloniaList<ITransition>)val13).Add((ITransition)new BoxShadowsTransition
		{
			Property = (AvaloniaProperty)(object)Border.BoxShadowProperty,
			Duration = TimeSpan.FromMilliseconds(160.0)
		});
		((Animatable)obj2).Transitions = val13;
		((InputElement)card).PointerEntered += delegate
		{
			card.BoxShadow = Ui.GlowShadow();
			hover.Opacity = 1.0;
			((InputElement)hover).IsHitTestVisible = true;
			ShowStar(show: true);
		};
		((InputElement)card).PointerExited += delegate
		{
			card.BoxShadow = Skin.CardShadow;
			hover.Opacity = 0.0;
			((InputElement)hover).IsHitTestVisible = false;
			ShowStar(show: false);
		};
		((InputElement)card).PointerPressed += delegate(object? _, PointerPressedEventArgs e)
		{
			PointerPoint currentPoint2 = ((PointerEventArgs)e).GetCurrentPoint((Visual)(object)card);
			PointerPointProperties properties2 = ((PointerPoint)currentPoint2).Properties;
			if (!((PointerPointProperties)properties2).IsRightButtonPressed)
			{
				((RoutedEventArgs)e).Handled = true;
				ShowDetail(game2);
			}
		};
		Menu cardMenu = BuildContextMenu(game2);
		openMenu = delegate
		{
			cardMenu.Show((Control)(object)card, new Point(_cardWidth - 46.0, 10.0));
		};
		((Interactive)card).AddHandler<PointerPressedEventArgs>(InputElement.PointerPressedEvent, (EventHandler<PointerPressedEventArgs>)delegate(object? _, PointerPressedEventArgs e)
		{
			PointerPoint currentPoint = ((PointerEventArgs)e).GetCurrentPoint((Visual)(object)card);
			PointerPointProperties properties = ((PointerPoint)currentPoint).Properties;
			if (((PointerPointProperties)properties).IsRightButtonPressed)
			{
				((RoutedEventArgs)e).Handled = true;
				cardMenu.Show((Control)(object)card, ((PointerEventArgs)e).GetPosition((Visual)(object)card));
			}
		}, (RoutingStrategies)2, false);
		Ui.FadeIn((Visual)(object)card, Math.Min(index, 16) * 14, 260);
		_covers[game2.Id] = val3;
		_placeholders[game2.Id] = val4;
		_titles[game2.Id] = val8;
		_subtitles[game2.Id] = val9;
		_starButtons[game2.Id] = starButton;
		_starIcons[game2.Id] = val5;
		return (Control)(object)card;
		void ShowStar(bool show)
		{
			starButton.Opacity = ((show || game2.Favorite) ? 1 : 0);
			((InputElement)starButton).IsHitTestVisible = show || game2.Favorite;
		}
	}

	private static Border BuildIconButton(string text, string iconData, double iconW, double iconH, Action onClick)
	{
		StackPanel val = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 9.0
		};
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)Ui.Icon(iconData, Skin.OnAccent, iconW, iconH));
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)Ui.T(text, 13.0, Skin.OnAccent, (FontWeight)600, (TextWrapping)0));
		return Ui.Clickable((Control)val, onClick, Skin.Accent, Skin.AccentHover, (CornerRadius?)new CornerRadius(4.0), (Thickness?)new Thickness(18.0, 9.0), (IBrush?)null, (Thickness?)null);
	}

	private Control BuildHoverOverlay(GameEntry game, Action openMenu)
	{
		GameEntry game2 = game;
		Path val = Ui.Icon("M0,0 L15,8.5 L0,17 Z", (IBrush)(object)Brushes.White, 30.0, 34.0);
		TextBlock val2 = Ui.T(Loc.T("btn.play"), 12.0, (IBrush?)(object)Brushes.White, (FontWeight)600, (TextWrapping)0);
		((Layoutable)val2).HorizontalAlignment = (HorizontalAlignment)2;
		StackPanel panel = new StackPanel
		{
			VerticalAlignment = (VerticalAlignment)2,
			HorizontalAlignment = (HorizontalAlignment)2,
			Spacing = 6.0
		};
		((AvaloniaList<Control>)(object)((Panel)panel).Children).Add((Control)(object)val);
		((AvaloniaList<Control>)(object)((Panel)panel).Children).Add((Control)(object)val2);
		StackPanel val3 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 3.0,
			HorizontalAlignment = (HorizontalAlignment)2,
			VerticalAlignment = (VerticalAlignment)2
		};
		for (int i = 0; i < 3; i++)
		{
			((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)new Border
			{
				Width = 3.5,
				Height = 3.5,
				CornerRadius = new CornerRadius(2.0),
				Background = (IBrush)(object)Brushes.White
			});
		}
		Border val4 = Ui.Clickable((Control)(object)val3, openMenu, (IBrush?)new SolidColorBrush(Color.Parse("#73000000"), 1.0), (IBrush?)new SolidColorBrush(Color.Parse("#B3000000"), 1.0), (CornerRadius?)new CornerRadius(4.0), (Thickness?)new Thickness(11.0, 8.0), (IBrush?)null, (Thickness?)null);
		((Layoutable)val4).HorizontalAlignment = (HorizontalAlignment)3;
		((Layoutable)val4).VerticalAlignment = (VerticalAlignment)3;
		((Layoutable)val4).Margin = new Thickness(0.0, 0.0, 8.0, 8.0);
		((InputElement)val4).Cursor = new Cursor((StandardCursorType)9);
		Border val5 = new Border
		{
			Background = (IBrush)new SolidColorBrush(Color.Parse("#C00B111A"), 1.0),
			Opacity = 0.0,
			IsHitTestVisible = false,
			Cursor = new Cursor((StandardCursorType)9)
		};
		Grid val6 = new Grid();
		((AvaloniaList<Control>)(object)((Panel)val6).Children).Add((Control)(object)panel);
		((AvaloniaList<Control>)(object)((Panel)val6).Children).Add((Control)(object)val4);
		((Decorator)val5).Child = (Control)val6;
		Transitions val7 = new Transitions();
		((AvaloniaList<ITransition>)val7).Add((ITransition)new DoubleTransition
		{
			Property = (AvaloniaProperty)(object)Visual.OpacityProperty,
			Duration = TimeSpan.FromMilliseconds(150.0)
		});
		((Animatable)val5).Transitions = val7;
		Border overlay = val5;
		((InputElement)overlay).PointerPressed += delegate(object? _, PointerPressedEventArgs e)
		{
			PointerPoint currentPoint = ((PointerEventArgs)e).GetCurrentPoint((Visual)(object)overlay);
			PointerPointProperties properties = ((PointerPoint)currentPoint).Properties;
			if (!((PointerPointProperties)properties).IsRightButtonPressed)
			{
				((RoutedEventArgs)e).Handled = true;
				Ui.Bounce((Visual)(object)panel, 0.82, 90);
				Launch(game2);
			}
		};
		return (Control)(object)overlay;
	}

	private Control BuildPlaceholder(GameEntry game)
	{
		string text = new string((from w in game.Title.Split(new char[3] { ' ', ':', '-' }, StringSplitOptions.RemoveEmptyEntries).Take(2)
			select char.ToUpperInvariant(w[0])).ToArray());
		if (string.IsNullOrEmpty(text))
		{
			text = "?";
		}
		StackPanel val = new StackPanel
		{
			VerticalAlignment = (VerticalAlignment)2,
			HorizontalAlignment = (HorizontalAlignment)2,
			Spacing = 8.0
		};
		TextBlock val2 = Ui.T(text, 40.0, Skin.PlaceholderText, (FontWeight)700, (TextWrapping)0);
		((Layoutable)val2).HorizontalAlignment = (HorizontalAlignment)2;
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val2);
		TextBlock val3 = Ui.T(game.Title, 11.0, Skin.PlaceholderTextDim, null, (TextWrapping)1);
		((Layoutable)val3).HorizontalAlignment = (HorizontalAlignment)2;
		val3.TextAlignment = (TextAlignment)1;
		((Layoutable)val3).MaxWidth = _cardWidth - 30.0;
		val3.MaxLines = 3;
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val3);
		return (Control)new Border
		{
			Background = Skin.PlaceholderGradientA,
			Child = (Control)(object)val
		};
	}

	private string Subtitle(GameEntry game)
	{
		string text = ((game.Source != null) ? Loc.T("launcher." + game.Source) : null);
		if (!game.ExeExists)
		{
			return text ?? Loc.T("lbl.unknown");
		}
		if (game.LastPlayedUtc.HasValue)
		{
			return game.LastPlayedUtc.Value.ToLocalTime().ToString("dd.MM.yyyy");
		}
		return text ?? Loc.T("lbl.never");
	}

	private Menu BuildContextMenu(GameEntry game)
	{
		GameEntry game2 = game;
		Menu menu = new Menu();
		menu.Item(Loc.T("ctx.play"), delegate
		{
			Launch(game2);
		});
		if (Platform.SupportsElevation)
		{
			menu.Item(Loc.T("ctx.admin"), delegate
			{
				Launch(game2, asAdmin: true);
			});
		}
		menu.Separator();
		menu.Item(Loc.T("ctx.folder"), delegate
		{
			Shell.OpenFolder(game2.FolderPath);
		});
		menu.Item(Loc.T("ctx.cover"), delegate
		{
			ChangeCover(game2);
		});
		menu.Item(Loc.T("ctx.rename"), delegate
		{
			Rename(game2);
		});
		menu.Item(Loc.T("ctx.fav"), delegate
		{
			ToggleFavorite(game2);
		});
		menu.Separator();
		menu.Item(Loc.T("ctx.remove"), delegate
		{
			RemoveGame(game2);
		}, danger: true);
		if (LauncherUninstall.Available(game2))
		{
			menu.Item(Loc.F("ctx.uninstallLauncher", LauncherUninstall.LauncherName(game2.Source)), delegate
			{
				UninstallViaLauncher(game2);
			}, danger: true);
		}
		menu.Item(Loc.T("ctx.uninstall"), delegate
		{
			DeleteFromDisk(game2);
		}, danger: true);
		return menu;
	}

	private void ShowDetail(GameEntry game)
	{
		GameEntry game2 = game;
		Border val = new Border
		{
			Background = Skin.Panel,
			CornerRadius = new CornerRadius(14.0),
			MaxWidth = 920.0,
			Margin = new Thickness(30.0),
			HorizontalAlignment = (HorizontalAlignment)2,
			VerticalAlignment = (VerticalAlignment)2
		};
		BoxShadow val2 = new BoxShadow
		{
			OffsetX = 0.0,
			OffsetY = 12.0,
			Blur = 40.0,
			Color = Color.Parse("#99000000"),
		};
		val.BoxShadow = new BoxShadows(val2);
		Border val3 = val;
		Grid val4 = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("300,*")
		};
		Grid val5 = new Grid
		{
			Height = 450.0,
			ClipToBounds = true
		};
		Bitmap val6 = Ui.LoadCoverThumb(game2.CoverFile, 320);
		if (val6 != null)
		{
			((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)new Image
			{
				Source = (IImage)(object)val6,
				Stretch = (Stretch)3
			});
		}
		else
		{
			((AvaloniaList<Control>)(object)((Panel)val5).Children).Add(BuildPlaceholder(game2));
		}
		Border val7 = new Border
		{
			CornerRadius = new CornerRadius(14.0, 0.0, 0.0, 14.0),
			ClipToBounds = true,
			Child = (Control)(object)val5
		};
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)val7);
		StackPanel val8 = new StackPanel
		{
			Margin = new Thickness(26.0, 22.0, 26.0, 22.0),
			Spacing = 12.0
		};
		DockPanel val9 = new DockPanel();
		Border val10 = Ui.Clickable((Control)(object)Ui.Icon("M1,1 L11,11 M11,1 L1,11", Skin.TextMuted, 12.0, 12.0, stroke: true), (Action?)delegate
		{
			_detailLayer.IsVisible = false;
		}, Skin.PanelAlt, Skin.CardHover, (CornerRadius?)new CornerRadius(6.0), (Thickness?)new Thickness(10.0, 10.0), (IBrush?)null, (Thickness?)null);
		DockPanel.SetDock((Control)(object)val10, (Dock)2);
		((AvaloniaList<Control>)(object)((Panel)val9).Children).Add((Control)(object)val10);
		TextBlock val11 = Ui.T(game2.Title, 24.0, Skin.Text, (FontWeight)700, (TextWrapping)1);
		((Layoutable)val11).Margin = new Thickness(0.0, 0.0, 12.0, 0.0);
		((AvaloniaList<Control>)(object)((Panel)val9).Children).Add((Control)(object)val11);
		((AvaloniaList<Control>)(object)((Panel)val8).Children).Add((Control)(object)val9);
		if (game2.SteamAppId.HasValue || !string.IsNullOrWhiteSpace(game2.SteamName))
		{
			string text = ((game2.SteamName != null) ? ("Steam · " + game2.SteamName) : $"Steam app {game2.SteamAppId}");
			((AvaloniaList<Control>)(object)((Panel)val8).Children).Add((Control)(object)Ui.T(text, 11.5, Skin.TextDim, null, (TextWrapping)0));
		}
		((AvaloniaList<Control>)(object)((Panel)val8).Children).Add(BuildMetaTable(game2));
		WrapPanel val12 = new WrapPanel
		{
			Orientation = (Orientation)0
		};
		Border playBtn = null;
		playBtn = BuildIconButton(Loc.T("btn.play"), "M0,0 L15,8.5 L0,17 Z", 13.0, 15.0, delegate
		{
			if (playBtn != null)
			{
				Ui.Bounce((Visual)(object)playBtn, 0.88, 100);
			}
			Launch(game2);
		});
		((Layoutable)playBtn).Margin = new Thickness(0.0, 4.0, 10.0, 8.0);
		((AvaloniaList<Control>)(object)((Panel)val12).Children).Add((Control)(object)playBtn);
		if (Platform.SupportsElevation)
		{
			Border val13 = Ui.GhostButton(Loc.T("btn.playAdmin"), delegate
			{
				Launch(game2, asAdmin: true);
			});
			((Layoutable)val13).Margin = new Thickness(0.0, 4.0, 10.0, 8.0);
			((AvaloniaList<Control>)(object)((Panel)val12).Children).Add((Control)(object)val13);
		}
		Border val14 = Ui.GhostButton(Loc.T("btn.openFolder"), delegate
		{
			Shell.OpenFolder(game2.FolderPath);
		});
		((Layoutable)val14).Margin = new Thickness(0.0, 4.0, 10.0, 8.0);
		((AvaloniaList<Control>)(object)((Panel)val12).Children).Add((Control)(object)val14);
		((AvaloniaList<Control>)(object)((Panel)val8).Children).Add((Control)(object)val12);
		WrapPanel val15 = new WrapPanel
		{
			Orientation = (Orientation)0
		};
		Border val16 = Ui.GhostButton(Loc.T("btn.changeCover"), delegate
		{
			ChangeCover(game2);
		});
		((Layoutable)val16).Margin = new Thickness(0.0, 0.0, 10.0, 8.0);
		((AvaloniaList<Control>)(object)((Panel)val15).Children).Add((Control)(object)val16);
		Border val17 = Ui.GhostButton(Loc.T("btn.rename"), delegate
		{
			Rename(game2);
		});
		((Layoutable)val17).Margin = new Thickness(0.0, 0.0, 10.0, 8.0);
		((AvaloniaList<Control>)(object)((Panel)val15).Children).Add((Control)(object)val17);
		Border val18 = Ui.GhostButton((game2.Favorite ? "★  " : "☆  ") + Loc.T(game2.Favorite ? "btn.unfavorite" : "btn.favorite"), delegate
		{
			ToggleFavorite(game2);
		});
		((Layoutable)val18).Margin = new Thickness(0.0, 0.0, 10.0, 8.0);
		((AvaloniaList<Control>)(object)((Panel)val15).Children).Add((Control)(object)val18);
		Border val19 = Ui.GhostButton(Loc.T("btn.remove"), delegate
		{
			RemoveGame(game2);
		}, Skin.Danger);
		((Layoutable)val19).Margin = new Thickness(0.0, 0.0, 10.0, 8.0);
		((AvaloniaList<Control>)(object)((Panel)val15).Children).Add((Control)(object)val19);
		if (LauncherUninstall.Available(game2))
		{
			Border val20 = Ui.GhostButton(Loc.F("btn.deleteLauncher", LauncherUninstall.LauncherName(game2.Source)), delegate
			{
				UninstallViaLauncher(game2);
			}, Skin.Danger);
			((Layoutable)val20).Margin = new Thickness(0.0, 0.0, 10.0, 8.0);
			((AvaloniaList<Control>)(object)((Panel)val15).Children).Add((Control)(object)val20);
		}
		Border val21 = Ui.GhostButton(Loc.T("btn.deleteDisk"), delegate
		{
			DeleteFromDisk(game2);
		}, Skin.Danger);
		((Layoutable)val21).Margin = new Thickness(0.0, 0.0, 10.0, 8.0);
		((AvaloniaList<Control>)(object)((Panel)val15).Children).Add((Control)(object)val21);
		((AvaloniaList<Control>)(object)((Panel)val8).Children).Add((Control)(object)val15);
		ScrollViewer val22 = new ScrollViewer
		{
			Content = val8,
			VerticalScrollBarVisibility = (ScrollBarVisibility)1,
			MaxHeight = 450.0
		};
		Ui.SmoothScroll(val22, 80.0);
		Grid.SetColumn((Control)(object)val22, 1);
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)val22);
		((Decorator)val3).Child = (Control)(object)val4;
		val3.RenderTransformOrigin = new RelativePoint(0.5, 0.5, (RelativeUnit)0);
		Transitions val23 = new Transitions();
		((AvaloniaList<ITransition>)val23).Add((ITransition)new TransformOperationsTransition
		{
			Property = (AvaloniaProperty)(object)Visual.RenderTransformProperty,
			Duration = TimeSpan.FromMilliseconds(190.0),
			Easing = (Easing)new CubicEaseOut()
		});
		((Animatable)val3).Transitions = val23;
		((Decorator)_detailLayer).Child = (Control)(object)val3;
		Border detailLayer = _detailLayer;
		Transitions val24 = new Transitions();
		((AvaloniaList<ITransition>)val24).Add((ITransition)new DoubleTransition
		{
			Property = (AvaloniaProperty)(object)Visual.OpacityProperty,
			Duration = TimeSpan.FromMilliseconds(200.0),
			Easing = (Easing)new CubicEaseOut()
		});
		((Animatable)detailLayer).Transitions = val24;
		_detailLayer.Opacity = 0.0;
		val3.RenderTransform = (ITransform)new ScaleTransform(0.965, 0.965);
		_detailLayer.IsVisible = true;
		Dispatcher.UIThread.Post((Action)delegate
		{
			_detailLayer.Opacity = 1.0;
		}, default(DispatcherPriority));
	}

	private Control BuildMetaTable(GameEntry game)
	{
		StackPanel panel = new StackPanel
		{
			Spacing = 5.0,
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
		};
		Row(Loc.T("lbl.path"), game.FolderPath);
		Row(Loc.T("lbl.exe"), string.IsNullOrEmpty(game.ExePath) ? "—" : System.IO.Path.GetFileName(game.ExePath));
		Row(Loc.T("lbl.size"), (game.SizeBytes > 0) ? Scanner.FormatSize(game.SizeBytes) : Loc.T("lbl.calculating"));
		if (game.Source != null)
		{
			Row(Loc.T("lbl.source"), Loc.T("launcher." + game.Source));
		}
		Row(Loc.T("lbl.added"), game.AddedUtc.ToLocalTime().ToString("dd.MM.yyyy"));
		Row(Loc.T("lbl.lastplayed"), game.LastPlayedUtc.HasValue ? game.LastPlayedUtc.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm") : Loc.T("lbl.never"));
		Row(Loc.T("lbl.plays"), game.PlayCount.ToString());
		if (!game.ExeExists)
		{
			((AvaloniaList<Control>)(object)((Panel)panel).Children).Add((Control)(object)Ui.T("⚠  " + Loc.T("lbl.unknown"), 11.5, Skin.Danger, null, (TextWrapping)0));
		}
		return (Control)(object)panel;
		void Row(string label, string value)
		{
			Grid val = new Grid
			{
				ColumnDefinitions = new ColumnDefinitions("130,*")
			};
			TextBlock val2 = Ui.T(label, 11.5, Skin.TextDim, null, (TextWrapping)0);
			((Layoutable)val2).VerticalAlignment = (VerticalAlignment)1;
			((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val2);
			TextBlock val3 = Ui.T(value, 11.5, Skin.TextMuted, null, (TextWrapping)1);
			Grid.SetColumn((Control)(object)val3, 1);
			((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val3);
			((AvaloniaList<Control>)(object)((Panel)panel).Children).Add((Control)(object)val);
		}
	}

	private void Launch(GameEntry game, bool asAdmin = false)
	{
		GameEntry game2 = game;
		if (!game2.ExeExists)
		{
			Dialogs.ShowMessageAsync((Window)(object)this, "Ludex", Loc.T("status.launchError") + "\n" + game2.ExePath);
			return;
		}
		ShowLaunchOverlay(game2);
		DispatcherTimer.RunOnce((Action)delegate
		{
			if (Shell.LaunchGame(game2.ExePath, out string error, asAdmin))
			{
				game2.LastPlayedUtc = DateTime.UtcNow;
				game2.PlayCount++;
				Storage.SaveGames(_games);
				if (_subtitles.TryGetValue(game2.Id, out TextBlock value))
				{
					value.Text = Subtitle(game2);
				}
				ShowEvent(Loc.F("status.launched", game2.Title));
				HideLaunchOverlay(1500, delegate
				{
					if (_detailLayer.IsVisible)
					{
						ShowDetail(game2);
					}
				});
			}
			else
			{
				HideLaunchOverlay();
				ShowEvent(Loc.T("status.launchError"));
				DispatcherTimer.RunOnce((Action)delegate
				{
					Dialogs.ShowMessageAsync((Window)(object)this, "Ludex", Loc.T("status.launchError") + "\n" + error);
				}, TimeSpan.FromMilliseconds(320.0), default(DispatcherPriority));
			}
		}, TimeSpan.FromMilliseconds(520.0), default(DispatcherPriority));
	}

	private void ShowLaunchOverlay(GameEntry game)
	{
		Grid val = new Grid
		{
			Height = 356.0,
			ClipToBounds = true
		};
		Bitmap val2 = Ui.LoadCoverThumb(game.CoverFile, 320);
		if (val2 != null)
		{
			((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)new Image
			{
				Source = (IImage)(object)val2,
				Stretch = (Stretch)3,
				HorizontalAlignment = (HorizontalAlignment)0,
				VerticalAlignment = (VerticalAlignment)0
			});
			Controls children = ((Panel)val).Children;
			Border val3 = new Border();
			LinearGradientBrush val4 = new LinearGradientBrush
			{
				StartPoint = new RelativePoint(0.0, 0.62, (RelativeUnit)0),
				EndPoint = new RelativePoint(0.0, 1.0, (RelativeUnit)0)
			};
			((AvaloniaList<GradientStop>)(object)((GradientBrush)val4).GradientStops).Add(new GradientStop(Color.Parse("#00000000"), 0.0));
			((AvaloniaList<GradientStop>)(object)((GradientBrush)val4).GradientStops).Add(new GradientStop(Color.Parse("#80000000"), 1.0));
			val3.Background = (IBrush)val4;
			((AvaloniaList<Control>)(object)children).Add((Control)val3);
		}
		else
		{
			((AvaloniaList<Control>)(object)((Panel)val).Children).Add(BuildPlaceholder(game));
		}
		Border val5 = new Border
		{
			CornerRadius = new CornerRadius(14.0, 14.0, 0.0, 0.0),
			ClipToBounds = true,
			Child = (Control)(object)val
		};
		TextBlock val6 = Ui.T(game.Title, 17.0, Skin.Text, (FontWeight)600, (TextWrapping)1);
		((Layoutable)val6).HorizontalAlignment = (HorizontalAlignment)2;
		val6.TextAlignment = (TextAlignment)1;
		TextBlock val7 = Ui.T(Loc.T("launch.title"), 12.5, Skin.TextMuted, null, (TextWrapping)0);
		((Layoutable)val7).HorizontalAlignment = (HorizontalAlignment)2;
		TranslateTransform shift = new TranslateTransform(0.0, 0.0);
		Border child = new Border
		{
			Width = 88.0,
			Height = 4.0,
			CornerRadius = new CornerRadius(2.0),
			Background = Skin.Accent,
			HorizontalAlignment = (HorizontalAlignment)1,
			RenderTransform = (ITransform)(object)shift
		};
		Border val8 = new Border
		{
			Width = 248.0,
			Height = 4.0,
			CornerRadius = new CornerRadius(2.0),
			Background = Skin.PanelAlt,
			ClipToBounds = true,
			HorizontalAlignment = (HorizontalAlignment)2,
			Margin = new Thickness(0.0, 10.0, 0.0, 2.0),
			Child = (Control)(object)child
		};
		DispatcherTimer? launchTimer = _launchTimer;
		if (launchTimer != null)
		{
			launchTimer.Stop();
		}
		double phase = 0.0;
		_launchTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(16.0)
		};
		_launchTimer.Tick += delegate
		{
			phase += 0.019;
			double num = (Math.Sin(phase) + 1.0) / 2.0;
			shift.X = num * 154.0;
		};
		_launchTimer.Start();
		TextBlock val9 = Ui.T(Loc.T("launch.hint"), 10.5, Skin.TextDim, null, (TextWrapping)0);
		((Layoutable)val9).HorizontalAlignment = (HorizontalAlignment)2;
		StackPanel val10 = new StackPanel
		{
			Margin = new Thickness(24.0, 16.0, 24.0, 20.0),
			Spacing = 5.0
		};
		((AvaloniaList<Control>)(object)((Panel)val10).Children).Add((Control)(object)val6);
		((AvaloniaList<Control>)(object)((Panel)val10).Children).Add((Control)(object)val7);
		((AvaloniaList<Control>)(object)((Panel)val10).Children).Add((Control)(object)val8);
		((AvaloniaList<Control>)(object)((Panel)val10).Children).Add((Control)(object)val9);
		StackPanel val11 = new StackPanel();
		((AvaloniaList<Control>)(object)((Panel)val11).Children).Add((Control)(object)val5);
		((AvaloniaList<Control>)(object)((Panel)val11).Children).Add((Control)(object)val10);
		Border val12 = new Border
		{
			Width = 320.0,
			Background = Skin.Panel,
			CornerRadius = new CornerRadius(14.0),
			HorizontalAlignment = (HorizontalAlignment)2,
			VerticalAlignment = (VerticalAlignment)2,
			RenderTransformOrigin = new RelativePoint(0.5, 0.5, (RelativeUnit)0)
		};
		BoxShadow val13 = new BoxShadow
		{
			OffsetX = 0.0,
			OffsetY = 16.0,
			Blur = 46.0,
			Color = Color.Parse("#AA000000"),
		};
		val12.BoxShadow = new BoxShadows(val13);
		Transitions val14 = new Transitions();
		((AvaloniaList<ITransition>)val14).Add((ITransition)new TransformOperationsTransition
		{
			Property = (AvaloniaProperty)(object)Visual.RenderTransformProperty,
			Duration = TimeSpan.FromMilliseconds(230.0),
			Easing = (Easing)new CubicEaseOut()
		});
		((Animatable)val12).Transitions = val14;
		((Decorator)val12).Child = (Control)(object)val11;
		Border val15 = val12;
		((Decorator)_launchLayer).Child = (Control)(object)val15;
		_launchLayer.Opacity = 0.0;
		val15.RenderTransform = (ITransform)new ScaleTransform(0.93, 0.93);
		_launchLayer.IsVisible = true;
		Dispatcher.UIThread.Post((Action)delegate
		{
			_launchLayer.Opacity = 1.0;
		}, default(DispatcherPriority));
	}

	private void HideLaunchOverlay(int delayMs = 0, Action? after = null)
	{
		Action after2 = after;
		DispatcherTimer? launchTimer = _launchTimer;
		if (launchTimer != null)
		{
			launchTimer.Stop();
		}
		_launchTimer = null;
		DispatcherTimer.RunOnce((Action)delegate
		{
			_launchLayer.Opacity = 0.0;
			DispatcherTimer.RunOnce((Action)delegate
			{
				_launchLayer.IsVisible = false;
				((Decorator)_launchLayer).Child = null;
				after2?.Invoke();
			}, TimeSpan.FromMilliseconds(230.0), default(DispatcherPriority));
		}, TimeSpan.FromMilliseconds(delayMs), default(DispatcherPriority));
	}

	private void ToggleFavorite(GameEntry game)
	{
		game.Favorite = !game.Favorite;
		Storage.SaveGames(_games);
		if ((_favoritesOnly && !game.Favorite) || string.Equals(_settings.SortMode, "favorite", StringComparison.Ordinal))
		{
			RenderGrid();
		}
		else
		{
			UpdateFavoriteVisuals(game);
		}
		if (_detailLayer.IsVisible)
		{
			ShowDetail(game);
		}
	}

	private void UpdateFavoriteVisuals(GameEntry game)
	{
		if (_starIcons.TryGetValue(game.Id, out Path value))
		{
			Path obj = value;
			IBrush fill = (IBrush)(game.Favorite ? ((object)Skin.Accent) : ((object)Brushes.White));
			((Shape)obj).Fill = fill;
		}
		if (_starButtons.TryGetValue(game.Id, out Border value2))
		{
			// 1.0, только пока карточка под курсором; иначе — по состоянию избранного,
			// так что снятая звезда сразу гаснет, а не остаётся гореть.
			value2.Opacity = (game.Favorite || value2.IsPointerOver) ? 1.0 : 0.0;
		}
	}

	private async void Rename(GameEntry game)
	{
		string text = await Dialogs.PromptAsync((Window)(object)this, Loc.T("dlg.renameTitle"), game.Title);
		if (!string.IsNullOrWhiteSpace(text))
		{
			game.Title = text;
			game.Locked = true;
			Storage.SaveGames(_games);
			if (_titles.TryGetValue(game.Id, out TextBlock value))
			{
				value.Text = game.Title;
			}
			if (_subtitles.TryGetValue(game.Id, out TextBlock value2))
			{
				value2.Text = Subtitle(game);
			}
			if (_detailLayer.IsVisible)
			{
				ShowDetail(game);
			}
		}
	}

	private async void ChangeCover(GameEntry game)
	{
		CoverPanel window = new CoverPanel(game.Title, _settings.SteamGridDbKey, game.Id, game.SteamAppId);
		TaskCompletionSource done = new TaskCompletionSource();
		window.Closed += delegate
		{
			done.TrySetResult();
		};
		((Layoutable)window).HorizontalAlignment = (HorizontalAlignment)2;
		((Layoutable)window).VerticalAlignment = (VerticalAlignment)2;
		((Layoutable)window).Margin = new Thickness(24.0);
		((Decorator)_settingsLayer).Child = (Control)(object)window;
		_settingsLayer.Opacity = 0.0;
		_settingsLayer.IsVisible = true;
		Dispatcher.UIThread.Post((Action)delegate
		{
			_settingsLayer.Opacity = 1.0;
		}, default(DispatcherPriority));
		await done.Task;
		_settingsLayer.Opacity = 0.0;
		_settingsLayer.IsVisible = false;
		((Decorator)_settingsLayer).Child = null;
		if (window.ChosenCoverPath != null)
		{
			game.CoverFile = window.ChosenCoverPath;
			game.CoverIsPortrait = window.ChosenIsPortrait;
			game.Locked = true;
			Storage.SaveGames(_games);
			UpdateCover(game);
			if (_detailLayer.IsVisible)
			{
				ShowDetail(game);
			}
		}
		else if (window.ResetRequested)
		{
			game.Locked = false;
			game.CoverFile = null;
			Storage.SaveGames(_games);
			UpdateCover(game);
			FetchArtworkForAsync(new List<GameEntry> { game });
		}
	}

	private void UpdateCover(GameEntry game)
	{
		Bitmap val = Ui.LoadBitmap(game.CoverFile);
		if (_covers.TryGetValue(game.Id, out Image value))
		{
			value.Source = (IImage)(object)val;
			if (val != null)
			{
				Ui.FadeIn((Visual)(object)value, 0, 320);
			}
		}
		if (_placeholders.TryGetValue(game.Id, out Control value2))
		{
			value2.IsVisible = val == null;
		}
	}

	private async void RemoveGame(GameEntry game)
	{
		if (await Dialogs.ConfirmAsync((Window)(object)this, Loc.T("dlg.removeTitle"), Loc.F("dlg.removeText", game.Title), Loc.T("btn.remove"), Loc.T("btn.cancel")))
		{
			RemoveFromLibraryOnly(game);
			ShowEvent(Loc.T("status.removed"));
		}
	}

	private async void UninstallViaLauncher(GameEntry game)
	{
		string url = LauncherUninstall.UrlFor(game);
		if (url == null)
		{
			DeleteFromDisk(game);
			return;
		}
		string launcher = LauncherUninstall.LauncherName(game.Source);
		if (!(await Dialogs.ConfirmAsync((Window)(object)this, Loc.T("dlg.launcherDeleteTitle"), Loc.F("dlg.launcherDeleteText", game.Title, launcher), Loc.F("btn.openLauncher", launcher), Loc.T("btn.cancel"))))
		{
			return;
		}
		ProcessBegin("uninstall", Loc.F("status.uninstallOpening", launcher));
		try
		{
			Shell.OpenUrl(url);
			AppPaths.Log("Удаление через лаунчер: " + game.Title + " → " + url);
			ShowEvent(Loc.F("status.uninstallOpened", launcher), sticky: true);
		}
		catch (Exception ex)
		{
			AppPaths.Log("uninstall via launcher failed: " + ex.Message);
			ShowEvent(Loc.T("status.deleteFailed"));
		}
		finally
		{
			ProcessEnd("uninstall");
		}
		_detailLayer.IsVisible = false;
	}

	private async void DeleteFromDisk(GameEntry game)
	{
		// Ищем папку по исходному названию игры, а не удаляем папку с exe вслепую.
		// Предложение всегда показывается и требует отдельного подтверждения.
		string? folder = Uninstaller.SuggestGameFolder(game, _games, _settings.Folders ?? new List<string>());
		bool chooseManually = folder == null;
		if (folder != null)
		{
			int answer = await Dialogs.ConfirmWithAlternativeAsync((Window)(object)this, Loc.T("dlg.deleteTitle"),
				Loc.F("dlg.deleteSuggested", game.Title, folder), Loc.T("btn.deleteForever"),
				Loc.T("dlg.deleteOtherFolder"), Loc.T("btn.cancel"));
			if (answer == 0) return;
			chooseManually = answer == 2;
		}
		if (chooseManually)
		{
			if (!await Dialogs.ConfirmAsync((Window)(object)this, Loc.T("dlg.deleteTitle"),
				Loc.F("dlg.deleteChooseIntro", game.Title, game.ExePath), Loc.T("dlg.deleteChooseButton"), Loc.T("btn.cancel"))) return;
			IReadOnlyList<IStorageFolder> folders;
			try
			{
				folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
				{
					Title = Loc.T("dlg.deletePickFolder"), AllowMultiple = false
				});
			}
			catch (Exception ex)
			{
				AppPaths.Log("delete folder picker: " + ex.Message);
				Dialogs.ShowError((Window)(object)this, Loc.T("status.deleteFailed") + ": " + ex.Message);
				return;
			}
			if (folders.Count == 0) return;
			folder = StorageProviderExtensions.TryGetLocalPath(folders[0]);
			if (string.IsNullOrWhiteSpace(folder) || !Uninstaller.IsValidGameFolder(folder, game, _games, out _))
			{
				Dialogs.ShowError((Window)(object)this, Loc.T("dlg.deleteInvalidFolder"));
				return;
			}
			if (!await Dialogs.ConfirmAsync((Window)(object)this, Loc.T("dlg.deleteTitle"),
				Loc.F("dlg.deleteSelected", game.Title, folder), Loc.T("btn.deleteForever"), Loc.T("btn.cancel"))) return;
		}
		if (string.IsNullOrWhiteSpace(folder)) return;
		// Повторная проверка после ожидания подтверждения.
		if (!Uninstaller.IsValidGameFolder(folder, game, _games, out _))
		{
			Dialogs.ShowError((Window)(object)this, Loc.T("dlg.deleteInvalidFolder"));
			return;
		}
		_detailLayer.IsVisible = false;
		ProcessBegin("delete", Loc.F("status.deleting", game.Title));
		(bool ok, string error) result;
		try { result = await Uninstaller.RemoveFolderAsync(folder); }
		catch (Exception ex) { result = (false, ex.Message); }
		finally { ProcessEnd("delete"); }
		if (!result.ok)
		{
			AppPaths.Log("delete failed: " + folder + " — " + result.error);
			ShowEvent(Loc.T("status.deleteFailed"));
			Dialogs.ShowError((Window)(object)this, Loc.T("dlg.deletePartialFailure") + "\n" + result.error);
		}
		else
		{
			AppPaths.Log("deleted game folder: " + folder);
			RemoveFromLibraryOnly(game);
			ShowEvent(Loc.T("status.deleted"));
		}
	}

	private void RemoveFromLibraryOnly(GameEntry game)
	{
		try
		{
			foreach (string item in Directory.EnumerateFiles(AppPaths.CoversDir, game.Id + "_*"))
			{
				File.Delete(item);
			}
		}
		catch
		{
		}
		HideForever(game);
		_games.Remove(game);
		Storage.SaveGames(_games);
		RefreshIdleStatus();
		_detailLayer.IsVisible = false;
		RenderGrid();
	}

	private void ShowSettings()
	{
		_settingsPanel = new SettingsPanel(_settings);
		_settingsPanel.Saved += OnSettingsSaved;
		_settingsPanel.Closed += CloseSettings;
		_settingsPanel.AppearanceChanged += OnAppearanceChanged;
		_settingsPanel.WallpaperChanged += OnWallpaperChanged;
		_settingsPanel.UpdateCheckRequested += delegate
		{
			CloseSettings();
			CheckUpdatesAsync(silent: false);
		};
		_settingsPanel.HiddenListRequested += ShowHiddenGames;
		_settingsPanel.CleanupRequested += delegate
		{
			CloseSettings();
			CleanupLibraryAsync();
		};
		((Layoutable)_settingsPanel).HorizontalAlignment = (HorizontalAlignment)2;
		((Layoutable)_settingsPanel).VerticalAlignment = (VerticalAlignment)2;
		((Layoutable)_settingsPanel).Margin = new Thickness(24.0);
		((Decorator)_settingsLayer).Child = (Control)(object)_settingsPanel;
		_settingsLayer.Opacity = 0.0;
		_settingsLayer.IsVisible = true;
		_settingsPanel.RenderTransform = (ITransform)new ScaleTransform(0.965, 0.965);
		Dispatcher.UIThread.Post((Action)delegate
		{
			_settingsLayer.Opacity = 1.0;
			_settingsPanel.RenderTransform = (ITransform)new ScaleTransform(1.0, 1.0);
		}, default(DispatcherPriority));
	}

	private void ShowIntegrations()
	{
		_integrationsPanel = new IntegrationsPanel(_settings);
		_integrationsPanel.Closed += CloseSettings;
		_integrationsPanel.Changed += delegate
		{
			Storage.SaveSettings(_settings);
			ImportLaunchersAsync();
		};
		((Layoutable)_integrationsPanel).HorizontalAlignment = (HorizontalAlignment)2;
		((Layoutable)_integrationsPanel).VerticalAlignment = (VerticalAlignment)2;
		((Layoutable)_integrationsPanel).Margin = new Thickness(24.0);
		((Decorator)_settingsLayer).Child = (Control)(object)_integrationsPanel;
		_settingsLayer.Opacity = 0.0;
		_settingsLayer.IsVisible = true;
		_integrationsPanel.RenderTransform = (ITransform)new ScaleTransform(0.965, 0.965);
		Dispatcher.UIThread.Post((Action)delegate
		{
			_settingsLayer.Opacity = 1.0;
			_integrationsPanel.RenderTransform = (ITransform)new ScaleTransform(1.0, 1.0);
		}, default(DispatcherPriority));
	}

	private void ShowHiddenGames()
	{
		if (_settings.HiddenGames.Count == 0)
		{
			ShowEvent(Loc.T("hidden.empty"));
			return;
		}
		_hiddenPanel = new HiddenPanel(_settings.HiddenGames, HiddenTitle);
		_hiddenPanel.Closed += CloseSettings;
		_hiddenPanel.Restored += delegate(string key)
		{
			_settings.HiddenGames.Remove(key);
			_settings.HiddenTitles.Remove(key);
			Storage.SaveSettings(_settings);
			AppPaths.Log("Игра возвращена в библиотеку: " + key);
			ShowEvent(Loc.T("hidden.restored"));
			RefreshLibraryFromDisk();
		};
		_hiddenPanel.RestoredAll += delegate
		{
			int count = _settings.HiddenGames.Count;
			_settings.HiddenGames.Clear();
			_settings.HiddenTitles.Clear();
			Storage.SaveSettings(_settings);
			AppPaths.Log($"Скрытые игры возвращены: {count}");
			ShowEvent(Loc.F("hidden.restoredMany", count));
			RefreshLibraryFromDisk();
		};
		((Layoutable)_hiddenPanel).HorizontalAlignment = (HorizontalAlignment)2;
		((Layoutable)_hiddenPanel).VerticalAlignment = (VerticalAlignment)2;
		((Layoutable)_hiddenPanel).Margin = new Thickness(24.0);
		((Decorator)_settingsLayer).Child = (Control)(object)_hiddenPanel;
		_settingsLayer.Opacity = 0.0;
		_settingsLayer.IsVisible = true;
		_hiddenPanel.RenderTransform = (ITransform)new ScaleTransform(0.965, 0.965);
		Dispatcher.UIThread.Post((Action)delegate
		{
			_settingsLayer.Opacity = 1.0;
			_hiddenPanel.RenderTransform = (ITransform)new ScaleTransform(1.0, 1.0);
		}, default(DispatcherPriority));
	}

	private string HiddenTitle(string key)
	{
		if (_settings.HiddenTitles.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value))
		{
			return value;
		}
		string text = key.TrimEnd('/', '\\');
		int num = text.LastIndexOf('/');
		string text2;
		if (num < 0)
		{
			text2 = text;
		}
		else
		{
			string text3 = text;
			int num2 = num + 1;
			text2 = text3.Substring(num2, text3.Length - num2);
		}
		text = text2;
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return key;
	}

	private void RefreshLibraryFromDisk()
	{
		if (_settings.Folders.Count > 0)
		{
			StartScanAsync();
		}
		else
		{
			ImportLaunchersAsync(quiet: true);
		}
	}

	private void CloseSettings()
	{
		if (!_settingsLayer.IsVisible)
		{
			return;
		}
		if (((Decorator)_settingsLayer).Child is CoverPanel coverPanel)
		{
			coverPanel.Cancel();
			return;
		}
		_settingsLayer.Opacity = 0.0;
		if (_settingsPanel != null)
		{
			_settingsPanel.RenderTransform = (ITransform)new ScaleTransform(0.98, 0.98);
		}
		if (_integrationsPanel != null)
		{
			_integrationsPanel.RenderTransform = (ITransform)new ScaleTransform(0.98, 0.98);
		}
		if (_hiddenPanel != null)
		{
			_hiddenPanel.RenderTransform = (ITransform)new ScaleTransform(0.98, 0.98);
		}
		DispatcherTimer.RunOnce((Action)delegate
		{
			_settingsLayer.IsVisible = false;
			((Decorator)_settingsLayer).Child = null;
			_settingsPanel = null;
			_integrationsPanel = null;
			_hiddenPanel = null;
			if (_appearanceDirty)
			{
				_appearanceDirty = false;
				BuildUi();
				RenderGrid();
			}
		}, TimeSpan.FromMilliseconds(170.0), default(DispatcherPriority));
	}

	private void OnSettingsSaved()
	{
		bool num = _settingsPanel?.ScanRequested ?? false;
		Loc.Lang = _settings.Language;
		Skin.Apply(_settings.Theme, _settings.Accent, _settings.Transparent);
		ApplyTransparency();
		((TemplatedControl)this).Background = Skin.Bg;
		Storage.SaveSettings(_settings);
		_settingsLayer.IsVisible = false;
		((Decorator)_settingsLayer).Child = null;
		_settingsPanel = null;
		_appearanceDirty = false;
		BuildUi();
		RenderGrid();
		if (num)
		{
			StartScanAsync();
		}
	}

	private void ApplyTransparency()
	{
		try
		{
			((TopLevel)this).TransparencyLevelHint = (IReadOnlyList<WindowTransparencyLevel>)(object)(_settings.Transparent ? new WindowTransparencyLevel[2]
			{
				WindowTransparencyLevel.AcrylicBlur,
				WindowTransparencyLevel.Blur
			} : new WindowTransparencyLevel[1] { WindowTransparencyLevel.None });
		}
		catch (Exception ex)
		{
			AppPaths.Log("Transparency hint failed: " + ex.Message);
		}
	}

	private void OnAppearanceChanged()
	{
		Skin.Apply(_settings.Theme, _settings.Accent, _settings.Transparent);
		ApplyTransparency();
		((TemplatedControl)this).Background = Skin.Bg;
		if (_settingsLayer != null)
		{
			_settingsLayer.Background = Skin.Overlay;
		}
		if (_detailLayer != null)
		{
			_detailLayer.Background = Skin.Overlay;
		}
		if (_launchLayer != null)
		{
			_launchLayer.Background = Skin.Overlay;
		}
		_appearanceDirty = true;
		RefreshWallpaper();
	}

	private void OnWallpaperChanged()
	{
		Storage.SaveSettings(_settings);
		RefreshWallpaper();
	}

	private void RefreshWallpaper()
	{
		if (_wallpaperLayer == null)
		{
			return;
		}
		((AvaloniaList<Control>)(object)((Panel)_wallpaperLayer).Children).Clear();
		Stream wallpaperStream = _wallpaperStream;
		_wallpaperStream = null;
		if (wallpaperStream != null)
		{
			try
			{
				wallpaperStream.Dispose();
			}
			catch
			{
			}
		}
		Control val = BuildWallpaperVisual();
		if (val != null)
		{
			((AvaloniaList<Control>)(object)((Panel)_wallpaperLayer).Children).Add(val);
			Color val2 = (Skin.IsLight ? Color.Parse("#EDF1F6") : Skin.BgColor);
			Controls children = ((Panel)_wallpaperLayer).Children;
			Border val3 = new Border();
			LinearGradientBrush val4 = new LinearGradientBrush
			{
				StartPoint = new RelativePoint(0.0, 0.0, (RelativeUnit)0),
				EndPoint = new RelativePoint(0.0, 1.0, (RelativeUnit)0)
			};
			((AvaloniaList<GradientStop>)(object)((GradientBrush)val4).GradientStops).Add(new GradientStop(Color.FromArgb((byte)(Skin.Transparent ? 110u : 184u), ((Color)val2).R, ((Color)val2).G, ((Color)val2).B), 0.0));
			((AvaloniaList<GradientStop>)(object)((GradientBrush)val4).GradientStops).Add(new GradientStop(Color.FromArgb((byte)(Skin.Transparent ? 180u : 232u), ((Color)val2).R, ((Color)val2).G, ((Color)val2).B), 1.0));
			val3.Background = (IBrush)val4;
			((AvaloniaList<Control>)(object)children).Add((Control)val3);
		}
	}

	private Control? BuildWallpaperVisual()
	{
		string backgroundPath = _settings.BackgroundPath;
		if (string.IsNullOrWhiteSpace(backgroundPath) || !File.Exists(backgroundPath))
		{
			return null;
		}
		if (backgroundPath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
		{
			try
			{
				_wallpaperStream = File.OpenRead(backgroundPath);
				return (Control?)new GifImage
				{
					Source = _wallpaperStream,
					Stretch = (Stretch)3,
					IterationCount = IterationCount.Infinite,
					HorizontalAlignment = (HorizontalAlignment)0,
					VerticalAlignment = (VerticalAlignment)0
				};
			}
			catch (Exception ex)
			{
				AppPaths.Log("Wallpaper gif failed: " + ex.Message);
				return null;
			}
		}
		Bitmap val = Ui.LoadBitmap(backgroundPath);
		if (val == null)
		{
			return null;
		}
		return (Control?)new Image
		{
			Source = (IImage)(object)val,
			Stretch = (Stretch)3,
			HorizontalAlignment = (HorizontalAlignment)0,
			VerticalAlignment = (VerticalAlignment)0
		};
	}

	public async Task RevealAsync()
	{
		if (_revealHost != null)
		{
			CubicEaseOut easing = new CubicEaseOut();
			int ms = RevealMs(460);
			ScaleTransform scale = new ScaleTransform(0.88, 0.88);
			TranslateTransform lift = new TranslateTransform(0.0, 18.0);
			TransformGroup val = new TransformGroup();
			((AvaloniaList<Transform>)(object)val.Children).Add((Transform)(object)scale);
			((AvaloniaList<Transform>)(object)val.Children).Add((Transform)(object)lift);
			TransformGroup renderTransform = val;
			_revealHost.RenderTransform = (ITransform)(object)renderTransform;
			_revealHost.Opacity = 0.0;
			await AnimateAsync((Visual)(object)_revealHost, ms, (IEasing)(object)easing, delegate(double t, double e)
			{
				float num = (float)(0.88 + 0.12 * e + 0.012 * Math.Sin(Math.PI * e));
				scale.ScaleX = num;
				scale.ScaleY = num;
				lift.Y = 18.0 * (1.0 - e);
				_revealHost.Opacity = Math.Min(1.0, e * 1.35);
			});
		}
	}

	private static int RevealMs(int fallback)
	{
		if (!int.TryParse(Environment.GetEnvironmentVariable("LUDEX_REVEAL_MS"), out var result) || result <= 0)
		{
			return fallback;
		}
		return result;
	}

	private static async Task AnimateAsync(Visual target, int ms, IEasing easing, Action<double, double> step)
	{
		Stopwatch clock = Stopwatch.StartNew();
		while (true)
		{
			double num = Math.Min(1.0, clock.Elapsed.TotalMilliseconds / (double)ms);
			step(num, easing.Ease(num));
			if (num >= 1.0)
			{
				break;
			}
			await Task.Delay(12);
		}
	}

	private static Action<string> Mark = delegate { };

	public Task WarmUpAsync()
	{
		// Окно больше не ждёт прогрев: раньше заставка висела, пока качался каталог
		// и сканировались папки. Теперь всё это идёт в фоне и само обновляет список,
		// когда закончит, — пользователь видит интерфейс сразу.
		_ = WarmUpCoreAsync();
		return Task.CompletedTask;
	}

	private async Task WarmUpCoreAsync()
	{
		System.Diagnostics.Stopwatch warmClock = System.Diagnostics.Stopwatch.StartNew();
		DateTime started = DateTime.UtcNow;
		// Полный каталог (130 тысяч игр) на старте больше не нужен: только индекс названий,
		// чтобы сразу работала проверка «это игра?». Список подгрузится при открытии рекомендаций.
		Task catalog = StoreCatalog.PreloadIndexAsync(CancellationToken.None);
		Task library = Task.CompletedTask;
		// Раньше здесь требовалось непустое _settings.Folders, поэтому при включённом
		// автопоиске и пустом списке папок сканирование на старте не запускалось вовсе —
		// библиотека оставалась пустой, хотя «Сканировать» находило игры.
		bool autoScanAtStart = _settings.AutoScan && (_settings.AutoScanOnStartup || _games.Count == 0);
		bool foldersAtStart = _settings.Folders.Count > 0 && (_settings.AutoScanOnStartup || _games.Count == 0);
		if (autoScanAtStart || foldersAtStart)
		{
			library = StartScanAsync();
		}
		else if (_settings.Launchers.Count > 0)
		{
			library = ImportLaunchersAsync(quiet: true);
		}
		while (DateTime.UtcNow - started < TimeSpan.FromSeconds(6.0) && (!catalog.IsCompleted || !library.IsCompleted))
		{
			await Task.Delay(200);
		}
		if (catalog.IsFaulted)
		{
			AppPaths.Log("WarmUp catalog: " + catalog.Exception?.GetBaseException().Message);
		}

		AppPaths.Log($"[запуск] прогрев {warmClock.ElapsedMilliseconds} мс (каталог {(catalog.IsCompleted ? "готов" : "догружается")})");
	}

	private void OnOpened(object? sender, EventArgs e)
	{
		ComputeSizesAsync();
		if (_settings.CheckUpdatesOnStartup)
		{
			CheckUpdatesAsync(silent: true);
		}
		if (_settings.Folders.Count == 0 && _games.Count == 0)
		{
			ShowEvent(Loc.T("status.noFolders"));
		}
	}

	private async Task CheckUpdatesAsync(bool silent)
	{
		if (silent)
		{
			await Task.Delay(2500);
		}
		UpdateInfo info;
		try
		{
			info = await Updater.CheckAsync();
		}
		catch (Exception ex)
		{
			AppPaths.Log("Update check failed: " + ex.Message);
			if (!silent)
			{
				await Dialogs.ShowMessageAsync((Window)(object)this, Loc.T("update.title"), Loc.F("update.checkFailed", ex.Message));
			}
			return;
		}
		if (info == null)
		{
			AppPaths.Log("Update check: уже последняя версия " + Updater.CurrentVersionText);
			if (!silent)
			{
				await Dialogs.ShowMessageAsync((Window)(object)this, Loc.T("update.title"), Loc.F("update.latest", Updater.CurrentVersionText));
			}
			return;
		}
		if (!Updater.CanSelfUpdate(out string reason))
		{
			AppPaths.Log("Update: self-update not possible — " + reason);
			await Dialogs.ShowMessageAsync((Window)(object)this, Loc.T("update.title"), Loc.F("update.manual", ((info.PageUrl.Length > 0) ? (info.PageUrl + "\n\n") : "") + reason));
			return;
		}
		ShowEvent(Loc.F("update.available", info.Version.ToString(3), Updater.CurrentVersionText));
		if (!(await Dialogs.UpdateAvailableAsync((Window)(object)this, info)))
		{
			return;
		}
		string package = null;
		try
		{
			await Dialogs.ShowProgressAsync((Window)(object)this, Loc.T("update.downloading"), info.AssetName + ((info.SizeText.Length > 0) ? (" · " + info.SizeText) : ""), async delegate(IProgress<double> progress)
			{
				package = await Updater.DownloadAsync(info, progress);
			});
		}
		catch (Exception ex2)
		{
			AppPaths.Log("Update download failed: " + ex2.Message);
			await Dialogs.ShowMessageAsync((Window)(object)this, Loc.T("update.title"), ex2.Message);
			return;
		}
		if (package != null)
		{
			try
			{
				Updater.ApplyAndRestart(package, info);
			}
			catch (Exception ex3)
			{
				AppPaths.Log("Update apply failed: " + ex3.Message);
				await Dialogs.ShowMessageAsync((Window)(object)this, Loc.T("update.title"), Loc.F("update.manual", (info.PageUrl.Length > 0) ? info.PageUrl : ex3.Message));
				return;
			}
			AppPaths.Log("Update: restarting into " + info.Tag);
			// Закрыть окно недостаточно: при включённом CloseToTray оно лишь прячется
			// в трей, процесс остаётся жить и держит файлы. Помощник обновления ждёт
			// завершения процесса — без Shutdown получался цикл «обновление → старая
			// копия снова стартует → снова обновление». Выходим по-настоящему.
			ExitApplication();
		}
	}

	private void OnClosing(object? sender, WindowClosingEventArgs e)
	{
		if (!_exiting && _settings.CloseToTray)
		{
			((CancelEventArgs)(object)e).Cancel = true;
			HideToTray();
		}
		else
		{
			SaveBeforeExit();
		}
	}

	private void SaveBeforeExit()
	{
		_workCts?.Cancel();
		_artCts?.Cancel();
		DispatcherTimer? toastWatchdog = _toastWatchdog;
		if (toastWatchdog != null)
		{
			toastWatchdog.Stop();
		}
		_toastWatchdog = null;
		DispatcherTimer? catalogNoteTimer = _catalogNoteTimer;
		if (catalogNoteTimer != null)
		{
			catalogNoteTimer.Stop();
		}
		_catalogNoteTimer = null;
		_statusTimer?.Dispose();
		_statusTimer = null;
		DispatcherTimer? eventTimer = _eventTimer;
		if (eventTimer != null)
		{
			eventTimer.Stop();
		}
		_eventTimer = null;
		_processToast?.HideNow();
		_eventToast?.HideNow();
		_settings.Language = Loc.Lang;
		_settings.WindowWidth = ((Layoutable)this).Width;
		_settings.WindowHeight = ((Layoutable)this).Height;
		_settings.WindowMaximized = (int)((Window)this).WindowState == 2;
		Storage.SaveSettings(_settings);
		Storage.SaveGames(_games);
	}

	public void EnsureTray()
	{
		CreateTray();
	}

	private void CreateTray()
	{
		if (_trayIcon != null)
		{
			return;
		}
		try
		{
			WindowIcon val = LoadTrayIcon();
			if (val == null)
			{
				AppPaths.Log("трей: значок не найден, значок не создан");
				return;
			}
			_trayMenu = new TrayMenu();
			_trayMenu.ShowWindowRequested += delegate
			{
				Dispatcher.UIThread.Post((Action)OpenFromTray, default(DispatcherPriority));
			};
			_trayMenu.CatalogRequested += delegate
			{
				Dispatcher.UIThread.Post((Action)delegate
				{
					OpenFromTray();
					ShowRecommendations();
				}, default(DispatcherPriority));
			};
			_trayMenu.LibraryRequested += delegate
			{
				Dispatcher.UIThread.Post((Action)delegate
				{
					OpenFromTray();
					ShowLibrary();
				}, default(DispatcherPriority));
			};
			_trayMenu.SettingsRequested += delegate
			{
				Dispatcher.UIThread.Post((Action)delegate
				{
					OpenFromTray();
					ShowSettings();
				}, default(DispatcherPriority));
			};
			_trayMenu.ExitRequested += delegate
			{
				Dispatcher.UIThread.Post((Action)ExitApplication, default(DispatcherPriority));
			};
			_trayIcon = new TrayIcon
			{
				Icon = val,
				ToolTipText = Loc.T("tray.tooltip"),
				Menu = _trayMenu.Menu,
				IsVisible = true
			};
			_trayIcon.Clicked += delegate
			{
				Dispatcher.UIThread.Post((Action)ToggleFromTray, default(DispatcherPriority));
			};
			Application current = Application.Current;
			TrayIcons val2 = new TrayIcons();
			((AvaloniaList<TrayIcon>)val2).Add(_trayIcon);
			TrayIcon.SetIcons(current, val2);
		}
		catch (Exception ex)
		{
			AppPaths.Log("трей: " + ex.Message);
		}
	}

	private static WindowIcon? LoadTrayIcon()
	{
		string[] array = new string[2] { "avares://Ludex/Assets/app.ico", "avares://Ludex/Assets/icon.png" };
		foreach (string uriString in array)
		{
			try
			{
				Uri uri = new Uri(uriString);
				if (!AssetLoader.Exists(uri, (Uri)null))
				{
					continue;
				}
				using Stream stream = AssetLoader.Open(uri, (Uri)null);
				return new WindowIcon(stream);
			}
			catch
			{
			}
		}
		return null;
	}

	private void ToggleFromTray()
	{
		if (this.IsVisible && (int)((Window)this).WindowState != 1)
		{
			HideToTray();
		}
		else
		{
			OpenFromTray();
		}
	}

	private void HideToTray()
	{
		if (_trayIcon == null)
		{
			CreateTray();
		}
		_hiddenToTray = true;
		((WindowBase)this).Hide();
	}

	public void OpenFromTray()
	{
		if (!this.IsVisible)
		{
			((WindowBase)this).Show();
		}
		if ((int)((Window)this).WindowState == 1)
		{
			((Window)this).WindowState = (WindowState)0;
		}
		((WindowBase)this).Activate();
		((WindowBase)this).Topmost = true;
		((WindowBase)this).Topmost = false;
		((InputElement)this).Focus((NavigationMethod)0, (KeyModifiers)0);
		if (_hiddenToTray)
		{
			_hiddenToTray = false;
			RevealInstantly();
		}
	}

	private void ExitApplication()
	{
		_exiting = true;
		SaveBeforeExit();
		try
		{
			TrayIcon? trayIcon = _trayIcon;
			if (trayIcon != null)
			{
				trayIcon.Dispose();
			}
		}
		catch
		{
		}
		_trayIcon = null;
		Application current = Application.Current;
		IApplicationLifetime obj2 = ((current != null) ? current.ApplicationLifetime : null);
		IClassicDesktopStyleApplicationLifetime val = (IClassicDesktopStyleApplicationLifetime)(object)((obj2 is IClassicDesktopStyleApplicationLifetime) ? obj2 : null);
		if (val != null)
		{
			((IControlledApplicationLifetime)val).Shutdown(0);
		}
		else
		{
			((Window)this).Close();
		}
	}

	private void RevealInstantly()
	{
		try
		{
			_revealHost.Opacity = 1.0;
		}
		catch
		{
		}
	}

	/// <summary>
	/// Вид рекомендаций тяжёлый (свои карточки, фильтры, полоса жанров), а нужен только
	/// при переходе в раздел. Строим один раз — при первом показе.
	/// </summary>
	private void EnsureRecsView()
	{
		if (_recsView != null)
		{
			return;
		}
		System.Diagnostics.Stopwatch recsClock = System.Diagnostics.Stopwatch.StartNew();
		Control view = (Control)(object)new RecommendationsView(TitleInLibrary, delegate(string text)
		{
			SetStatus(text);
		}, delegate(int count)
		{
			_recsFound = count;
			if (_recsVisible)
			{
				ShowRecsCount();
			}
		}, () => _settings.SearchEngine);
		_recsView = view;
		((AvaloniaList<Control>)(object)((Panel)_recsHost).Children).Add(view);
		Mark($"UI: рекомендации (лениво, {recsClock.ElapsedMilliseconds} мс)");
	}

	private void ShowRecommendations()
	{
		EnsureRecsView();
		_recsVisible = true;
		_libraryHost.IsVisible = false;
		_recsHost.IsVisible = true;
		if (_sectionLabel != null)
		{
			_sectionLabel.Text = Loc.T("nav.recs");
		}
		ShowRecsCount();
		ApplyFavChipState();
	}

	private void ShowLibrary()
	{
		if (_recsVisible)
		{
			_recsVisible = false;
			_recsHost.IsVisible = false;
			_libraryHost.IsVisible = true;
			if (_sectionLabel != null)
			{
				_sectionLabel.Text = Loc.T("nav.library");
			}
			UpdateCount();
			ApplyFavChipState();
		}
	}

	private void ShowRecsCount()
	{
		if (_countLabel != null)
		{
			_countLabel.Text = Loc.F("recs.found", _recsFound);
		}
	}

	private bool TitleInLibrary(string title)
	{
		string text = NormalizeTitle(title);
		if (text.Length < 3)
		{
			return false;
		}
		List<string> libraryIndex = LibraryIndex;
		if (libraryIndex.Count == 0)
		{
			return false;
		}
		foreach (string item in libraryIndex)
		{
			if (item.Contains(text))
			{
				return true;
			}
		}
		if (text.Length > 6)
		{
			foreach (string item2 in libraryIndex)
			{
				if (item2.Length >= 3 && text.Contains(item2))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static string NormalizeTitle(string s)
	{
		return new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
	}

	private void ApplyFavChipState()
	{
		if (_recChip != null)
		{
			Border? recChip = _recChip;
			IBrush baseBg = (IBrush)(_recsVisible ? ((object)Skin.PanelAlt) : ((object)Brushes.Transparent));
			Ui.SetColors(recChip, baseBg, _recsVisible ? Skin.CardHover : Skin.PanelAlt);
		}
		if (_recLabel != null)
		{
			_recLabel.Foreground = (_recsVisible ? Skin.Text : Skin.TextMuted);
		}
		if (_recsVisible)
		{
			if (_favChip != null)
			{
				Ui.SetColors(_favChip, (IBrush)(object)Brushes.Transparent, Skin.PanelAlt);
			}
			if (_favChipLabel != null)
			{
				_favChipLabel.Foreground = Skin.TextMuted;
			}
			if (_allChip != null)
			{
				Ui.SetColors(_allChip, (IBrush)(object)Brushes.Transparent, Skin.PanelAlt);
			}
			if (_allLabel != null)
			{
				_allLabel.Foreground = Skin.TextMuted;
			}
			return;
		}
		if (_favChip != null)
		{
			Border? favChip = _favChip;
			IBrush baseBg2 = (IBrush)(_favoritesOnly ? ((object)Skin.PanelAlt) : ((object)Brushes.Transparent));
			Ui.SetColors(favChip, baseBg2, _favoritesOnly ? Skin.CardHover : Skin.PanelAlt);
		}
		if (_favChipLabel != null)
		{
			_favChipLabel.Foreground = (_favoritesOnly ? Skin.Text : Skin.TextMuted);
		}
		if (_allChip != null)
		{
			Border? allChip = _allChip;
			IBrush baseBg3 = (IBrush)(_favoritesOnly ? ((object)Brushes.Transparent) : ((object)Skin.PanelAlt));
			Ui.SetColors(allChip, baseBg3, _favoritesOnly ? Skin.PanelAlt : Skin.CardHover);
		}
		if (_allLabel != null)
		{
			_allLabel.Foreground = (_favoritesOnly ? Skin.TextMuted : Skin.Text);
		}
	}

	private void ProcessBegin(string key, string text)
	{
		string key2 = key;
		string text2 = text;
		RunOnUi(delegate
		{
			_processes.RemoveAll(((string Key, string Text) p) => p.Key == key2);
			_processes.Add((key2, text2));
			RefreshProcessStatus();
		});
	}

	private void ProcessUpdate(string key, string text)
	{
		string key2 = key;
		string text2 = text;
		RunOnUi(delegate
		{
			int num = _processes.FindIndex(((string Key, string Text) p) => p.Key == key2);
			if (num < 0)
			{
				_processes.Add((key2, text2));
			}
			else
			{
				_processes[num] = (key2, text2);
			}
			RefreshProcessStatus();
		});
	}

	private void ProcessEnd(string key)
	{
		string key2 = key;
		RunOnUi(delegate
		{
			_processes.RemoveAll(((string Key, string Text) p) => p.Key == key2);
			RefreshProcessStatus();
		});
	}

	private void RefreshProcessStatus()
	{
		if (_processes.Count > 0)
		{
			List<(string, string)> processes = _processes;
			SetStatus(processes[processes.Count - 1].Item2);
		}
		else
		{
			SetStatus(_idleStatus());
		}
	}

	private string _idleStatus()
	{
		return Loc.F("status.done", _games.Count);
	}

	private void RunOnUi(Action action)
	{
		if (Dispatcher.UIThread.CheckAccess())
		{
			action();
		}
		else
		{
			Dispatcher.UIThread.Post(action, default(DispatcherPriority));
		}
	}

	private void SetStatus(string text)
	{
		string text2 = text;
		if (_processToast == null)
		{
			return;
		}
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post((Action)delegate
			{
				SetStatus(text2);
			}, default(DispatcherPriority));
		}
		else
		{
			if (_processToast.Text == text2)
			{
				return;
			}
			_statusPending = text2;
			if (_statusSwitching)
			{
				return;
			}
			_statusSwitching = true;
			DispatcherTimer.RunOnce((Action)delegate
			{
				_statusSwitching = false;
				if (_processToast != null && _statusPending != null)
				{
					string statusPending = _statusPending;
					_statusPending = null;
					if (statusPending == _idleStatus())
					{
						_processToast.Hide();
					}
					else
					{
						_processToast.Show(statusPending);
					}
				}
			}, TimeSpan.FromMilliseconds((!string.IsNullOrEmpty(text2)) ? 90 : 0), default(DispatcherPriority));
		}
	}

	private int PrunePhantomGames()
	{
		int count = _games.Count;
		_games.RemoveAll((GameEntry g) => g.Source != null && !Launchers.LooksInstalled(g.FolderPath));
		return count - _games.Count;
	}

	private void ShowEvent(string text, bool sticky = false)
	{
		string text2 = text;
		if (_eventToast == null)
		{
			return;
		}
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post((Action)delegate
			{
				ShowEvent(text2, sticky);
			}, default(DispatcherPriority));
			return;
		}
		_eventToast.Show(text2);
		DispatcherTimer? eventTimer = _eventTimer;
		if (eventTimer != null)
		{
			eventTimer.Stop();
		}
		_eventTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(sticky ? 6 : 4)
		};
		_eventTimer.Tick += delegate
		{
			DispatcherTimer? eventTimer2 = _eventTimer;
			if (eventTimer2 != null)
			{
				eventTimer2.Stop();
			}
			_eventToast?.Hide();
		};
		_eventTimer.Start();
	}

	private static string FolderKey(string? path)
	{
		if (!string.IsNullOrWhiteSpace(path))
		{
			return path.Trim().TrimEnd('\\', '/').Replace('\\', '/')
				.ToLowerInvariant();
		}
		return "";
	}

	private bool IsHiddenFolder(string? folder)
	{
		string text = FolderKey(folder);
		if (text.Length > 0)
		{
			return _settings.HiddenGames.Contains(text);
		}
		return false;
	}

	private void HideForever(GameEntry game)
	{
		string text = FolderKey(game.FolderPath);
		if (text.Length != 0 && !_settings.HiddenGames.Contains(text))
		{
			_settings.HiddenGames.Add(text);
			_settings.HiddenTitles[text] = game.Title;
			Storage.SaveSettings(_settings);
			AppPaths.Log($"Игра скрыта навсегда: {game.Title} ({text})");
		}
	}

	private void RefreshIdleStatus()
	{
		if (_processes.Count == 0)
		{
			RunOnUi(RefreshProcessStatus);
		}
	}

	private void UpdateCount()
	{
		if (_countLabel != null)
		{
			_countLabel.Text = Loc.F("status.games", _games.Count);
		}
	}

	private async Task StartScanAsync()
	{
		if (_scanning)
		{
			return;
		}
		bool autoScan = _settings.AutoScan;
		if (!autoScan && _settings.Folders.Count == 0 && _settings.Launchers.Count == 0)
		{
			ShowEvent(Loc.T("status.noFolders"));
			ShowSettings();
			return;
		}
		_scanning = true;
		_scanButton.Opacity = 0.55;
		((InputElement)_scanButton).IsEnabled = false;
		_workCts?.Cancel();
		_workCts = new CancellationTokenSource();
		CancellationToken ct = _workCts.Token;
		_processToast?.TogglePulse(on: true);
		ProcessBegin("scan", Loc.T(autoScan ? "auto.step.launchers" : "status.scanFolders"));
		try
		{
			if (autoScan)
			{
				await AutoScanAsync(ct);
				return;
			}
			List<string> folders = _settings.Folders.ToList();
			List<string> extra = _settings.ExtraIgnoreExe.ToList();
			List<ScanHit> list = ((folders.Count > 0) ? (await Task.Run(() => Scanner.Scan(folders, delegate(string message)
			{
				string message2 = message;
				Dispatcher.UIThread.Post((Action)delegate
				{
					ShowEvent(message2, sticky: true);
				}, default(DispatcherPriority));
			}, extra, null, () => ct.IsCancellationRequested), ct)) : new List<ScanHit>());
			List<ScanHit> list2 = list;
			List<ScanHit> hits = list2;
			if (!ct.IsCancellationRequested)
			{
				int count = hits.Count;
				hits = hits.Where((ScanHit h) => !Scanner.LooksLikeProgram(h.Title, h.ExePath)).ToList();
				int num = count - hits.Count;
				if (num > 0)
				{
					AppPaths.Log($"Отсеяно программ: {num}");
					ShowEvent(Loc.F("status.filtered", num), sticky: true);
				}
				MergeHits(hits);
				int num2 = await ImportLaunchersAsync(quiet: true);
				Storage.SaveGames(_games);
				RenderGrid();
				string text = ((folders.Count > 0) ? Loc.F("status.found", hits.Count) : "");
				if (num2 > 0)
				{
					text = (text + "  " + Loc.F("status.launchers", num2)).Trim();
				}
				ProcessUpdate("scan", (text.Length > 0) ? text : Loc.T("status.scanFolders"));
				await FetchArtworkForAsync(_games.Where((GameEntry g) => string.IsNullOrEmpty(g.CoverFile) || !File.Exists(g.CoverFile)).ToList());
				Storage.SaveGames(_games);
				ComputeSizesAsync();
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex2)
		{
			AppPaths.Log("Scan failed: " + ex2);
			ShowEvent(Loc.T("status.launchError"));
		}
		finally
		{
			_scanning = false;
			_processToast?.TogglePulse(on: false);
			_scanButton.Opacity = 1.0;
			((InputElement)_scanButton).IsEnabled = true;
			ProcessEnd("scan");
		}
	}

	/// <summary>
	/// Добавить игру вручную: выбрать папку, проверить название и файл запуска,
	/// подтянуть обложку. Ничего не проверяет «это игра?» — раз человек сказал, значит игра.
	/// </summary>
	private async Task AddGameManuallyAsync()
	{
		try
		{
			AddGameResult? result = await AddGameDialog.ShowAsync(this, delegate(string folder)
			{
				return _games.Any((GameEntry g) => string.Equals(g.FolderPath, folder, StringComparison.OrdinalIgnoreCase));
			});
			if (result == null)
			{
				return;
			}
			if (!result.Ok)
			{
				ShowEvent(result.Error ?? Loc.T("addGame.failed"));
				return;
			}
			GameEntry entry = new GameEntry
			{
				Title = result.Title,
				FolderPath = result.Folder,
				ExePath = result.Exe,
				AddedUtc = DateTime.UtcNow
			};
			_games.Add(entry);
			Storage.SaveGames(_games);
			RenderGrid();
			AppPaths.Log($"добавлено вручную: {entry.Title} ({entry.FolderPath})");
			ShowEvent(Loc.F("addGame.added", entry.Title));
			await FetchArtworkForAsync(new List<GameEntry> { entry });
			Storage.SaveGames(_games);
			RenderGrid();
			ComputeSizesAsync();
		}
		catch (Exception ex)
		{
			AppPaths.Log("AddGameManually: " + ex);
			ShowEvent(Loc.T("addGame.failed"));
		}
	}

	private async Task AutoScanAsync(CancellationToken ct)
	{
		AutoScanner.Report report = await Task.Run(() => AutoScanner.Scan(delegate(string message)
		{
			string message3 = message;
			Dispatcher.UIThread.Post((Action)delegate
			{
				ProcessUpdate("scan", message3);
			}, default(DispatcherPriority));
		}, () => ct.IsCancellationRequested), ct);
		if (ct.IsCancellationRequested)
		{
			return;
		}
		AppPaths.Log(report.Summary);
		ProcessUpdate("scan", Loc.F("status.autoFound", report.Hits.Count));
		List<ScanHit> hits = report.Hits.Select((AutoScanner.Hit hit) => new ScanHit
		{
			Title = hit.Title,
			FolderPath = hit.Folder,
			ExePath = hit.Exe
		}).ToList();
		if (hits.Count > 0)
		{
			GameVerifier.Result result = await GameVerifier.VerifyAsync(hits.Select((ScanHit h) => h.Title), delegate(string message)
			{
				string message2 = message;
				Dispatcher.UIThread.Post((Action)delegate
				{
					ProcessUpdate("scan", message2);
				}, default(DispatcherPriority));
			}, () => ct.IsCancellationRequested, ct);
			HashSet<string> keep = new HashSet<string>(result.Confirmed, StringComparer.OrdinalIgnoreCase);
			int count = hits.Count;
			hits = hits.Where((ScanHit h) => keep.Contains(h.Title)).ToList();
			AppPaths.Log($"проверено {count}, отсеяно {count - hits.Count}");
			if (result.Rejected.Count > 0)
			{
				ProcessUpdate("scan", Loc.F("auto.verified", hits.Count, count - hits.Count));
			}
		}
		int added = MergeHits(hits);
		int num = await ImportLaunchersAsync(quiet: true);
		Storage.SaveGames(_games);
		RenderGrid();
		string text = Loc.F("status.found", added);
		if (num > 0)
		{
			text = (text + "  " + Loc.F("status.launchers", num)).Trim();
		}
		ProcessUpdate("scan", text);
		await FetchArtworkForAsync(_games.Where((GameEntry g) => string.IsNullOrEmpty(g.CoverFile) || !File.Exists(g.CoverFile)).ToList());
		Storage.SaveGames(_games);
		ComputeSizesAsync();
	}

	private async Task CleanupLibraryAsync()
	{
		if (_scanning)
		{
			return;
		}
		List<GameEntry> junk = _games.Where((GameEntry g) => g.Source == null && !g.Favorite && !AutoScanner.IsPlausibleGameFolder(g.FolderPath)).ToList();
		foreach (GameEntry item in junk)
		{
			AppPaths.Log("убрано из библиотеки (служебная папка): " + item.Title + " (" + item.FolderPath + ")");
			_games.Remove(item);
		}
		List<GameEntry> candidates = _games.Where((GameEntry g) => g.Source == null && !g.Favorite && !string.IsNullOrWhiteSpace(g.Title)).ToList();
		if (candidates.Count == 0)
		{
			// нечего проверять: библиотека уже чистая — сообщаем и выходим
			_processToast?.SetBusy(false);
			ShowEvent(Loc.T("status.nothingCleaned"));
			return;
		}
		// Держим плашку «занято» до самого конца: раньше она могла исчезнуть
		// посреди проверки, и казалось, что кнопка «Убрать всё, что не игра» зависла.
		_processToast?.SetBusy(true);
		_processToast?.TogglePulse(on: true);
		ProcessBegin("cleanup", Loc.T("auto.step.verify"));
		AppPaths.Log("cleanup: старт, кандидатов " + candidates.Count);
		try
		{
			HashSet<string> keep = new HashSet<string>((await GameVerifier.VerifyAsync(candidates.Select((GameEntry g) => g.Title), delegate(string message)
			{
				string message2 = message;
				Dispatcher.UIThread.Post((Action)delegate
				{
					ProcessUpdate("cleanup", message2);
				}, default(DispatcherPriority));
			})).Confirmed, StringComparer.OrdinalIgnoreCase);
			List<GameEntry> list = candidates.Where((GameEntry g) => !keep.Contains(g.Title)).ToList();
			foreach (GameEntry item2 in list)
			{
				AppPaths.Log("убрано из библиотеки: " + item2.Title + " (" + item2.FolderPath + ")");
				_games.Remove(item2);
			}
			int num = junk.Count + list.Count;
			if (num == 0)
			{
				ShowEvent(Loc.T("status.nothingCleaned"));
				return;
			}
			Storage.SaveGames(_games);
			RenderGrid();
			UpdateCount();
			ShowEvent(Loc.F("status.cleaned", num), sticky: true);
		}
		catch (Exception ex)
		{
			// Ошибка сети или Steam не должна оставлять плашку висеть навсегда.
			AppPaths.Log("cleanup: ошибка — " + ex.Message);
			ShowEvent(Loc.T("status.nothingCleaned"));
		}
		finally
		{
			_processToast?.TogglePulse(on: false);
			_processToast?.SetBusy(false);
			ProcessEnd("cleanup");
			AppPaths.Log("cleanup: готово");
		}
	}

	private async Task<int> ImportLaunchersAsync(bool quiet = false)
	{
		List<string> enabled = _settings.Launchers.ToList();
		if (enabled.Count == 0 && _games.All((GameEntry g) => g.Source == null))
		{
			return 0;
		}
		List<LauncherGame> found;
		try
		{
			List<LauncherGame> list = ((enabled.Count == 0) ? new List<LauncherGame>() : (await Task.Run(() => Launchers.Import(enabled))));
			List<LauncherGame> list2 = list;
			found = list2;
		}
		catch (Exception ex)
		{
			AppPaths.Log("Import launchers failed: " + ex.Message);
			return 0;
		}
		if (!quiet)
		{
			ProcessBegin("import", Loc.T("status.importing"));
		}
		int count = found.Count;
		found = found.Where((LauncherGame f) => !Scanner.LooksLikeProgram(f.Title, string.IsNullOrWhiteSpace(f.Exe) ? f.Folder : f.Exe)).ToList();
		if (count != found.Count)
		{
			AppPaths.Log($"Отсеяно программ среди игр лаунчеров: {count - found.Count}");
		}
		MergeImported(found);
		int num = PrunePhantomGames();
		if (num > 0)
		{
			AppPaths.Log($"Убрано несуществующих игр: {num}");
		}
		int num2 = RemoveDuplicates();
		if (num2 > 0)
		{
			AppPaths.Log($"Убрано дублей: {num2}");
			ShowEvent(Loc.F("status.dupes", num2), sticky: true);
		}
		Storage.SaveGames(_games);
		if (!quiet)
		{
			RenderGrid();
			ProcessUpdate("import", Loc.F("status.launchers", found.Count));
			ProcessEnd("import");
		}
		HashSet<string> imported = new HashSet<string>(found.Select((LauncherGame f) => f.Folder), StringComparer.OrdinalIgnoreCase);
		List<GameEntry> list3 = _games.Where((GameEntry g) => imported.Contains(g.FolderPath) && (string.IsNullOrEmpty(g.CoverFile) || !File.Exists(g.CoverFile))).ToList();
		if (list3.Count > 0)
		{
			await FetchArtworkForAsync(list3);
		}
		return found.Count;
	}

	private int MergeImported(List<LauncherGame> found)
	{
		int num = 0;
		Dictionary<string, GameEntry> dictionary = new Dictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
		foreach (GameEntry game in _games)
		{
			if (!string.IsNullOrWhiteSpace(game.FolderPath) && !dictionary.ContainsKey(game.FolderPath))
			{
				dictionary[game.FolderPath] = game;
			}
		}
		Dictionary<string, GameEntry> dictionary2 = new Dictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
		foreach (GameEntry game2 in _games)
		{
			dictionary2.TryAdd(Normalize(game2.Title), game2);
		}
		foreach (LauncherGame item in found)
		{
			if (IsHiddenFolder(item.Folder))
			{
				continue;
			}
			GameEntry value2;
			if (dictionary.TryGetValue(item.Folder, out var value))
			{
				value.Source = item.Launcher;
				if (item.AppId.HasValue && !value.SteamAppId.HasValue)
				{
					value.SteamAppId = item.AppId;
				}
				if (string.IsNullOrEmpty(value.ExternalId) && !string.IsNullOrEmpty(item.ExternalId))
				{
					value.ExternalId = item.ExternalId;
				}
				if (string.IsNullOrEmpty(value.ExePath))
				{
					value.ExePath = item.Exe;
				}
				if (!value.Locked && string.IsNullOrWhiteSpace(value.Title))
				{
					value.Title = item.Title;
				}
			}
			else if (dictionary2.TryGetValue(Normalize(item.Title), out value2))
			{
				if (value2.Source == null)
				{
					value2.Source = item.Launcher;
				}
			}
			else
			{
				GameEntry gameEntry = new GameEntry
				{
					Title = item.Title,
					FolderPath = item.Folder,
					ExePath = item.Exe,
					Source = item.Launcher,
					FromLauncher = true,
					SteamAppId = item.AppId,
					ExternalId = (string.IsNullOrEmpty(item.ExternalId) ? null : item.ExternalId),
					AddedUtc = DateTime.UtcNow
				};
				_games.Add(gameEntry);
				dictionary[item.Folder] = gameEntry;
				num++;
			}
		}
		HashSet<string> keep = new HashSet<string>(found.Select((LauncherGame f) => f.Folder), StringComparer.OrdinalIgnoreCase);
		_games.RemoveAll((GameEntry g) => g.FromLauncher && !keep.Contains(g.FolderPath));
		foreach (GameEntry item2 in _games.Where((GameEntry g) => !g.FromLauncher && g.Source != null && !keep.Contains(g.FolderPath)))
		{
			item2.Source = null;
		}
		return num;
	}

	private int RemoveDuplicates()
	{
		Dictionary<string, GameEntry> dictionary = new Dictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
		List<GameEntry> list = new List<GameEntry>();
		foreach (GameEntry item in _games.OrderByDescending(Score).ThenBy((GameEntry g) => g.AddedUtc))
		{
			string text = Normalize(item.Title);
			if (text.Length == 0)
			{
				continue;
			}
			if (!dictionary.TryGetValue(text, out var value))
			{
				dictionary[text] = item;
				continue;
			}
			if (string.IsNullOrEmpty(value.ExePath))
			{
				value.ExePath = item.ExePath;
			}
			if (string.IsNullOrEmpty(value.CoverFile) && !string.IsNullOrEmpty(item.CoverFile))
			{
				value.CoverFile = item.CoverFile;
				value.CoverIsPortrait = item.CoverIsPortrait;
			}
			if (!value.SteamAppId.HasValue)
			{
				value.SteamAppId = item.SteamAppId;
			}
			if (string.IsNullOrEmpty(value.ExternalId))
			{
				value.ExternalId = item.ExternalId;
			}
			if (value.Source == null)
			{
				value.Source = item.Source;
			}
			if (item.PlayCount > value.PlayCount)
			{
				value.PlayCount = item.PlayCount;
			}
			if (item.LastPlayedUtc.HasValue && (!value.LastPlayedUtc.HasValue || item.LastPlayedUtc > value.LastPlayedUtc))
			{
				value.LastPlayedUtc = item.LastPlayedUtc;
			}
			value.Favorite |= item.Favorite;
			list.Add(item);
			AppPaths.Log($"Дубль убран: {item.Title} ({item.FolderPath}) → оставлено {value.FolderPath}");
		}
		foreach (GameEntry item2 in list)
		{
			_games.Remove(item2);
		}
		return list.Count;
		static int Score(GameEntry g)
		{
			return (Directory.Exists(g.FolderPath) ? 4 : 0) + ((!string.IsNullOrEmpty(g.CoverFile) && File.Exists(g.CoverFile)) ? 2 : 0) + (g.FromLauncher ? 1 : 0);
		}
	}

	private static string Normalize(string title)
	{
		return new string(title.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
	}

	private int MergeHits(List<ScanHit> hits)
	{
		int num = 0;
		Dictionary<string, GameEntry> dictionary = new Dictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
		foreach (GameEntry game in _games)
		{
			if (!dictionary.ContainsKey(game.FolderPath))
			{
				dictionary[game.FolderPath] = game;
			}
		}
		foreach (ScanHit hit in hits)
		{
			if (IsHiddenFolder(hit.FolderPath))
			{
				continue;
			}
			if (dictionary.TryGetValue(hit.FolderPath, out var value))
			{
				value.ExePath = hit.ExePath;
				if (!value.Locked)
				{
					value.Title = hit.Title;
				}
				continue;
			}
			GameEntry gameEntry = new GameEntry
			{
				Title = hit.Title,
				FolderPath = hit.FolderPath,
				ExePath = hit.ExePath,
				AddedUtc = DateTime.UtcNow
			};
			_games.Add(gameEntry);
			dictionary[hit.FolderPath] = gameEntry;
			num++;
		}
		_games.RemoveAll((GameEntry g) => !Directory.Exists(g.FolderPath));
		return num;
	}

	private async Task FetchArtworkForAsync(List<GameEntry> games)
	{
		if (games.Count == 0)
		{
			return;
		}
		if (_artCts == null)
		{
			_artCts = new CancellationTokenSource();
		}
		CancellationToken ct = _artCts.Token;
		SemaphoreSlim semaphore = new SemaphoreSlim(3);
		int done = 0;
		int total = games.Count;
		string key = _settings.SteamGridDbKey;
		_processToast?.TogglePulse(on: true);
		ProcessBegin("artwork", Loc.F("status.art", 0, total));
		IEnumerable<Task> tasks = ((IEnumerable<GameEntry>)games).Select((Func<GameEntry, Task>)async delegate(GameEntry game)
		{
			GameEntry game2 = game;
			await semaphore.WaitAsync();
			using CancellationTokenSource gameCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			gameCts.CancelAfter(TimeSpan.FromSeconds(60.0));
			CancellationToken gameCt = gameCts.Token;
			try
			{
				if (!gameCt.IsCancellationRequested)
				{
					string text = null;
					bool coverIsPortrait = false;
					int? appId = null;
					string matchedName = null;
					if (game2.SteamAppId.HasValue)
					{
						(text, coverIsPortrait) = await Artwork.DirectAsync(game2.SteamAppId.Value, game2.Id, gameCt);
						if (text == null && game2.Source != "steam")
						{
							game2.SteamAppId = null;
						}
					}
					if (text == null)
					{
						(text, coverIsPortrait, appId, matchedName) = await Artwork.AutoArtworkAsync(game2.Title, key, game2.Id, gameCt);
					}
					if (matchedName != null)
					{
						game2.SteamName = matchedName;
						if (appId.HasValue && text != null)
						{
							game2.SteamAppId = appId;
						}
					}
					if (text != null)
					{
						game2.CoverFile = text;
						game2.CoverIsPortrait = coverIsPortrait;
						await Dispatcher.UIThread.InvokeAsync((Action)delegate
						{
							UpdateCover(game2);
							if (_detailLayer.IsVisible)
							{
								ShowDetail(game2);
							}
						});
					}
					Storage.SaveGames(_games);
				}
			}
			catch (OperationCanceledException) when (gameCts.IsCancellationRequested && !ct.IsCancellationRequested)
			{
				AppPaths.Log("Обложка не найдена за минуту: " + game2.Title);
			}
			catch (Exception ex2)
			{
				AppPaths.Log("Artwork failed for " + game2.Title + ": " + ex2.Message);
			}
			finally
			{
				semaphore.Release();
				int num = Interlocked.Increment(ref done);
				ProcessUpdate("artwork", Loc.F("status.art", num, total));
			}
		});
		try
		{
			await Task.WhenAll(tasks);
		}
		catch
		{
		}
		_processToast?.TogglePulse(on: false);
		ProcessEnd("artwork");
	}

	private async Task ComputeSizesAsync()
	{
		List<GameEntry> pending = _games.Where((GameEntry g) => g.SizeBytes <= 0).ToList();
		if (pending.Count == 0)
		{
			return;
		}
		ProcessBegin("sizes", Loc.F("status.sizes", 0, pending.Count));
		SemaphoreSlim semaphore = new SemaphoreSlim(2);
		int done = 0;
		IEnumerable<Task> tasks = ((IEnumerable<GameEntry>)pending).Select((Func<GameEntry, Task>)async delegate(GameEntry game)
		{
			GameEntry game2 = game;
			await semaphore.WaitAsync();
			try
			{
				long num = await Task.Run(() => Scanner.DirectorySize(game2.FolderPath));
				if (num > 0)
				{
					game2.SizeBytes = num;
				}
			}
			catch
			{
			}
			finally
			{
				semaphore.Release();
				int num2 = Interlocked.Increment(ref done);
				if (num2 == pending.Count || num2 % 5 == 0)
				{
					ProcessUpdate("sizes", Loc.F("status.sizes", num2, pending.Count));
				}
			}
		});
		try
		{
			await Task.WhenAll(tasks);
		}
		catch
		{
		}
		Storage.SaveGames(_games);
		ProcessEnd("sizes");
	}
}
