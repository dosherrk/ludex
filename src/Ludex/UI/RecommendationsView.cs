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

public sealed class RecommendationsView : Grid
{
	private const double CardWidth = 296.0;

	private const double CoverHeight = 138.0;

	private const int PageSize = 60;

	private static string _query = "";

	private static string _genre = "all";

	private static string _sort = "catalog";

	private static readonly CatalogFilter _filter = new CatalogFilter();

	private List<StoreGame> _web = new List<StoreGame>();

	private string _webQuery = "";

	private bool _webBusy;

	private int _searchGeneration;

	private static readonly CatalogFilter DefaultFilter = new CatalogFilter();

	private readonly Func<string, bool> _inLibrary;

	private readonly Func<string> _engine;

	private readonly Action<string>? _status;

	private readonly Action<int>? _count;

	private readonly WrapPanel _cards = new WrapPanel
	{
		Orientation = (Orientation)0,
		Margin = new Thickness(2.0)
	};

	private readonly WrapPanel _genreChips = new WrapPanel
	{
		Orientation = (Orientation)0
	};

	private readonly Dictionary<string, (Border Frame, TextBlock Label)> _genres = new Dictionary<string, (Border, TextBlock)>();

	private readonly Dictionary<int, Image> _cardCovers = new Dictionary<int, Image>();

	private readonly StackPanel _emptyBox;

	private readonly TextBlock _found = Ui.T("", 11.5, Skin.TextDim, null, (TextWrapping)0);

	private readonly TextBlock _note = Ui.T("", 11.0, Skin.TextDim, null, (TextWrapping)0);

	private readonly TextBlock _empty = Ui.T("", 15.0, Skin.Text, (FontWeight)600, (TextWrapping)0);

	private readonly TextBlock _loadingText = Ui.T(Loc.T("catalog.loadingStart"), 13.0, Skin.TextMuted, null, (TextWrapping)0);

	private readonly Border _loading;

	private readonly Border _detail;

	private readonly Border _more;

	private readonly TextBlock _filterCount;

	private readonly WrapPanel _presetRow;

	private readonly TextBlock _presetHint;

	private Control? _filterAnchor;

	private Border? _resetButton;

	private readonly Dictionary<int, bool> _inLib = new Dictionary<int, bool>();

	private readonly List<StoreGame> _mine = new List<StoreGame>();

	private int _inLibStamp = -1;

	private readonly DispatcherTimer _searchTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(320.0)
	};

	private CancellationTokenSource? _webCts;

	private List<StoreGame> _games = new List<StoreGame>();

	private CancellationTokenSource? _cts;

	private bool _busy;

	private int _shown = 60;

	private string _favouritesSignature = "";

	private static readonly Dictionary<int, DateTime> _coverFailed = new Dictionary<int, DateTime>();

	private static readonly Dictionary<string, Bitmap> _bitmaps = new Dictionary<string, Bitmap>();

	public RecommendationsView(Func<string, bool> inLibrary, Action<string>? status = null, Action<int>? count = null, Func<string>? engine = null)
	{
		_inLibrary = inLibrary;
		_engine = engine ?? ((Func<string>)(() => "google"));
		_status = status;
		_count = count;
		((Grid)this).RowDefinitions = new RowDefinitions("Auto,*");
		StackPanel val = new StackPanel
		{
			Spacing = 9.0,
			Margin = new Thickness(22.0, 12.0, 22.0, 8.0)
		};
		_searchTimer.Tick += delegate
		{
			_searchTimer.Stop();
			_shown = 60;
			Refresh(quiet: true);
			SearchStoreAsync();
		};
		_web = StoreSearch.Cached(_query);
		if (_web.Count > 0)
		{
			_webQuery = _query.Trim();
		}
		TextBox val2 = Ui.SearchBox(_query, Loc.T("recs.search"), delegate(string text)
		{
			_query = text ?? "";
			RefreshInstant();
			_webCts?.Cancel();
			_searchTimer.Stop();
			_searchTimer.Start();
			List<StoreGame> list = StoreSearch.Cached(_query);
			if (list.Count != 0)
			{
				_web = list;
				_webQuery = _query.Trim();
				_webBusy = false;
				RefreshCore(quiet: true);
			}
		});
		((Layoutable)val2).Width = 286.0;
		((Layoutable)val2).Height = 38.0;
		Dropdown dropdown = new Dropdown(CatalogFacets.Sorts.Select(((string Value, string Key) x) => Loc.T(x.Key)).ToArray(), Math.Max(0, Array.FindIndex(CatalogFacets.Sorts, ((string Value, string Key) x) => x.Value == _sort)), 190.0, 38.0);
		dropdown.SelectionChanged += delegate(int index)
		{
			_sort = CatalogFacets.Sorts[Math.Clamp(index, 0, CatalogFacets.Sorts.Length - 1)].Value;
			_filter.Sort = _sort;
			_shown = 60;
			Refresh();
		};
		Border val3 = Ui.GhostButton(Loc.T("recs.refresh"), delegate
		{
			LoadAsync(force: true);
		}, Skin.TextMuted);
		((Layoutable)val3).Height = 38.0;
		((Decorator)val3).Padding = new Thickness(14.0, 0.0);
		_filterCount = Ui.T("", 11.5, Skin.OnAccent, (FontWeight)600, (TextWrapping)0);
		_filterCount.IsVisible = false;
		Path val4 = Ui.Icon("M2,4 L14,4 M4,8 L12,8 M6,12 L10,12", Skin.Text, 16.0, 16.0, stroke: true, 1.6);
		StackPanel val5 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 9.0,
			VerticalAlignment = (VerticalAlignment)2
		};
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)val4);
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)Ui.T(Loc.T("facet.button"), 13.0, Skin.Text, (FontWeight)500, (TextWrapping)0));
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)_filterCount);
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)Ui.Icon("M1,3 L6,8 L11,3", Skin.TextMuted, 12.0, 9.0, stroke: true, 1.7));
		Border val6 = Ui.Clickable((Control)val5, (Action?)OpenFilters, (IBrush?)(object)Brushes.Transparent, Skin.CardHover, (CornerRadius?)new CornerRadius(7.0), (Thickness?)new Thickness(14.0, 0.0), Skin.Border, (Thickness?)new Thickness(1.0));
		((Layoutable)val6).Height = 38.0;
		_filterAnchor = (Control?)(object)val6;
		_resetButton = Ui.GhostButton(Loc.T("facet.reset"), ResetFilters, Skin.TextMuted);
		((Layoutable)_resetButton).Height = 38.0;
		((Decorator)_resetButton).Padding = new Thickness(14.0, 0.0);
		((Layoutable)_resetButton).Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
		_resetButton.IsVisible = false;
		StackPanel val7 = new StackPanel
		{
			Orientation = (Orientation)0,
			VerticalAlignment = (VerticalAlignment)2
		};
		((AvaloniaList<Control>)(object)((Panel)val7).Children).Add((Control)(object)val6);
		((AvaloniaList<Control>)(object)((Panel)val7).Children).Add((Control)(object)_resetButton);
		StackPanel val8 = val7;
		Grid val9 = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto")
		};
		((AvaloniaList<Control>)(object)((Panel)val9).Children).Add((Control)(object)val2);
		((Layoutable)val6).Margin = new Thickness(10.0, 0.0, 0.0, 0.0);
		Grid.SetColumn((Control)(object)val8, 1);
		((Layoutable)val8).HorizontalAlignment = (HorizontalAlignment)1;
		((AvaloniaList<Control>)(object)((Panel)val9).Children).Add((Control)(object)val8);
		((Layoutable)val3).Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		Grid.SetColumn((Control)(object)val3, 2);
		((AvaloniaList<Control>)(object)((Panel)val9).Children).Add((Control)(object)val3);
		Grid.SetColumn((Control)(object)dropdown, 3);
		((AvaloniaList<Control>)(object)((Panel)val9).Children).Add((Control)(object)dropdown);
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val9);
		_presetHint = Ui.T("", 11.0, Skin.TextDim, null, (TextWrapping)0);
		((Layoutable)_presetHint).Margin = new Thickness(0.0, -2.0, 0.0, 0.0);
		_presetHint.IsVisible = false;
		_presetRow = new WrapPanel
		{
			Orientation = (Orientation)0
		};
		_presetRow.IsVisible = false;
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)_presetHint);
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)_presetRow);
		((Layoutable)_genreChips).Margin = new Thickness(-2.0, 0.0, 0.0, 0.0);
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)_genreChips);
		BuildGenreChips();
		StackPanel val10 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 10.0,
			Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
		};
		((AvaloniaList<Control>)(object)((Panel)val10).Children).Add((Control)(object)_found);
		_note.Opacity = 0.85;
		((AvaloniaList<Control>)(object)((Panel)val10).Children).Add((Control)(object)_note);
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val10);
		((AvaloniaList<Control>)(object)((Panel)this).Children).Add((Control)(object)val);
		StackPanel val11 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 9.0,
			VerticalAlignment = (VerticalAlignment)2
		};
		((AvaloniaList<Control>)(object)((Panel)val11).Children).Add((Control)(object)Ui.T(Loc.T("recs.more"), 13.0, Skin.OnAccent, (FontWeight)600, (TextWrapping)0));
		((AvaloniaList<Control>)(object)((Panel)val11).Children).Add((Control)(object)Ui.Icon("M1 4 L7 10 L13 4", Skin.OnAccent, 15.0, 15.0, stroke: true, 2.1));
		StackPanel content = val11;
		_more = Ui.Clickable((Control)(object)content, (Action?)delegate
		{
			_shown += 60;
			Refresh();
		}, Skin.Accent, Skin.AccentHover, (CornerRadius?)new CornerRadius(999.0), (Thickness?)new Thickness(26.0, 13.0), (IBrush?)null, (Thickness?)null);
		((Layoutable)_more).HorizontalAlignment = (HorizontalAlignment)2;
		((Layoutable)_more).Margin = new Thickness(0.0, 18.0, 0.0, 24.0);
		_more.BoxShadow = Ui.GlowShadow();
		_more.IsVisible = false;
		Border more = _more;
		Transitions val12 = new Transitions();
		((AvaloniaList<ITransition>)val12).Add((ITransition)new BoxShadowsTransition
		{
			Property = (AvaloniaProperty)(object)Border.BoxShadowProperty,
			Duration = TimeSpan.FromMilliseconds(200.0)
		});
		((Animatable)more).Transitions = val12;
		((InputElement)_more).PointerEntered += delegate
		{
			_more.BoxShadow = Ui.GlowShadow(strong: true);
		};
		((InputElement)_more).PointerExited += delegate
		{
			_more.BoxShadow = Ui.GlowShadow();
		};
		StackPanel val13 = new StackPanel();
		((AvaloniaList<Control>)(object)((Panel)val13).Children).Add((Control)(object)_cards);
		((AvaloniaList<Control>)(object)((Panel)val13).Children).Add((Control)(object)_more);
		StackPanel content2 = val13;
		ScrollViewer val14 = new ScrollViewer
		{
			Content = content2,
			Padding = new Thickness(20.0, 4.0, 14.0, 12.0),
			HorizontalScrollBarVisibility = (ScrollBarVisibility)0,
			VerticalScrollBarVisibility = (ScrollBarVisibility)1
		};
		Ui.SmoothScroll(val14, 130.0);
		Grid.SetRow((Control)(object)val14, 1);
		((AvaloniaList<Control>)(object)((Panel)this).Children).Add((Control)(object)val14);
		_emptyBox = new StackPanel
		{
			Spacing = 6.0,
			HorizontalAlignment = (HorizontalAlignment)2,
			VerticalAlignment = (VerticalAlignment)2,
			IsHitTestVisible = false,
			IsVisible = false
		};
		((AvaloniaList<Control>)(object)((Panel)_emptyBox).Children).Add((Control)(object)_empty);
		((AvaloniaList<Control>)(object)((Panel)_emptyBox).Children).Add((Control)(object)Ui.T(Loc.T("recs.emptyHint"), 12.0, Skin.TextDim, null, (TextWrapping)0));
		Grid.SetRow((Control)(object)_emptyBox, 1);
		((AvaloniaList<Control>)(object)((Panel)this).Children).Add((Control)(object)_emptyBox);
		Border val15 = new Border();
		StackPanel val16 = new StackPanel
		{
			Spacing = 10.0,
			HorizontalAlignment = (HorizontalAlignment)2,
			VerticalAlignment = (VerticalAlignment)2
		};
		((AvaloniaList<Control>)(object)((Panel)val16).Children).Add((Control)(object)_loadingText);
		((Decorator)val15).Child = (Control)val16;
		val15.IsVisible = true;
		_loading = val15;
		Grid.SetRow((Control)(object)_loading, 1);
		((AvaloniaList<Control>)(object)((Panel)this).Children).Add((Control)(object)_loading);
		_detail = new Border
		{
			Background = Skin.Overlay,
			IsVisible = false
		};
		((InputElement)_detail).PointerPressed += delegate(object? _, PointerPressedEventArgs e)
		{
			if (((RoutedEventArgs)e).Source == _detail)
			{
				_detail.IsVisible = false;
			}
		};
		Grid.SetRowSpan((Control)(object)_detail, 2);
		((AvaloniaList<Control>)(object)((Panel)this).Children).Add((Control)(object)_detail);
		((InputElement)this).KeyDown += delegate(object? _, KeyEventArgs e)
		{
			if ((int)e.Key == 13 && _detail.IsVisible)
			{
				_detail.IsVisible = false;
				((RoutedEventArgs)e).Handled = true;
			}
		};
		LoadAsync(force: false);
	}

	private async Task LoadAsync(bool force)
	{
		if (_busy)
		{
			return;
		}
		_busy = true;
		_cts?.Cancel();
		_cts = new CancellationTokenSource();
		CancellationToken ct = _cts.Token;
		_loading.IsVisible = true;
		_loadingText.Text = Loc.T("catalog.loadingStart");
		_note.Text = "";
		_more.IsVisible = false;
		try
		{
			List<StoreGame> games = await StoreCatalog.LoadAsync(force, delegate(string text)
			{
				string text2 = text;
				Dispatcher.UIThread.Post((Action)delegate
				{
					_loadingText.Text = text2;
				}, default(DispatcherPriority));
			}, ct);
			if (!ct.IsCancellationRequested)
			{
				_games = games;
				_loading.IsVisible = false;
				if (_games.Count == 0)
				{
					_games = OfflineGames();
					_note.Text = (string.IsNullOrEmpty(StoreCatalog.LastError) ? Loc.T("recs.offline") : Loc.F("recs.offlineWhy", StoreCatalog.LastError));
					_status?.Invoke(Loc.T("recs.offline"));
				}
				else
				{
					DateTime dateTime = ((StoreCatalog.FetchedUtc == default(DateTime)) ? DateTime.Now : StoreCatalog.FetchedUtc.ToLocalTime());
					_note.Text = Loc.F("catalog.updated", dateTime.ToString("HH:mm"));
					_status?.Invoke(Loc.F("recs.loaded", _games.Count));
				}
				BuildGenreChips();
				_shown = 60;
				Refresh();
			}
		}
		catch (Exception ex)
		{
			AppPaths.Log("RecommendationsView load: " + ex.Message);
			_games = OfflineGames();
			_loading.IsVisible = false;
			_note.Text = Loc.F("recs.offlineWhy", ex.Message);
			BuildGenreChips();
			Refresh();
		}
		finally
		{
			_busy = false;
		}
	}

	private static List<StoreGame> OfflineGames()
	{
		List<StoreGame> list = new List<StoreGame>();
		foreach (RecGame game in Recommendations.Games)
		{
			StoreGame storeGame = new StoreGame
			{
				AppId = 0,
				Name = game.Title,
				Developer = game.Developer,
				ReleaseDate = game.Year.ToString()
			};
			storeGame.Genres = game.Genres.Select((string x) => Loc.T("genre." + x)).ToArray();
			storeGame.Description = game.Description;
			storeGame.Sections.Add("all");
			storeGame.Sections.Add("today");
			list.Add(storeGame);
		}
		return list;
	}

	private async Task SearchStoreAsync()
	{
		string text = (_query ?? "").Trim();
		if (text.Length < 2)
		{
			_web.Clear();
			_webQuery = "";
			if (_webBusy)
			{
				_webBusy = false;
				Refresh(quiet: true);
			}
		}
		else
		{
			if (string.Equals(text, _webQuery, StringComparison.CurrentCultureIgnoreCase) && _web.Count > 0)
			{
				return;
			}
			int generation = ++_searchGeneration;
			_webCts?.Cancel();
			_webCts?.Dispose();
			_webCts = new CancellationTokenSource();
			CancellationToken token = _webCts.Token;
			List<StoreGame> list = StoreSearch.Cached(text);
			if (list.Count > 0)
			{
				_web = list;
				_webBusy = false;
				RefreshCore(quiet: true);
				return;
			}
			_webBusy = true;
			RefreshCore(quiet: true);
			List<StoreGame> list2;
			try
			{
				list2 = await StoreSearch.SearchAsync(text, token);
			}
			catch (OperationCanceledException)
			{
				return;
			}
			if (generation == _searchGeneration && !token.IsCancellationRequested)
			{
				bool num = list2.Count == _web.Count && list2.Count > 0 && list2.Zip(_web).All<(StoreGame, StoreGame)>(((StoreGame First, StoreGame Second) pair) => pair.First.AppId == pair.Second.AppId);
				_web = list2;
				_webBusy = false;
				_shown = 60;
				if (!num)
				{
					RefreshCore(quiet: true);
				}
				else
				{
					RefreshInstant();
				}
				EnsureCoversAsync(list2.Take(24).ToList());
			}
		}
	}

	private void OpenFilters()
	{
		Control filterAnchor = _filterAnchor;
		if (filterAnchor != null)
		{
			Rect bounds = this.Bounds;
			double width = Math.Max(420.0, Math.Min(760.0, ((Rect)bounds).Width - 80.0));
			FilterPopover.Show(filterAnchor, _filter, delegate(CatalogFilter applied)
			{
				_filter.CopyFrom(applied);
				_sort = _filter.Sort;
				_genre = "all";
				_shown = 60;
				ApplyChipStyles();
				UpdateFilterBadge();
				Refresh();
			}, width);
		}
	}

	private bool InLibrary(StoreGame game, bool useCache = true)
	{
		if (useCache && _inLib.TryGetValue(game.AppId, out var value))
		{
			return value;
		}
		bool flag = _inLibrary(game.Name);
		_inLib[game.AppId] = flag;
		if (flag)
		{
			_mine.Add(game);
		}
		return flag;
	}

	private void UpdateFilterBadge()
	{
		int count = _filter.Count;
		_filterCount.IsVisible = count > 0;
		_filterCount.Text = ((count > 0) ? count.ToString() : "");
		if (_resetButton != null)
		{
			_resetButton.IsVisible = count > 0;
		}
	}

	private void ResetFilters()
	{
		_filter.CopyFrom(new CatalogFilter());
		_sort = "catalog";
		_filter.Sort = _sort;
		_genre = "all";
		_shown = 60;
		ApplyChipStyles();
		UpdateFilterBadge();
		Refresh();
	}

	private void UpdateFavourites()
	{
		if (_presetRow == null)
		{
			return;
		}
		List<StoreGame> list = ((_mine.Count > 0) ? _mine : _games.Take(0).ToList());
		if (list.Count < 3)
		{
			_presetRow.IsVisible = false;
			_presetHint.IsVisible = false;
			_genreChips.IsVisible = true;
			return;
		}
		List<string> list2 = (from gr in (from t in list.SelectMany((StoreGame g) => g.Themes)
				where !string.IsNullOrWhiteSpace(t)
				where !CatalogFacets.IsGenreTheme(t)
				where (!(t == "Free to Play") && !(t == "Singleplayer")) || 1 == 0
				group t by t into gr
				orderby gr.Count() descending
				select gr).Take(8)
			select gr.Key).ToList();
		List<string> list3 = (from t in list.SelectMany((StoreGame g) => g.Genres.Concat(g.Themes.Where(CatalogFacets.IsGenreTheme)))
			where !string.IsNullOrWhiteSpace(t)
			group t by t into gr
			orderby gr.Count() descending
			select gr.Key).Take(3).ToList();
		List<string> list4 = ((list2.Count > 0) ? list2 : list3);
		string text = string.Join('|', list4) + "#" + list.Count;
		if (text == _favouritesSignature)
		{
			return;
		}
		_favouritesSignature = text;
		((AvaloniaList<Control>)(object)((Panel)_presetRow).Children).Clear();
		_presetRow.IsVisible = list4.Count > 0;
		_presetHint.IsVisible = list4.Count > 0;
		_genreChips.IsVisible = list4.Count == 0;
		_presetHint.Text = Loc.T("facet.favourites") + " · " + Loc.F("facet.favouritesHint", list.Count);
		foreach (string item in list4)
		{
			bool isGenre = CatalogFacets.Genres.Contains<string>(item, StringComparer.CurrentCultureIgnoreCase) || CatalogFacets.IsGenreTheme(item);
			Border val = Ui.Clickable((Control)(object)Ui.T(CatalogFacets.Label(item), 12.0, Skin.Text, (FontWeight)500, (TextWrapping)0), (Action?)delegate
			{
				_filter.Tags.Clear();
				_filter.Genres.Clear();
				if (isGenre)
				{
					_filter.Genres.Add(item);
				}
				else
				{
					_filter.Tags.Add(item);
				}
				_genre = "all";
				_shown = 60;
				ApplyChipStyles();
				UpdateFilterBadge();
				Refresh();
			}, (IBrush?)(object)Brushes.Transparent, Skin.CardHover, (CornerRadius?)new CornerRadius(999.0), (Thickness?)new Thickness(12.0, 6.0), Skin.Border, (Thickness?)new Thickness(1.0));
			((Layoutable)val).Margin = new Thickness(0.0, 0.0, 6.0, 6.0);
			((AvaloniaList<Control>)(object)((Panel)_presetRow).Children).Add((Control)(object)val);
		}
	}

	private void BuildGenreChips()
	{
		((AvaloniaList<Control>)(object)((Panel)_genreChips).Children).Clear();
		_genres.Clear();
		AddChip(_genres, _genreChips, "all", Loc.T("recs.allGenres"), delegate
		{
			_genre = "all";
			_shown = 60;
			ApplyChipStyles();
			Refresh();
		});
		foreach (string genre in (from gr in (from s in _games.SelectMany((StoreGame g) => g.Genres)
				where !string.IsNullOrWhiteSpace(s)
				group s by s into gr
				orderby gr.Count() descending
				select gr).Take(9)
			select gr.Key).ToList())
		{
			AddChip(_genres, _genreChips, genre, genre, delegate
			{
				_genre = genre;
				_shown = 60;
				ApplyChipStyles();
				Refresh();
			});
		}
		if (_genre != "all" && !_genres.ContainsKey(_genre))
		{
			AddChip(_genres, _genreChips, _genre, _genre, delegate
			{
				_genre = "all";
				_shown = 60;
				ApplyChipStyles();
				Refresh();
			});
		}
		ApplyChipStyles();
	}

	private static void AddChip(Dictionary<string, (Border, TextBlock)> map, WrapPanel host, string key, string title, Action onClick, bool bold = false)
	{
		TextBlock val = Ui.T(title, 12.0, Skin.TextMuted, (FontWeight)(bold ? 600 : 500), (TextWrapping)0);
		Border val2 = Ui.Clickable((Control)(object)val, onClick, (IBrush?)(object)Brushes.Transparent, Skin.PanelAlt, (CornerRadius?)new CornerRadius(999.0), (Thickness?)new Thickness(13.0, 7.0), (IBrush?)null, (Thickness?)null);
		((Layoutable)val2).Margin = new Thickness(2.0, 0.0, 4.0, 0.0);
		map[key] = (val2, val);
		((AvaloniaList<Control>)(object)((Panel)host).Children).Add((Control)(object)val2);
	}

	private void ApplyChipStyles()
	{
		foreach (KeyValuePair<string, (Border, TextBlock)> genre in _genres)
		{
			genre.Deconstruct(out var key, out var value);
			(Border, TextBlock) tuple = value;
			string text = key;
			Border item = tuple.Item1;
			TextBlock item2 = tuple.Item2;
			bool flag = text == _genre;
			IBrush baseBg = (IBrush)(flag ? ((object)Skin.PanelAlt) : ((object)Brushes.Transparent));
			Ui.SetColors(item, baseBg, flag ? Skin.CardHover : Skin.PanelAlt);
			item2.Foreground = (flag ? Skin.Text : Skin.TextMuted);
			item2.FontWeight = (FontWeight)(flag ? 600 : 500);
		}
	}

	private List<StoreGame> Visible()
	{
		IEnumerable<StoreGame> enumerable = _games;
		HashSet<int> seen;
		List<StoreGame> merged;
		if (!string.IsNullOrWhiteSpace(_query))
		{
			string s = _query.Trim();
			IEnumerable<StoreGame> source2 = VisibleLocal();
			seen = new HashSet<int>();
			merged = new List<StoreGame>();
			Add(_web.Where((StoreGame g) => g.Name.StartsWith(s, StringComparison.CurrentCultureIgnoreCase)));
			Add(source2.Where((StoreGame g) => g.Name.StartsWith(s, StringComparison.CurrentCultureIgnoreCase)));
			Add(_web);
			Add(source2);
			return merged;
		}
		if (_genre != "all")
		{
			enumerable = enumerable.Where((StoreGame g) => g.Genres.Contains(_genre));
		}
		if (_filter.Genres.Count > 0)
		{
			enumerable = enumerable.Where((StoreGame g) => g.Genres.Any(_filter.Genres.Contains) || g.Genres.Any(delegate(string any)
			{
				string any2 = any;
				return _filter.Genres.Any((string pick) => string.Equals(CatalogFacets.Label(pick), any2, StringComparison.CurrentCultureIgnoreCase));
			}));
		}
		if (_filter.Tags.Count > 0)
		{
			enumerable = enumerable.Where((StoreGame g) => CatalogFacets.HasTheme(g, _filter.Tags));
		}
		if (_filter.Decade.Length > 0)
		{
			int result;
			int from = (int.TryParse(_filter.Decade, out result) ? result : 0);
			enumerable = enumerable.Where((StoreGame g) => YearOf(g) >= from);
		}
		if (_filter.Size.Length > 0)
		{
			enumerable = _filter.Size switch
			{
				"described" => enumerable.Where((StoreGame g) => !string.IsNullOrWhiteSpace(g.Description)), 
				"short" => enumerable.Where((StoreGame g) => !string.IsNullOrWhiteSpace(g.Description) && g.Description.Length <= 160), 
				"long" => enumerable.Where((StoreGame g) => !string.IsNullOrWhiteSpace(g.Description) && g.Description.Length > 160), 
				_ => enumerable, 
			};
		}
		if (_filter.OnlyUnshelved)
		{
			enumerable = enumerable.Where((StoreGame g) => !InLibrary(g));
		}
		return (_filter.Sort switch
		{
			"az" => enumerable.OrderBy<StoreGame, string>((StoreGame g) => g.Name, StringComparer.CurrentCultureIgnoreCase), 
			"za" => enumerable.OrderByDescending<StoreGame, string>((StoreGame g) => g.Name, StringComparer.CurrentCultureIgnoreCase), 
			"new" => enumerable.OrderByDescending(YearOf), 
			"old" => enumerable.OrderBy(YearOf), 
			_ => enumerable, 
		}).ToList();
		void Add(IEnumerable<StoreGame> source)
		{
			foreach (StoreGame item in source)
			{
				if (seen.Add(item.AppId))
				{
					merged.Add(item);
				}
			}
		}
	}

	private IEnumerable<StoreGame> VisibleLocal()
	{
		if (string.IsNullOrWhiteSpace(_query))
		{
			return _games;
		}
		string s = _query.Trim();
		return _games.Where((StoreGame g) => g.Name.Contains(s, StringComparison.CurrentCultureIgnoreCase) || g.Developer.Contains(s, StringComparison.CurrentCultureIgnoreCase) || g.Genres.Any((string x) => x.Contains(s, StringComparison.CurrentCultureIgnoreCase)) || g.Themes.Any((string x) => x.Contains(s, StringComparison.CurrentCultureIgnoreCase)));
	}

	private static int YearOf(StoreGame game)
	{
		if (!int.TryParse(game.ReleaseDate, out var result) || result <= 1900)
		{
			return 0;
		}
		return result;
	}

	private void RefreshInstant()
	{
		if (_found != null)
		{
			if (!string.IsNullOrWhiteSpace(_query))
			{
				int num = VisibleLocal().Count();
				string text = (_webBusy ? Loc.T("catalog.searching") : ((_web.Count <= 0) ? Loc.F("catalog.showing", Math.Min(((AvaloniaList<Control>)(object)((Panel)_cards).Children).Count, num), num) : Loc.F("catalog.foundWeb", Math.Max(num, _web.Count))));
				_found.Text = text;
			}
			else
			{
				_found.Text = Loc.F("catalog.showing", ((AvaloniaList<Control>)(object)((Panel)_cards).Children).Count, _games.Count);
			}
		}
	}

	private void Refresh(bool quiet = false)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		RefreshCore(quiet);
		stopwatch.Stop();
		if (stopwatch.ElapsedMilliseconds >= 40)
		{
			AppPaths.Log($"каталог: перерисовка {stopwatch.ElapsedMilliseconds} мс, запрос «{_query}»");
		}
	}

	private void RefreshCore(bool quiet)
	{
		if (string.IsNullOrWhiteSpace(_query) && _inLibStamp != _games.Count)
		{
			_inLibStamp = _games.Count;
			_inLib.Clear();
			_mine.Clear();
			if (_games.Count <= 4000)
			{
				foreach (StoreGame game in _games)
				{
					InLibrary(game, useCache: false);
				}
			}
		}
		UpdateFilterBadge();
		UpdateFavourites();
		List<StoreGame> list = Visible();
		List<StoreGame> list2 = list.Take(Math.Max(60, _shown)).ToList();
		((AvaloniaList<Control>)(object)((Panel)_cards).Children).Clear();
		_cardCovers.Clear();
		foreach (StoreGame item in list2)
		{
			((AvaloniaList<Control>)(object)((Panel)_cards).Children).Add(BuildCard(item));
		}
		_found.Text = Loc.F("catalog.showing", list2.Count, list.Count);
		if (!string.IsNullOrWhiteSpace(_query))
		{
			if (_webBusy)
			{
				_found.Text = Loc.T("catalog.searching");
			}
			else if (_web.Count > 0)
			{
				_found.Text = Loc.F("catalog.foundWeb", list.Count);
			}
		}
		_empty.Text = (string.IsNullOrWhiteSpace(_query) ? Loc.T("recs.empty") : Loc.F("catalog.nothingFound", _query.Trim()));
		_emptyBox.IsVisible = !_loading.IsVisible && list.Count == 0;
		_more.IsVisible = list.Count > list2.Count;
		_count?.Invoke(list.Count);
		if (list2.Count > 0 && !_loading.IsVisible && !quiet)
		{
			Ui.FadeSlideIn((Visual)(object)_cards, 0, 160, 6.0);
		}
		if (list2.Count > 0 && !quiet)
		{
			EnsureCoversAsync(list2);
		}
	}

	private async Task EnsureCoversAsync(List<StoreGame> slice)
	{
		DateTime now = DateTime.UtcNow;
		DateTime dateTime;
		List<StoreGame> list = (from g in slice
			where g.CoverPath == null || !File.Exists(g.CoverPath)
			where !_coverFailed.TryGetValue(g.AppId, out dateTime) || now - dateTime > TimeSpan.FromMinutes(5.0)
			select g).ToList();
		if (list.Count == 0)
		{
			return;
		}
		CancellationToken token = _cts?.Token ?? CancellationToken.None;
		HttpClient http = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(25.0)
		};
		try
		{
			http.DefaultRequestHeaders.UserAgent.ParseAdd("Ludex/" + Updater.CurrentVersionText);
			SemaphoreSlim gate = new SemaphoreSlim(6);
			try
			{
				await Task.WhenAll(((IEnumerable<StoreGame>)list).Select((Func<StoreGame, Task>)async delegate(StoreGame game)
				{
					StoreGame game2 = game;
					try
					{
						await gate.WaitAsync(token);
					}
					catch
					{
						return;
					}
					try
					{
						string path = System.IO.Path.Combine(StoreFeed.CoversDir, $"{game2.AppId}.jpg");
						if (!File.Exists(path) || new FileInfo(path).Length < 1000)
						{
							byte[] array = await DownloadCoverAsync(http, game2, token);
							if (array == null)
							{
								_coverFailed[game2.AppId] = DateTime.UtcNow;
								return;
							}
							_coverFailed.Remove(game2.AppId);
							await File.WriteAllBytesAsync(path, array, token);
						}
						game2.CoverPath = path;
						Dispatcher.UIThread.Post((Action)delegate
						{
							ApplyCover(game2);
						}, default(DispatcherPriority));
					}
					catch (Exception ex)
					{
						AppPaths.Log($"Catalog cover {game2.AppId}: {ex.Message}");
					}
					finally
					{
						gate.Release();
					}
				}));
			}
			finally
			{
				if (gate != null)
				{
					((IDisposable)gate).Dispose();
				}
			}
		}
		finally
		{
			if (http != null)
			{
				((IDisposable)http).Dispose();
			}
		}
	}

	private static async Task<byte[]?> DownloadCoverAsync(HttpClient http, StoreGame game, CancellationToken ct)
	{
		List<string> urls = new List<string>
		{
			game.CoverUrl,
			$"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.AppId}/capsule_231x87.jpg",
			$"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.AppId}/capsule_616x353.jpg",
			$"https://cdn.akamai.steamstatic.com/steam/apps/{game.AppId}/header.jpg"
		};
		try
		{
			string text = await StoreFeed.HeaderImageAsync(game.AppId, ct);
			if (!string.IsNullOrWhiteSpace(text))
			{
				urls.Add(text);
			}
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch
		{
		}
		foreach (string item in urls)
		{
			if (string.IsNullOrWhiteSpace(item))
			{
				continue;
			}
			try
			{
				byte[] array = await http.GetByteArrayAsync(item, ct);
				if (array.Length > 1000)
				{
					return array;
				}
			}
			catch
			{
			}
		}
		return null;
	}

	private void ApplyCover(StoreGame game)
	{
		if (game.CoverPath == null || !_cardCovers.TryGetValue(game.AppId, out Image value))
		{
			return;
		}
		try
		{
			value.Source = (IImage)new Bitmap(game.CoverPath);
		}
		catch
		{
		}
	}

	private Control BuildCard(StoreGame game)
	{
		StoreGame game2 = game;
		Grid val = new Grid
		{
			Width = 296.0,
			Height = 138.0,
			ClipToBounds = true
		};
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add(Placeholder(game2));
		Image val2 = new Image
		{
			Stretch = (Stretch)3,
			HorizontalAlignment = (HorizontalAlignment)0,
			VerticalAlignment = (VerticalAlignment)0
		};
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val2);
		Bitmap val3 = LoadCover(game2.CoverPath);
		if (val3 != null)
		{
			val2.Source = (IImage)(object)val3;
		}
		_cardCovers[game2.AppId] = val2;
		if (_inLibrary(game2.Name))
		{
			((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)new Border
			{
				Child = (Control)(object)Ui.T(Loc.T("recs.inLibrary"), 10.5, Skin.Text, (FontWeight)600, (TextWrapping)0),
				Background = Skin.PanelAlt,
				CornerRadius = new CornerRadius(4.0),
				Padding = new Thickness(7.0, 3.0),
				Margin = new Thickness(8.0),
				HorizontalAlignment = (HorizontalAlignment)3,
				VerticalAlignment = (VerticalAlignment)1
			});
		}
		Border val4 = new Border
		{
			Opacity = 0.0,
			IsHitTestVisible = false
		};
		LinearGradientBrush val5 = new LinearGradientBrush
		{
			StartPoint = new RelativePoint(0.0, 1.0, (RelativeUnit)0),
			EndPoint = new RelativePoint(0.0, 0.0, (RelativeUnit)0)
		};
		((AvaloniaList<GradientStop>)(object)((GradientBrush)val5).GradientStops).Add(new GradientStop(Color.FromArgb((byte)90, byte.MaxValue, byte.MaxValue, byte.MaxValue), 0.0));
		((AvaloniaList<GradientStop>)(object)((GradientBrush)val5).GradientStops).Add(new GradientStop(Color.FromArgb((byte)0, byte.MaxValue, byte.MaxValue, byte.MaxValue), 0.6));
		val4.Background = (IBrush)val5;
		Transitions val6 = new Transitions();
		((AvaloniaList<ITransition>)val6).Add((ITransition)new DoubleTransition
		{
			Property = (AvaloniaProperty)(object)Visual.OpacityProperty,
			Duration = TimeSpan.FromMilliseconds(160.0)
		});
		((Animatable)val4).Transitions = val6;
		Border hover = val4;
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)hover);
		StackPanel val7 = new StackPanel
		{
			Margin = new Thickness(12.0, 9.0, 12.0, 11.0),
			Spacing = 4.0
		};
		TextBlock val8 = Ui.T(game2.Name, 12.5, Skin.Text, (FontWeight)600, (TextWrapping)0);
		((Layoutable)val8).MaxWidth = 272.0;
		((AvaloniaList<Control>)(object)((Panel)val7).Children).Add((Control)(object)val8);
		Border val9 = new Border
		{
			Background = Skin.CardPlate,
			CornerRadius = new CornerRadius(0.0, 0.0, 6.0, 6.0),
			Child = (Control)(object)val7
		};
		StackPanel val10 = new StackPanel();
		((AvaloniaList<Control>)(object)((Panel)val10).Children).Add((Control)new Border
		{
			CornerRadius = new CornerRadius(6.0, 6.0, 0.0, 0.0),
			ClipToBounds = true,
			Child = (Control)(object)val
		});
		((AvaloniaList<Control>)(object)((Panel)val10).Children).Add((Control)(object)val9);
		Border val11 = new Border
		{
			Child = (Control)(object)val10,
			Background = (IBrush)(object)Brushes.Transparent,
			CornerRadius = new CornerRadius(6.0),
			BoxShadow = Skin.CardShadow,
			Margin = new Thickness(0.0, 0.0, 16.0, 16.0),
			Width = 296.0,
			Cursor = new Cursor((StandardCursorType)9)
		};
		Transitions val12 = new Transitions();
		((AvaloniaList<ITransition>)val12).Add((ITransition)new BoxShadowsTransition
		{
			Property = (AvaloniaProperty)(object)Border.BoxShadowProperty,
			Duration = TimeSpan.FromMilliseconds(160.0)
		});
		((Animatable)val11).Transitions = val12;
		Border card = val11;
		((InputElement)card).PointerEntered += delegate
		{
			card.BoxShadow = Ui.GlowShadow();
			hover.Opacity = 1.0;
		};
		((InputElement)card).PointerExited += delegate
		{
			card.BoxShadow = Skin.CardShadow;
			hover.Opacity = 0.0;
		};
		((InputElement)card).PointerPressed += delegate(object? _, PointerPressedEventArgs e)
		{
			PointerPoint currentPoint = ((PointerEventArgs)e).GetCurrentPoint((Visual)(object)card);
			PointerPointProperties properties = ((PointerPoint)currentPoint).Properties;
			if (((PointerPointProperties)properties).IsLeftButtonPressed)
			{
				((RoutedEventArgs)e).Handled = true;
				ShowDetail(game2);
			}
		};
		return (Control)(object)card;
	}

	private static Control Placeholder(StoreGame game)
	{
		(string A, string B) tuple = RecGame.PosterColors(game.Name);
		string item = tuple.A;
		string item2 = tuple.B;
		Grid val = new Grid();
		LinearGradientBrush val2 = new LinearGradientBrush
		{
			StartPoint = new RelativePoint(0.0, 0.0, (RelativeUnit)0),
			EndPoint = new RelativePoint(1.0, 1.0, (RelativeUnit)0)
		};
		((AvaloniaList<GradientStop>)(object)((GradientBrush)val2).GradientStops).Add(new GradientStop(Color.Parse(item), 0.0));
		((AvaloniaList<GradientStop>)(object)((GradientBrush)val2).GradientStops).Add(new GradientStop(Color.Parse(item2), 1.0));
		((Panel)val).Background = (IBrush)val2;
		Grid val3 = val;
		string text = Initials(game.Name);
		if (text.Length > 0)
		{
			((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)new TextBlock
			{
				Text = text,
				FontSize = 30.0,
				FontWeight = (FontWeight)700,
				Foreground = (IBrush)new SolidColorBrush(Color.FromArgb((byte)46, byte.MaxValue, byte.MaxValue, byte.MaxValue), 1.0),
				HorizontalAlignment = (HorizontalAlignment)2,
				VerticalAlignment = (VerticalAlignment)2
			});
		}
		return (Control)(object)val3;
	}

	private static string Initials(string name)
	{
		List<char> list = new List<char>();
		string[] array = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			char c = array[i][0];
			if (char.IsLetterOrDigit(c))
			{
				list.Add(char.ToUpperInvariant(c));
				if (list.Count == 2)
				{
					break;
				}
			}
		}
		return new string(list.ToArray());
	}

	private static Bitmap? LoadCover(string? path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return null;
		}
		if (_bitmaps.TryGetValue(path, out Bitmap value))
		{
			return value;
		}
		try
		{
			Bitmap val = new Bitmap(path);
			if (_bitmaps.Count > 200)
			{
				_bitmaps.Clear();
			}
			_bitmaps[path] = val;
			return val;
		}
		catch
		{
			return null;
		}
	}

	private void ShowDetail(StoreGame game)
	{
		StoreGame game2 = game;
		Grid val = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("Auto,*")
		};
		Border val2 = new Border
		{
			Width = 452.0,
			Height = 254.0,
			CornerRadius = new CornerRadius(6.0),
			ClipToBounds = true,
			VerticalAlignment = (VerticalAlignment)1,
			Child = Placeholder(game2)
		};
		Bitmap val3 = LoadCover(game2.CoverPath);
		if (val3 != null)
		{
			((Decorator)val2).Child = (Control)new Image
			{
				Source = (IImage)(object)val3,
				Stretch = (Stretch)3
			};
		}
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val2);
		StackPanel val4 = new StackPanel
		{
			Spacing = 9.0,
			Margin = new Thickness(22.0, 0.0, 0.0, 0.0)
		};
		TextBlock val5 = Ui.T(game2.Name, 19.0, Skin.Text, (FontWeight)700, (TextWrapping)1);
		((Layoutable)val5).MaxWidth = 360.0;
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)val5);
		TextBlock val6 = Ui.T(string.IsNullOrEmpty(game2.Developer) ? "" : Loc.F("recs.devLine", game2.Developer, game2.Publisher), 11.5, Skin.TextDim, null, (TextWrapping)0);
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)val6);
		if (game2.Genres.Length != 0)
		{
			WrapPanel val7 = new WrapPanel
			{
				Orientation = (Orientation)0
			};
			string[] genres = game2.Genres;
			foreach (string text in genres)
			{
				((AvaloniaList<Control>)(object)((Panel)val7).Children).Add((Control)new Border
				{
					Child = (Control)(object)Ui.T(text, 11.0, Skin.TextMuted, null, (TextWrapping)0),
					Background = Skin.PanelAlt,
					CornerRadius = new CornerRadius(999.0),
					Padding = new Thickness(10.0, 4.0),
					Margin = new Thickness(0.0, 0.0, 6.0, 4.0)
				});
			}
			((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)val7);
		}
		if (!string.IsNullOrEmpty(game2.ReleaseDate))
		{
			((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)Ui.T(Loc.F("recs.release", game2.ReleaseDate), 11.0, Skin.TextDim, null, (TextWrapping)0));
		}
		string text2 = game2.Description;
		if (!string.IsNullOrWhiteSpace(text2))
		{
			if (text2.Length > 260)
			{
				text2 = text2.Substring(0, 260).TrimEnd() + "…";
			}
			TextBlock val8 = Ui.T(text2, 12.5, Skin.TextMuted, null, (TextWrapping)1);
			((Layoutable)val8).MaxWidth = 360.0;
			((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)val8);
		}
		else if (game2.AppId > 0)
		{
			TextBlock val9 = Ui.T("", 12.5, Skin.TextMuted, null, (TextWrapping)1);
			((Layoutable)val9).MaxWidth = 360.0;
			((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)val9);
			FillDescriptionAsync(game2, val9, val6);
		}
		if (_inLibrary(game2.Name))
		{
			((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)Ui.T("★ " + Loc.T("recs.inLibrary"), 11.5, Skin.Text, null, (TextWrapping)0));
		}
		StackPanel val10 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 10.0,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		((AvaloniaList<Control>)(object)((Panel)val10).Children).Add((Control)(object)Ui.PrimaryButton(Loc.T("recs.findWeb"), delegate
		{
			SearchWeb(game2);
		}));
		((AvaloniaList<Control>)(object)((Panel)val10).Children).Add((Control)(object)Ui.GhostButton(Loc.T("btn.close"), delegate
		{
			_detail.IsVisible = false;
		}));
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)val10);
		Grid.SetColumn((Control)(object)val4, 1);
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val4);
		Border val11 = new Border
		{
			Background = Skin.Panel,
			CornerRadius = new CornerRadius(8.0),
			Padding = new Thickness(20.0),
			BoxShadow = Ui.GlowShadow(),
			Child = (Control)(object)val,
			HorizontalAlignment = (HorizontalAlignment)2,
			VerticalAlignment = (VerticalAlignment)2
		};
		((Decorator)_detail).Child = (Control)(object)val11;
		_detail.IsVisible = true;
		Ui.FadeSlideIn((Visual)(object)val11, 0, 180, 8.0);
	}

	private static async Task FillDescriptionAsync(StoreGame game, TextBlock slot, TextBlock devLine)
	{
		TextBlock slot2 = slot;
		TextBlock devLine2 = devLine;
		StoreGame game2 = game;
		try
		{
			await StoreFeed.EnrichAsync(new StoreGame[1] { game2 }, null, CancellationToken.None);
			string text = game2.Description ?? "";
			if (text.Length > 260)
			{
				text = text.Substring(0, 260).TrimEnd() + "…";
			}
			Dispatcher.UIThread.Post((Action)delegate
			{
				slot2.Text = text;
				string text2 = devLine2.Text;
				if ((text2 == "" || text2 == null) && !string.IsNullOrEmpty(game2.Developer))
				{
					devLine2.Text = Loc.F("recs.devLine", game2.Developer, game2.Publisher);
				}
			}, default(DispatcherPriority));
		}
		catch (Exception ex)
		{
			AppPaths.Log("RecommendationsView описание: " + ex.Message);
		}
	}

	private void SearchWeb(StoreGame game)
	{
		string stringToEscape = (Loc.IsRu ? ("\"" + game.Name + "\" скачать") : ("\"" + game.Name + "\" download"));
		string text = _engine();
		Shell.OpenUrl((text == "yandex") ? ("https://yandex.ru/search/?text=" + Uri.EscapeDataString(stringToEscape)) : ((!(text == "ddg")) ? ("https://www.google.com/search?q=" + Uri.EscapeDataString(stringToEscape)) : ("https://duckduckgo.com/?q=" + Uri.EscapeDataString(stringToEscape))));
	}
}
