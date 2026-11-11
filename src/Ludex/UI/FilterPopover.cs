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

public sealed class FilterPopover
{
	private readonly OverlayPanel _overlay;

	private readonly Border _panel;

	private FilterPopover(OverlayPanel overlay, Border panel)
	{
		_overlay = overlay;
		_panel = panel;
	}

	public static FilterPopover? Show(Control anchor, CatalogFilter current, Action<CatalogFilter> onApply, double width)
	{
		Action<CatalogFilter> onApply2 = onApply;
		CatalogFilter draft = current.Clone();
		List<(Border Frame, TextBlock Label, Func<bool> IsOn)> chips = new List<(Border, TextBlock, Func<bool>)>();
		StackPanel val = new StackPanel
		{
			Spacing = 15.0
		};
		Toggle described = null;
		Toggle unshelved = null;
		OverlayPanel overlay = null;
		WrapPanel val2 = new WrapPanel
		{
			Orientation = (Orientation)0
		};
		foreach (var item10 in CatalogFacets.Presets())
		{
			string item = item10.Title;
			CatalogFilter preset = item10.Filter;
			Border val3 = Ui.Clickable((Control)(object)Ui.T(item, 12.0, Skin.TextMuted, (FontWeight)500, (TextWrapping)0), (Action?)delegate
			{
				draft.CopyFrom(preset);
				Apply();
				Sync();
			}, (IBrush?)(object)Brushes.Transparent, Skin.PanelAlt, (CornerRadius?)new CornerRadius(999.0), (Thickness?)new Thickness(12.0, 6.0), (IBrush?)null, (Thickness?)null);
			((Layoutable)val3).Margin = new Thickness(0.0, 0.0, 6.0, 6.0);
			((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)val3);
		}
		WrapPanel val4 = new WrapPanel
		{
			Orientation = (Orientation)0
		};
		string[] genres = CatalogFacets.Genres;
		foreach (string genre in genres)
		{
			TextBlock val5 = Ui.T(CatalogFacets.Label(genre), 12.5, Skin.TextMuted, (FontWeight)500, (TextWrapping)0);
			Border val6 = Ui.Clickable((Control)(object)val5, (Action?)delegate
			{
				Toggle(draft.Genres, genre);
				Sync();
			}, (IBrush?)(object)Brushes.Transparent, Skin.CardHover, (CornerRadius?)new CornerRadius(8.0), (Thickness?)new Thickness(14.0, 9.0), (IBrush?)null, (Thickness?)null);
			((Layoutable)val6).Margin = new Thickness(0.0, 0.0, 6.0, 6.0);
			((Layoutable)val6).MinWidth = 118.0;
			((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)val6);
			chips.Add((val6, val5, () => draft.Genres.Contains(genre)));
		}
		WrapPanel val7 = new WrapPanel
		{
			Orientation = (Orientation)0
		};
		genres = CatalogFacets.Tags;
		foreach (string tag in genres)
		{
			TextBlock val8 = Ui.T(CatalogFacets.Label(tag), 11.5, Skin.TextMuted, (FontWeight)500, (TextWrapping)0);
			Border val9 = Ui.Clickable((Control)(object)val8, (Action?)delegate
			{
				Toggle(draft.Tags, tag);
				Sync();
			}, (IBrush?)(object)Brushes.Transparent, Skin.PanelAlt, (CornerRadius?)new CornerRadius(999.0), (Thickness?)new Thickness(11.0, 5.0), (IBrush?)null, (Thickness?)null);
			((Layoutable)val9).Margin = new Thickness(0.0, 0.0, 5.0, 5.0);
			((AvaloniaList<Control>)(object)((Panel)val7).Children).Add((Control)(object)val9);
			chips.Add((val9, val8, () => draft.Tags.Contains(tag)));
		}
		WrapPanel val10 = new WrapPanel
		{
			Orientation = (Orientation)0
		};
		(string, string)[] array = new(string, string)[6]
		{
			("", Loc.T("facet.any")),
			("1990", "1990-е"),
			("2000", "2000-е"),
			("2010", "2010-е"),
			("2020", "2020-е"),
			("2024", "2024+")
		};
		for (int j = 0; j < array.Length; j++)
		{
			(string, string) tuple = array[j];
			string item2 = tuple.Item1;
			string item3 = tuple.Item2;
			string v3 = item2;
			TextBlock val11 = Ui.T(item3, 11.5, Skin.TextMuted, (FontWeight)500, (TextWrapping)0);
			Border val12 = Ui.Clickable((Control)(object)val11, (Action?)delegate
			{
				draft.Decade = v3;
				Sync();
			}, (IBrush?)(object)Brushes.Transparent, Skin.PanelAlt, (CornerRadius?)new CornerRadius(999.0), (Thickness?)new Thickness(11.0, 5.0), (IBrush?)null, (Thickness?)null);
			((Layoutable)val12).Margin = new Thickness(0.0, 0.0, 5.0, 5.0);
			((AvaloniaList<Control>)(object)((Panel)val10).Children).Add((Control)(object)val12);
			chips.Add((val12, val11, () => draft.Decade == v3));
		}
		WrapPanel val13 = new WrapPanel
		{
			Orientation = (Orientation)0
		};
		array = new(string, string)[4]
		{
			("", Loc.T("facet.any")),
			("described", Loc.T("facet.size.described")),
			("short", Loc.T("facet.size.short")),
			("long", Loc.T("facet.size.long"))
		};
		for (int k = 0; k < array.Length; k++)
		{
			(string, string) tuple2 = array[k];
			string item4 = tuple2.Item1;
			string item5 = tuple2.Item2;
			string v2 = item4;
			TextBlock val14 = Ui.T(item5, 11.5, Skin.TextMuted, (FontWeight)500, (TextWrapping)0);
			Border val15 = Ui.Clickable((Control)(object)val14, (Action?)delegate
			{
				draft.Size = v2;
				Sync();
			}, (IBrush?)(object)Brushes.Transparent, Skin.PanelAlt, (CornerRadius?)new CornerRadius(999.0), (Thickness?)new Thickness(11.0, 5.0), (IBrush?)null, (Thickness?)null);
			((Layoutable)val15).Margin = new Thickness(0.0, 0.0, 5.0, 5.0);
			((AvaloniaList<Control>)(object)((Panel)val13).Children).Add((Control)(object)val15);
			chips.Add((val15, val14, () => draft.Size == v2));
		}
		WrapPanel val16 = new WrapPanel
		{
			Orientation = (Orientation)0
		};
		(string, string)[] sorts = CatalogFacets.Sorts;
		for (int l = 0; l < sorts.Length; l++)
		{
			(string, string) tuple3 = sorts[l];
			string item6 = tuple3.Item1;
			string item7 = tuple3.Item2;
			string v = item6;
			TextBlock val17 = Ui.T(Loc.T(item7), 12.0, Skin.TextMuted, (FontWeight)500, (TextWrapping)0);
			Border val18 = Ui.Clickable((Control)(object)val17, (Action?)delegate
			{
				draft.Sort = v;
				Sync();
			}, (IBrush?)(object)Brushes.Transparent, Skin.PanelAlt, (CornerRadius?)new CornerRadius(999.0), (Thickness?)new Thickness(12.0, 6.0), (IBrush?)null, (Thickness?)null);
			((Layoutable)val18).Margin = new Thickness(0.0, 0.0, 6.0, 6.0);
			((AvaloniaList<Control>)(object)((Panel)val16).Children).Add((Control)(object)val18);
			chips.Add((val18, val17, () => draft.Sort == v));
		}
		StackPanel val19 = new StackPanel
		{
			Spacing = 2.0
		};
		described = new Toggle(Loc.T("facet.onlyDescribed"), draft.OnlyDescribed, delegate(bool on)
		{
			draft.OnlyDescribed = on;
		}, interactive: true, 12.0);
		unshelved = new Toggle(Loc.T("facet.onlyUnshelved"), draft.OnlyUnshelved, delegate(bool on)
		{
			draft.OnlyUnshelved = on;
		}, interactive: true, 12.0);
		((AvaloniaList<Control>)(object)((Panel)val19).Children).Add((Control)(object)described);
		((AvaloniaList<Control>)(object)((Panel)val19).Children).Add((Control)(object)unshelved);
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add(Section(Loc.T("facet.presets"), (Control)(object)val2));
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add(Section(Loc.T("facet.genres"), (Control)(object)val4));
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add(Section(Loc.T("facet.tags"), (Control)(object)val7));
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add(Section(Loc.T("facet.decade"), (Control)(object)val10));
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add(Section(Loc.T("facet.size"), (Control)(object)val13));
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add(Section(Loc.T("facet.sortTitle"), (Control)(object)val16));
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val19);
		Border val20 = Ui.GhostButton(Loc.T("facet.reset"), delegate
		{
			draft.CopyFrom(new CatalogFilter());
			Apply();
			Sync();
		}, Skin.TextMuted);
		((Decorator)val20).Padding = new Thickness(14.0, 8.0);
		((Layoutable)val20).Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		Border val21 = Ui.Clickable((Control)(object)Ui.T(Loc.T("facet.show"), 12.5, Skin.OnAccent, (FontWeight)600, (TextWrapping)0), (Action?)delegate
		{
			overlay.Close();
			onApply2(draft);
		}, Skin.Accent, Skin.AccentHover, (CornerRadius?)new CornerRadius(999.0), (Thickness?)new Thickness(20.0, 9.0), (IBrush?)null, (Thickness?)null);
		Controls children = ((Panel)val).Children;
		StackPanel val22 = new StackPanel
		{
			Orientation = (Orientation)0,
			HorizontalAlignment = (HorizontalAlignment)3,
			Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
		};
		((AvaloniaList<Control>)(object)((Panel)val22).Children).Add((Control)(object)val20);
		((AvaloniaList<Control>)(object)((Panel)val22).Children).Add((Control)(object)val21);
		((AvaloniaList<Control>)(object)children).Add((Control)val22);
		Border val23 = new Border
		{
			Background = (IBrush)new SolidColorBrush(Skin.PanelColor, 1.0),
			BorderBrush = Skin.GlassEdge,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(14.0),
			Padding = new Thickness(18.0, 16.0),
			Width = width,
			Child = (Control)(object)val,
			Opacity = 0.0,
			RenderTransform = (ITransform)new TranslateTransform(0.0, -10.0)
		};
		BoxShadow val24 = new BoxShadow
		{
			OffsetX = 0.0,
			OffsetY = 14.0,
			Blur = 34.0,
			Color = Color.Parse("#88000000"),
		};
		val23.BoxShadow = new BoxShadows(val24);
		Transitions val25 = new Transitions();
		((AvaloniaList<ITransition>)val25).Add((ITransition)new DoubleTransition
		{
			Property = (AvaloniaProperty)(object)Visual.OpacityProperty,
			Duration = TimeSpan.FromMilliseconds(150.0),
			Easing = (Easing)new CubicEaseOut()
		});
		((AvaloniaList<ITransition>)val25).Add((ITransition)new TransformOperationsTransition
		{
			Property = (AvaloniaProperty)(object)Visual.RenderTransformProperty,
			Duration = TimeSpan.FromMilliseconds(180.0),
			Easing = (Easing)new CubicEaseOut()
		});
		((Animatable)val23).Transitions = val25;
		Border body = val23;
		Border panel = body;
		Rect bounds = anchor.Bounds;
		overlay = OverlayPanel.Show(anchor, (Control)(object)panel, new Point(0.0, ((Rect)bounds).Height + 8.0));
		if (overlay == null)
		{
			return null;
		}
		Dispatcher.UIThread.Post((Action)delegate
		{
			body.Opacity = 1.0;
			body.RenderTransform = (ITransform)new TranslateTransform(0.0, 0.0);
		}, DispatcherPriority.Input);
		Sync();
		return new FilterPopover(overlay, body);
		void Apply()
		{
			described?.SetSilently(draft.OnlyDescribed);
			unshelved?.SetSilently(draft.OnlyUnshelved);
		}
		void Sync()
		{
			foreach (var item11 in chips)
			{
				Border item8 = item11.Frame;
				TextBlock item9 = item11.Label;
				bool flag = item11.IsOn();
				IBrush baseBg = (IBrush)(flag ? ((object)Skin.CardHover) : ((object)Brushes.Transparent));
				Ui.SetColors(item8, baseBg, flag ? Skin.CardHover : Skin.PanelAlt);
				item9.Foreground = (flag ? Skin.Text : Skin.TextMuted);
				item9.FontWeight = (FontWeight)(flag ? 600 : 500);
			}
		}
		static void Toggle(HashSet<string> set, string value)
		{
			if (!set.Remove(value))
			{
				set.Add(value);
			}
		}
	}

	private static Control Section(string title, Control content)
	{
		StackPanel val = new StackPanel
		{
			Spacing = 8.0
		};
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)Ui.T(title, 11.0, Skin.TextDim, (FontWeight)600, (TextWrapping)0));
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add(content);
		return (Control)val;
	}
}
