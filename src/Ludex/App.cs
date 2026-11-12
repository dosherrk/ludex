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

public class App : Application
{
	public override void Initialize()
	{
		((Application)this).RequestedThemeVariant = ThemeVariant.Dark;
		((Application)this).Styles.Add((IStyle)new FluentTheme((IServiceProvider)null));
		Style val = new Style((Func<Selector, Selector>)((Selector? x) => Selectors.OfType<ScrollBar>(x)))
		{
			Setters =
			{
				(SetterBase)new Setter((AvaloniaProperty)(object)Layoutable.WidthProperty, (object)8.0),
				(SetterBase)new Setter((AvaloniaProperty)(object)Layoutable.HeightProperty, (object)8.0),
				(SetterBase)new Setter((AvaloniaProperty)(object)Visual.OpacityProperty, (object)0.45),
				(SetterBase)new Setter((AvaloniaProperty)(object)TemplatedControl.BackgroundProperty, (object)Brushes.Transparent)
			}
		};
		IList<SetterBase> setters = ((StyleBase)val).Setters;
		StyledProperty<Transitions> transitionsProperty = Animatable.TransitionsProperty;
		Transitions val2 = new Transitions();
		((AvaloniaList<ITransition>)val2).Add((ITransition)new DoubleTransition
		{
			Property = (AvaloniaProperty)(object)Visual.OpacityProperty,
			Duration = TimeSpan.FromMilliseconds(190.0)
		});
		setters.Add((SetterBase)new Setter((AvaloniaProperty)(object)transitionsProperty, (object)val2));
		Style val3 = val;
		Style val4 = new Style((Func<Selector, Selector>)((Selector? x) => Selectors.Class(Selectors.OfType<ScrollBar>(x), ":pointerover")))
		{
			Setters = { (SetterBase)new Setter((AvaloniaProperty)(object)Visual.OpacityProperty, (object)1.0) }
		};
		((Application)this).Styles.Add((IStyle)(object)val3);
		((Application)this).Styles.Add((IStyle)(object)val4);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		IApplicationLifetime applicationLifetime = ((Application)this).ApplicationLifetime;
		IClassicDesktopStyleApplicationLifetime desktop = (IClassicDesktopStyleApplicationLifetime)(object)((applicationLifetime is IClassicDesktopStyleApplicationLifetime) ? applicationLifetime : null);
		if (desktop != null)
		{
			// Приложение живёт в трее: закрытие окна — это не выход. При стандартном
			// OnLastWindowClose закрытие заставки до показа главного окна завершало
			// процесс целиком — со стороны это выглядело как «запустился и сразу умер».
			desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
			SplashWindow splash = new SplashWindow();
			((WindowBase)splash).Show();
			MainWindow main = new MainWindow();
			main.EnsureTray();
			SingleInstance.Listen(main.OpenFromTray);
			Dispatcher.UIThread.Post((Action)async delegate
			{
				try
				{
					await main.WarmUpAsync();
				}
				catch (Exception ex)
				{
					AppPaths.Log("WarmUp: " + ex);
				}
				desktop.MainWindow = (Window)(object)main;
				((WindowBase)main).Show();
				((WindowBase)main).Activate();
				splash.OpenInto();
				main.RevealAsync();
				splash.Finish();
			}, default(DispatcherPriority));
		}
		((Application)this).OnFrameworkInitializationCompleted();
	}
}
