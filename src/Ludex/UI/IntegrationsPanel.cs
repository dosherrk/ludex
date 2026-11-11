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

public sealed class IntegrationsPanel : Border
{
	private readonly AppSettings _settings;

	private readonly List<string> _enabled;

	private readonly Dictionary<string, TextBlock> _statusTexts = new Dictionary<string, TextBlock>();

	private readonly Dictionary<string, Toggle> _toggles = new Dictionary<string, Toggle>();

	private List<LauncherState> _states = new List<LauncherState>();

	private StackPanel _rows;

	private TextBlock _summary;

	private Border _rescanButton;

	private bool _scanning;

	public event Action? Closed;

	public event Action? Changed;

	public IntegrationsPanel(AppSettings settings)
	{
		_settings = settings;
		_enabled = new List<string>(settings.Launchers);
		((Layoutable)this).MaxWidth = 720.0;
		((Layoutable)this).MaxHeight = 700.0;
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
		LoadAsync();
	}

	private void BuildContent()
	{
		DockPanel val = new DockPanel
		{
			Margin = new Thickness(24.0, 20.0, 20.0, 0.0)
		};
		Border val2 = Ui.Clickable((Control)(object)Ui.Icon("M1,1 L11,11 M11,1 L1,11", Skin.TextMuted, 12.0, 12.0, stroke: true), (Action?)delegate
		{
			this.Closed?.Invoke();
		}, Skin.PanelAlt, Skin.CardHover, (CornerRadius?)new CornerRadius(6.0), (Thickness?)new Thickness(10.0, 10.0), (IBrush?)null, (Thickness?)null);
		((Layoutable)val2).VerticalAlignment = (VerticalAlignment)1;
		DockPanel.SetDock((Control)(object)val2, (Dock)2);
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val2);
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)Ui.T(Loc.T("integrations.title"), 21.0, Skin.Text, (FontWeight)700, (TextWrapping)0));
		StackPanel val3 = new StackPanel
		{
			Margin = new Thickness(24.0, 14.0, 24.0, 16.0),
			Spacing = 12.0
		};
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.T(Loc.T("integrations.hint"), 11.5, Skin.TextDim, null, (TextWrapping)1));
		_summary = Ui.T("", 11.5, Skin.Accent, (FontWeight)500, (TextWrapping)0);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)_summary);
		_rows = new StackPanel
		{
			Spacing = 8.0
		};
		Launchers.Descriptor[] all = Launchers.All;
		foreach (Launchers.Descriptor descriptor in all)
		{
			if (Launchers.IsSupported(descriptor.Key))
			{
				((AvaloniaList<Control>)(object)((Panel)_rows).Children).Add(BuildRow(descriptor.Key));
			}
		}
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)_rows);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.Divider());
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.T(Loc.T("integrations.how"), 12.5, Skin.Text, (FontWeight)600, (TextWrapping)0));
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.T(Loc.T("integrations.howHint"), 11.0, Skin.TextDim, null, (TextWrapping)1));
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)Ui.T(Loc.T("integrations.applyHint"), 11.0, Skin.TextDim, null, (TextWrapping)1));
		StackPanel val4 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 10.0,
			Margin = new Thickness(24.0, 14.0, 24.0, 20.0),
			HorizontalAlignment = (HorizontalAlignment)3
		};
		_rescanButton = Ui.GhostButton(Loc.T("btn.rescanAll"), delegate
		{
			LoadAsync(force: true);
		});
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)_rescanButton);
		((AvaloniaList<Control>)(object)((Panel)val4).Children).Add((Control)(object)Ui.PrimaryButton(Loc.T("btn.close"), delegate
		{
			this.Closed?.Invoke();
		}));
		DockPanel val5 = new DockPanel
		{
			LastChildFill = true
		};
		DockPanel.SetDock((Control)(object)val, (Dock)3);
		DockPanel.SetDock((Control)(object)val4, (Dock)1);
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)val);
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)val4);
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)new ScrollViewer
		{
			Content = val3,
			VerticalScrollBarVisibility = (ScrollBarVisibility)1,
			Background = (IBrush)(object)Brushes.Transparent
		});
		((Decorator)this).Child = (Control)(object)val5;
		RefreshSummary();
	}

	private Control BuildRow(string key)
	{
		string key2 = key;
		TextBlock val = Ui.T(Loc.T("launcher." + key2), 13.0, Skin.Text, (FontWeight)500, (TextWrapping)0);
		TextBlock val2 = Ui.T(Loc.T("launcher." + key2 + ".hint"), 10.5, Skin.TextDim, null, (TextWrapping)1);
		StackPanel val3 = new StackPanel
		{
			Spacing = 2.0,
			VerticalAlignment = (VerticalAlignment)2
		};
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)val);
		((AvaloniaList<Control>)(object)((Panel)val3).Children).Add((Control)(object)val2);
		Control child = LauncherLogo.Create(key2);
		TextBlock val4 = Ui.T(Loc.T("integrations.checking"), 10.5, Skin.TextDim, null, (TextWrapping)0);
		((Layoutable)val4).VerticalAlignment = (VerticalAlignment)2;
		val4.TextAlignment = (TextAlignment)2;
		_statusTexts[key2] = val4;
		Toggle toggle = new Toggle("", _enabled.Contains<string>(key2, StringComparer.OrdinalIgnoreCase), delegate(bool on)
		{
			if (on)
			{
				if (!_enabled.Contains<string>(key2, StringComparer.OrdinalIgnoreCase))
				{
					_enabled.Add(key2);
				}
			}
			else
			{
				_enabled.RemoveAll((string x) => string.Equals(x, key2, StringComparison.OrdinalIgnoreCase));
			}
			_settings.Launchers = new List<string>(_enabled);
			RefreshSummary();
			this.Changed?.Invoke();
		});
		((Layoutable)toggle).VerticalAlignment = (VerticalAlignment)2;
		_toggles[key2] = toggle;
		StackPanel val5 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 12.0,
			VerticalAlignment = (VerticalAlignment)2,
			HorizontalAlignment = (HorizontalAlignment)3
		};
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)val4);
		((AvaloniaList<Control>)(object)((Panel)val5).Children).Add((Control)(object)toggle);
		Grid val6 = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
		};
		Decorator val7 = new Decorator
		{
			Child = child,
			Margin = new Thickness(0.0, 0.0, 12.0, 0.0)
		};
		((AvaloniaList<Control>)(object)((Panel)val6).Children).Add((Control)(object)val7);
		Grid.SetColumn((Control)(object)val3, 1);
		((AvaloniaList<Control>)(object)((Panel)val6).Children).Add((Control)(object)val3);
		Grid.SetColumn((Control)(object)val5, 2);
		((AvaloniaList<Control>)(object)((Panel)val6).Children).Add((Control)(object)val5);
		return (Control)new Border
		{
			Background = Skin.PanelAlt,
			CornerRadius = new CornerRadius(8.0),
			Padding = new Thickness(12.0, 10.0),
			Child = (Control)(object)val6
		};
	}

	private async Task LoadAsync(bool force = false)
	{
		if (_scanning)
		{
			return;
		}
		_scanning = true;
		bool flag = !force && Launchers.HasSessionCache;
		if (!flag)
		{
			_rescanButton.Opacity = 0.55;
			((InputElement)_rescanButton).IsEnabled = false;
			foreach (string key in _statusTexts.Keys)
			{
				_statusTexts[key].Text = Loc.T("integrations.checking");
			}
		}
		try
		{
			List<LauncherState> list = (flag ? Launchers.ScanAll(_enabled) : (await Task.Run(() => Launchers.RescanAll(_enabled))));
			List<LauncherState> list2 = list;
			List<LauncherState> states = list2;
			_states = states;
			await Dispatcher.UIThread.InvokeAsync((Action)delegate
			{
				foreach (LauncherState item in states)
				{
					if (_statusTexts.TryGetValue(item.Key, out TextBlock value))
					{
						value.Text = ((item.Error != null) ? Loc.T("integrations.error") : ((!item.Installed) ? Loc.T("integrations.notInstalled") : ((item.Count == 0) ? Loc.T("integrations.empty") : Loc.F("integrations.found", item.Count))));
						value.Foreground = ((item.Installed && item.Count > 0) ? Skin.TextMuted : Skin.TextDim);
						if (_toggles.TryGetValue(item.Key, out Toggle value2))
						{
							value2.Opacity = (item.Installed ? 1.0 : 0.5);
						}
					}
				}
				RefreshSummary();
			});
		}
		catch (Exception ex)
		{
			AppPaths.Log("Integrations scan: " + ex.Message);
		}
		finally
		{
			_scanning = false;
			_rescanButton.Opacity = 1.0;
			((InputElement)_rescanButton).IsEnabled = true;
		}
	}

	private void RefreshSummary()
	{
		int num = _states.Count((LauncherState s) => _enabled.Contains<string>(s.Key, StringComparer.OrdinalIgnoreCase));
		int num2 = _states.Where((LauncherState s) => _enabled.Contains<string>(s.Key, StringComparer.OrdinalIgnoreCase)).Sum((LauncherState s) => s.Count);
		_summary.Text = Loc.F("integrations.enabled", num, num2);
	}
}
