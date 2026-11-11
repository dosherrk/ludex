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

public sealed class HiddenPanel : Border
{
	private readonly List<string> _keys;

	private readonly Func<string, string> _titleOf;

	private readonly StackPanel _rows = new StackPanel
	{
		Spacing = 6.0
	};

	private readonly TextBlock _summary;

	public event Action<string>? Restored;

	public event Action? RestoredAll;

	public event Action? Closed;

	public HiddenPanel(IEnumerable<string> keys, Func<string, string> titleOf)
	{
		_keys = keys.ToList();
		_titleOf = titleOf;
		((Layoutable)this).MaxWidth = 720.0;
		((Layoutable)this).MaxHeight = 660.0;
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
		DockPanel val2 = new DockPanel();
		DockPanel val3 = new DockPanel
		{
			Margin = new Thickness(26.0, 22.0, 22.0, 0.0)
		};
		Border val4 = Ui.Clickable((Control)(object)Ui.Icon("M1,1 L11,11 M11,1 L1,11", Skin.TextMuted, 12.0, 12.0, stroke: true), (Action?)delegate
		{
			this.Closed?.Invoke();
		}, Skin.PanelAlt, Skin.CardHover, (CornerRadius?)new CornerRadius(6.0), (Thickness?)new Thickness(10.0, 10.0), (IBrush?)null, (Thickness?)null);
		((Layoutable)val4).VerticalAlignment = (VerticalAlignment)1;
		DockPanel.SetDock((Control)(object)val4, (Dock)2);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)val4);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.T(Loc.T("hidden.title"), 21.0, Skin.Text, (FontWeight)700, (TextWrapping)0));
		DockPanel.SetDock((Control)(object)val3, (Dock)3);
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)val3);
		StackPanel val5 = new StackPanel
		{
			Orientation = (Orientation)0,
			HorizontalAlignment = (HorizontalAlignment)3,
			Spacing = 10.0,
			Margin = new Thickness(26.0, 12.0, 22.0, 22.0)
		};
		Border val6 = Ui.GhostButton(Loc.T("hidden.restoreAll"), delegate
		{
			this.RestoredAll?.Invoke();
			_keys.Clear();
			RenderRows();
		});
		Border val7 = Ui.PrimaryButton(Loc.T("btn.close"), delegate
		{
			this.Closed?.Invoke();
		});
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)val6);
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)val7);
		DockPanel.SetDock((Control)(object)val5, (Dock)1);
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)val5);
		StackPanel val8 = new StackPanel
		{
			Margin = new Thickness(26.0, 14.0, 26.0, 0.0),
			Spacing = 12.0
		};
		_summary = Ui.T(CountText(), 11.5, Skin.TextDim, null, (TextWrapping)1);
		((AvaloniaList<Control>)(object)((Panel)val8).Children).Add((Control)(object)Ui.T(Loc.T("hidden.hint"), 11.5, Skin.TextDim, null, (TextWrapping)1));
		((AvaloniaList<Control>)(object)((Panel)val8).Children).Add((Control)(object)_summary);
		ScrollViewer val9 = new ScrollViewer
		{
			Content = _rows,
			VerticalScrollBarVisibility = (ScrollBarVisibility)1,
			MaxHeight = 420.0,
			Background = (IBrush)(object)Brushes.Transparent
		};
		Ui.SmoothScroll(val9, 70.0);
		((AvaloniaList<Control>)(object)((Panel)val8).Children).Add((Control)(object)val9);
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)val8);
		((Decorator)this).Child = (Control)(object)val2;
		RenderRows();
	}

	private string CountText()
	{
		return Loc.F("hidden.count", _keys.Count);
	}

	private void RenderRows()
	{
		((AvaloniaList<Control>)(object)((Panel)_rows).Children).Clear();
		if (_keys.Count == 0)
		{
			((AvaloniaList<Control>)(object)((Panel)_rows).Children).Add((Control)new Border
			{
				Background = Skin.PanelAlt,
				CornerRadius = new CornerRadius(8.0),
				Padding = new Thickness(14.0, 16.0),
				Child = (Control)(object)Ui.T(Loc.T("hidden.empty"), 12.0, Skin.TextDim, null, (TextWrapping)1)
			});
			_summary.Text = CountText();
			return;
		}
		foreach (string item in _keys.OrderBy<string, string>((string k) => _titleOf(k), StringComparer.CurrentCultureIgnoreCase))
		{
			((AvaloniaList<Control>)(object)((Panel)_rows).Children).Add(Row(item));
		}
		_summary.Text = CountText();
	}

	private Control Row(string key)
	{
		string key2 = key;
		TextBlock val = Ui.T(_titleOf(key2), 12.5, Skin.Text, (FontWeight)500, (TextWrapping)0);
		val.TextTrimming = TextTrimming.CharacterEllipsis;
		TextBlock val2 = Ui.T(key2, 10.5, Skin.TextDim, null, (TextWrapping)0);
		val2.TextTrimming = TextTrimming.CharacterEllipsis;
		StackPanel val3 = new StackPanel
		{
			Spacing = 2.0,
			VerticalAlignment = (VerticalAlignment)2
		};
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)val);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)val2);
		Border val4 = Ui.GhostButton(Loc.T("hidden.restore"), delegate
		{
			if (_keys.Remove(key2))
			{
				RenderRows();
				this.Restored?.Invoke(key2);
			}
		});
		((Layoutable)val4).VerticalAlignment = (VerticalAlignment)2;
		Grid val5 = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("*,Auto")
		};
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)val3);
		Grid.SetColumn((Control)(object)val4, 1);
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)val4);
		return (Control)new Border
		{
			Background = Skin.PanelAlt,
			CornerRadius = new CornerRadius(8.0),
			Padding = new Thickness(14.0, 9.0, 10.0, 9.0),
			Child = (Control)(object)val5
		};
	}
}
