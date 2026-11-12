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

public static class Ui
{
	public const string PlayGeometry = "M0,0 L15,8.5 L0,17 Z";

	public const string CloseGeometry = "M1,1 L11,11 M11,1 L1,11";

	public const string MenuGeometry = "M0,2.5 H20 M0,10 H20 M0,17.5 H20";

	public const string GearGeometry = "M19.14,12.94c0.04-0.3,0.06-0.61,0.06-0.94c0-0.32-0.02-0.64-0.07-0.94l2.03-1.58c0.18-0.14,0.23-0.41,0.12-0.61l-1.92-3.32c-0.12-0.22-0.37-0.29-0.59-0.22l-2.39,0.96c-0.5-0.38-1.03-0.7-1.62-0.94L14.4,2.81c-0.04-0.24-0.24-0.41-0.48-0.41h-3.84c-0.24,0-0.43,0.17-0.47,0.41L9.25,5.35C8.66,5.59,8.12,5.92,7.63,6.29L5.24,5.33c-0.22-0.08-0.47,0-0.59,0.22L2.74,8.87C2.62,9.08,2.66,9.34,2.86,9.48l2.03,1.58C4.84,11.36,4.8,11.69,4.8,12s0.02,0.64,0.07,0.94l-2.03,1.58c-0.18,0.14-0.23,0.41-0.12,0.61l1.92,3.32c0.12,0.22,0.37,0.29,0.59,0.22l2.39-0.96c0.5,0.38,1.03,0.7,1.62,0.94l0.36,2.54c0.05,0.24,0.24,0.41,0.48,0.41h3.84c0.24,0,0.44-0.17,0.47-0.41l0.36-2.54c0.59-0.24,1.13-0.56,1.62-0.94l2.39,0.96c0.22,0.08,0.47,0,0.59-0.22l1.92-3.32c0.12-0.22,0.07-0.47-0.12-0.61L19.14,12.94z M12,15.6c-1.98,0-3.6-1.62-3.6-3.6s1.62-3.6,3.6-3.6s3.6,1.62,3.6,3.6S13.98,15.6,12,15.6z";

	public const string MinimizeGeometry = "M0,0 H13 V2.4 H0 Z";

	public const string MaximizeGeometry = "M0.6,0.6 H11.4 V11.4 H0.6 Z";

	public const string RestoreGeometry = "M2.6,0.6 H11.4 V9.4 M0.6,2.6 H9.4 V11.4 H0.6 Z";

	public const string PaletteGeometry = "M12,22c5.52,0,10-4.48,10-10S17.52,2,12,2S2,6.48,2,12c0,3.31,2.69,6,6,6h1.5c0.83,0,1.5,0.67,1.5,1.5c0,0.28-0.08,0.54-0.21,0.76C10.4,20.88,11.13,22,12,22z M6.5,11C5.67,11,5,10.33,5,9.5S5.67,8,6.5,8S8,8.67,8,9.5S7.33,11,6.5,11z M9.5,7C8.67,7,8,6.33,8,5.5S8.67,4,9.5,4S11,4.67,11,5.5S10.33,7,9.5,7z M14.5,7C13.67,7,13,6.33,13,5.5S13.67,4,14.5,4S16,4.67,16,5.5S15.33,7,14.5,7z M17.5,11c-0.83,0-1.5-0.67-1.5-1.5S16.67,8,17.5,8S19,8.67,19,9.5S18.33,11,17.5,11z";

	public const string LinkGeometry = "M7.5,12.5 L12.5,7.5 M8.6,4.6 L10.4,2.8 A3.9,3.9 0 0 1 15.9,8.3 L14.1,10.1 M5.9,9.9 L4.1,11.7 A3.9,3.9 0 0 0 9.6,17.2 L11.4,15.4";

	public const string GridGeometry = "M0,0 H8.4 V8.4 H0 Z M11.6,0 H20 V8.4 H11.6 Z M0,11.6 H8.4 V20 H0 Z M11.6,11.6 H20 V20 H11.6 Z";

	public const string SparkleGeometry = "M10,0 L12.1,7.9 L20,10 L12.1,12.1 L10,20 L7.9,12.1 L0,10 L7.9,7.9 Z";

	public const string StarGeometry = "M10,0 L12.6,6.6 L19.6,7.2 L14.3,11.9 L15.9,18.9 L10,15.3 L4.1,18.9 L5.7,11.9 L0.4,7.2 L7.4,6.6 Z";

	public static TextBlock T(string text, double size = 13.0, IBrush? color = null, FontWeight? weight = null, TextWrapping wrap = 0)
	{
		return new TextBlock
		{
			Text = text,
			FontSize = size,
			Foreground = (color ?? Skin.Text),
			FontWeight = weight.GetValueOrDefault((FontWeight)400),
			TextWrapping = wrap,
			TextTrimming = (((int)wrap == 0) ? TextTrimming.CharacterEllipsis : TextTrimming.None),
			VerticalAlignment = (VerticalAlignment)2
		};
	}

	public static Border Clickable(Control content, Action? onClick, IBrush? bg = null, IBrush? hover = null, CornerRadius? radius = null, Thickness? padding = null, IBrush? borderBrush = null, Thickness? borderThickness = null)
	{
		Action onClick2 = onClick;
		Border frame = new Border
		{
			Child = content,
			Background = (IBrush)(((object)bg) ?? ((object)Brushes.Transparent)),
			CornerRadius = (CornerRadius)((radius) ?? new CornerRadius(6.0)),
			Padding = (Thickness)((padding) ?? new Thickness(14.0, 8.0)),
			BorderBrush = borderBrush,
			BorderThickness = (Thickness)((borderThickness) ?? new Thickness(0.0)),
			Cursor = (Cursor)((onClick2 == null) ? ((object)Cursor.Default) : ((object)new Cursor((StandardCursorType)9))),
			HorizontalAlignment = (HorizontalAlignment)1,
			VerticalAlignment = (VerticalAlignment)2
		};
		if (onClick2 != null)
		{
			((Control)frame).Tag = "clickable";
			IBrush val = hover ?? Skin.PanelAlt;
			IBrush val2 = (IBrush)(((object)bg) ?? ((object)Brushes.Transparent));
			ISolidColorBrush val3 = (ISolidColorBrush)(object)((val is ISolidColorBrush) ? val : null);
			if (val3 != null)
			{
				ISolidColorBrush val4 = (ISolidColorBrush)(object)((val2 is ISolidColorBrush) ? val2 : null);
				if (val4 != null)
				{
					Color color = val4.Color;
					if (((Color)color).A == 0)
					{
						color = val3.Color;
						byte r = ((Color)color).R;
						color = val3.Color;
						byte g = ((Color)color).G;
						color = val3.Color;
						val2 = (IBrush)new SolidColorBrush(Color.FromArgb((byte)0, r, g, ((Color)color).B), 1.0);
					}
				}
			}
			((IDictionary<object, object>)((StyledElement)frame).Resources)[(object)"gvBaseBg"] = val2;
			((IDictionary<object, object>)((StyledElement)frame).Resources)[(object)"gvHoverBg"] = val;
			frame.Background = val2;
			frame.RenderTransformOrigin = new RelativePoint(0.5, 0.5, (RelativeUnit)0);
			Border obj = frame;
			Transitions val5 = new Transitions();
			((AvaloniaList<ITransition>)val5).Add((ITransition)new BrushTransition
			{
				Property = (AvaloniaProperty)(object)Border.BackgroundProperty,
				Duration = TimeSpan.FromMilliseconds(130.0)
			});
			((AvaloniaList<ITransition>)val5).Add((ITransition)new TransformOperationsTransition
			{
				Property = (AvaloniaProperty)(object)Visual.RenderTransformProperty,
				Duration = TimeSpan.FromMilliseconds(110.0),
				Easing = (Easing)new CubicEaseOut()
			});
			((Animatable)obj).Transitions = val5;
			((InputElement)frame).PointerEntered += delegate
			{
				frame.Background = HoverOf(frame);
			};
			((InputElement)frame).PointerExited += delegate
			{
				frame.Background = BaseOf(frame);
			};
			((InputElement)frame).PointerReleased += delegate
			{
				frame.RenderTransform = (ITransform)new ScaleTransform(1.0, 1.0);
			};
			((InputElement)frame).PointerCaptureLost += delegate
			{
				frame.RenderTransform = (ITransform)new ScaleTransform(1.0, 1.0);
			};
			((InputElement)frame).PointerPressed += delegate(object? _, PointerPressedEventArgs e)
			{
				PointerPoint currentPoint = ((PointerEventArgs)e).GetCurrentPoint((Visual)(object)frame);
				PointerPointProperties properties = ((PointerPoint)currentPoint).Properties;
				if (((PointerPointProperties)properties).IsLeftButtonPressed)
				{
					((RoutedEventArgs)e).Handled = true;
					frame.RenderTransform = (ITransform)new ScaleTransform(0.955, 0.955);
					onClick2();
				}
			};
		}
		return frame;
	}

	private static IBrush BaseOf(Border frame)
	{
		if (((IDictionary<object, object>)((StyledElement)frame).Resources).TryGetValue((object)"gvBaseBg", out object value))
		{
			IBrush val = (IBrush)((value is IBrush) ? value : null);
			if (val != null)
			{
				return val;
			}
		}
		return (IBrush)(object)Brushes.Transparent;
	}

	private static IBrush HoverOf(Border frame)
	{
		if (((IDictionary<object, object>)((StyledElement)frame).Resources).TryGetValue((object)"gvHoverBg", out object value))
		{
			IBrush val = (IBrush)((value is IBrush) ? value : null);
			if (val != null)
			{
				return val;
			}
		}
		return Skin.PanelAlt;
	}

	public static void SetColors(Border frame, IBrush baseBg, IBrush hoverBg)
	{
		ISolidColorBrush val = (ISolidColorBrush)(object)((hoverBg is ISolidColorBrush) ? hoverBg : null);
		if (val != null)
		{
			ISolidColorBrush val2 = (ISolidColorBrush)(object)((baseBg is ISolidColorBrush) ? baseBg : null);
			if (val2 != null)
			{
				Color color = val2.Color;
				if (((Color)color).A == 0)
				{
					color = val.Color;
					byte r = ((Color)color).R;
					color = val.Color;
					byte g = ((Color)color).G;
					color = val.Color;
					baseBg = (IBrush)new SolidColorBrush(Color.FromArgb((byte)0, r, g, ((Color)color).B), 1.0);
				}
			}
		}
		((IDictionary<object, object>)((StyledElement)frame).Resources)[(object)"gvBaseBg"] = baseBg;
		((IDictionary<object, object>)((StyledElement)frame).Resources)[(object)"gvHoverBg"] = hoverBg;
		frame.Background = baseBg;
	}

	public static void AnimateTextBox(TextBox box)
	{
		TextBox box2 = box;
		TextBox obj = box2;
		Transitions val = new Transitions();
		((AvaloniaList<ITransition>)val).Add((ITransition)new BrushTransition
		{
			Property = (AvaloniaProperty)(object)Border.BorderBrushProperty,
			Duration = TimeSpan.FromMilliseconds(160.0)
		});
		((AvaloniaList<ITransition>)val).Add((ITransition)new BrushTransition
		{
			Property = (AvaloniaProperty)(object)Border.BackgroundProperty,
			Duration = TimeSpan.FromMilliseconds(160.0)
		});
		((Animatable)obj).Transitions = val;
		((InputElement)box2).GotFocus += delegate
		{
			((TemplatedControl)box2).BorderBrush = Skin.Accent;
		};
		((InputElement)box2).LostFocus += delegate
		{
			((TemplatedControl)box2).BorderBrush = Skin.Border;
		};
	}

	public static Border PrimaryButton(string text, Action onClick, double extraWidth = 0.0)
	{
		TextBlock val = T(text, 13.0, Skin.OnAccent, (FontWeight)600, (TextWrapping)0);
		((Layoutable)val).HorizontalAlignment = (HorizontalAlignment)2;
		Border b = Clickable((Control)(object)val, onClick, Skin.Accent, Skin.AccentHover, (CornerRadius?)new CornerRadius(4.0), (Thickness?)new Thickness(20.0, 10.0), (IBrush?)null, (Thickness?)null);
		b.BoxShadow = GlowShadow();
		Border obj = b;
		Transitions val2 = new Transitions();
		((AvaloniaList<ITransition>)val2).Add((ITransition)new BoxShadowsTransition
		{
			Property = (AvaloniaProperty)(object)Border.BoxShadowProperty,
			Duration = TimeSpan.FromMilliseconds(200.0)
		});
		((Animatable)obj).Transitions = val2;
		((InputElement)b).PointerEntered += delegate
		{
			b.BoxShadow = GlowShadow(strong: true);
		};
		((InputElement)b).PointerExited += delegate
		{
			b.BoxShadow = GlowShadow();
		};
		if (extraWidth > 0.0)
		{
			((Layoutable)b).MinWidth = extraWidth;
		}
		return b;
	}

	public static BoxShadows GlowShadow(bool strong = false)
	{
		byte b = (byte)(strong ? 76u : 42u);
		BoxShadow val = new BoxShadow
		{
			OffsetX = 0.0,
			OffsetY = 0.0,
			Blur = (strong ? 26 : 18),
			Spread = (strong ? 2 : 0),
			Color = Color.FromArgb(b, byte.MaxValue, byte.MaxValue, byte.MaxValue),
		};
		return new BoxShadows(val);
	}

	public static Border GhostButton(string text, Action onClick, IBrush? fg = null)
	{
		return Clickable((Control)(object)T(text, 13.0, fg ?? Skin.Text, (FontWeight)500, (TextWrapping)0), onClick, (IBrush?)(object)Brushes.Transparent, Skin.PanelAlt, (CornerRadius?)new CornerRadius(4.0), (Thickness?)new Thickness(18.0, 9.0), Skin.Border, (Thickness?)new Thickness(1.0));
	}

	public static TextBox SearchBox(string text, string watermark, Action<string> onChanged)
	{
		Action<string> onChanged2 = onChanged;
		TextBox tb = new TextBox
		{
			Text = text,
			Watermark = watermark,
			FontSize = 13.0,
			Height = 36.0,
			VerticalContentAlignment = (VerticalAlignment)2,
			Background = Skin.PanelAlt,
			Foreground = Skin.Text,
			BorderBrush = (IBrush)(object)Brushes.Transparent,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(4.0),
			Padding = new Thickness(12.0, 0.0, 12.0, 0.0)
		};
		tb.TextChanged += delegate
		{
			onChanged2(tb.Text ?? "");
		};
		AnimateTextBox(tb);
		return tb;
	}

	public static Path Icon(string data, IBrush brush, double width, double height, bool stroke = false, double thickness = 1.8)
	{
		return new Path
		{
			Data = Geometry.Parse(data),
			Fill = (stroke ? null : brush),
			Stroke = (stroke ? brush : null),
			StrokeThickness = thickness,
			StrokeLineCap = (PenLineCap)1,
			StrokeJoin = (PenLineJoin)2,
			Stretch = (Stretch)2,
			Width = width,
			Height = height,
			HorizontalAlignment = (HorizontalAlignment)2,
			VerticalAlignment = (VerticalAlignment)2
		};
	}

	public static void FadeIn(Visual visual, int delayMs = 0, int durationMs = 240)
	{
		Visual visual2 = visual;
		visual2.Opacity = 0.0;
		Visual obj = visual2;
		Transitions val = new Transitions();
		((AvaloniaList<ITransition>)val).Add((ITransition)new DoubleTransition
		{
			Property = (AvaloniaProperty)(object)Visual.OpacityProperty,
			Duration = TimeSpan.FromMilliseconds(durationMs)
		});
		((Animatable)obj).Transitions = val;
		if (delayMs <= 0)
		{
			visual2.Opacity = 1.0;
			return;
		}
		DispatcherTimer.RunOnce((Action)delegate
		{
			visual2.Opacity = 1.0;
		}, TimeSpan.FromMilliseconds(delayMs), default(DispatcherPriority));
	}

	public static void Bounce(Visual visual, double to = 0.9, int holdMs = 110)
	{
		Visual visual2 = visual;
		visual2.RenderTransformOrigin = new RelativePoint(0.5, 0.5, (RelativeUnit)0);
		Visual obj = visual2;
		Transitions val = new Transitions();
		((AvaloniaList<ITransition>)val).Add((ITransition)new TransformOperationsTransition
		{
			Property = (AvaloniaProperty)(object)Visual.RenderTransformProperty,
			Duration = TimeSpan.FromMilliseconds(140.0),
			Easing = (Easing)new CubicEaseOut()
		});
		((Animatable)obj).Transitions = val;
		visual2.RenderTransform = (ITransform)new ScaleTransform(to, to);
		DispatcherTimer.RunOnce((Action)delegate
		{
			visual2.RenderTransform = (ITransform)new ScaleTransform(1.0, 1.0);
		}, TimeSpan.FromMilliseconds(holdMs), default(DispatcherPriority));
	}

	public static void SmoothScroll(ScrollViewer scroll, double step = 110.0)
	{
		ScrollViewer scroll2 = scroll;
		double target = 0.0;
		double current = 0.0;
		DispatcherTimer timer = null;
		((Interactive)scroll2).AddHandler<PointerWheelEventArgs>(InputElement.PointerWheelChangedEvent, (EventHandler<PointerWheelEventArgs>)delegate(object? _, PointerWheelEventArgs e)
		{
			Vector val = e.Delta;
			double y = ((Vector)val).Y;
			if (!(Math.Abs(y) < 0.01))
			{
				Size val2 = scroll2.Extent;
				double height = ((Size)val2).Height;
				val2 = scroll2.Viewport;
				double num2 = Math.Max(0.0, height - ((Size)val2).Height);
				if (!(num2 <= 0.5))
				{
					DispatcherTimer val3 = timer;
					if (val3 == null || !val3.IsEnabled)
					{
						val = scroll2.Offset;
						current = ((Vector)val).Y;
						target = current;
					}
					target = Math.Clamp(target - y * step, 0.0, num2);
					((RoutedEventArgs)e).Handled = true;
					if (timer == null)
					{
						timer = new DispatcherTimer
						{
							Interval = TimeSpan.FromMilliseconds(12.0)
						};
						timer.Tick += delegate
						{
							Tick();
						};
					}
					if (!timer.IsEnabled)
					{
						timer.Start();
					}
				}
			}
		}, (RoutingStrategies)2, false);
		void Tick()
		{
			double num = target - current;
			Vector offset;
			if (Math.Abs(num) < 0.5)
			{
				current = target;
				ScrollViewer obj = scroll2;
				offset = scroll2.Offset;
				obj.Offset = new Vector(((Vector)offset).X, current);
				DispatcherTimer obj2 = timer;
				if (obj2 != null)
				{
					obj2.Stop();
				}
			}
			else
			{
				current += num * 0.14;
				ScrollViewer obj3 = scroll2;
				offset = scroll2.Offset;
				obj3.Offset = new Vector(((Vector)offset).X, current);
			}
		}
	}

	public static void FadeSlideIn(Visual visual, int delayMs = 0, int durationMs = 220, double offset = 10.0)
	{
		Visual visual2 = visual;
		visual2.Opacity = 0.0;
		visual2.RenderTransform = (ITransform)new TranslateTransform(0.0, offset);
		Visual obj = visual2;
		Transitions val = new Transitions();
		((AvaloniaList<ITransition>)val).Add((ITransition)new DoubleTransition
		{
			Property = (AvaloniaProperty)(object)Visual.OpacityProperty,
			Duration = TimeSpan.FromMilliseconds(durationMs),
			Easing = (Easing)new CubicEaseOut()
		});
		((AvaloniaList<ITransition>)val).Add((ITransition)new TransformOperationsTransition
		{
			Property = (AvaloniaProperty)(object)Visual.RenderTransformProperty,
			Duration = TimeSpan.FromMilliseconds(durationMs),
			Easing = (Easing)new CubicEaseOut()
		});
		((Animatable)obj).Transitions = val;
		if (delayMs <= 0)
		{
			Dispatcher.UIThread.Post((Action)Show, default(DispatcherPriority));
		}
		else
		{
			DispatcherTimer.RunOnce((Action)Show, TimeSpan.FromMilliseconds(delayMs), default(DispatcherPriority));
		}
		void Show()
		{
			visual2.Opacity = 1.0;
			visual2.RenderTransform = (ITransform)new TranslateTransform(0.0, 0.0);
		}
	}

	/// <summary>
	/// Обложка для сетки: картинка 460×215 декодируется в 176×264 на карточке,
	/// поэтому сразу уменьшаем её вдвое — и память, и время на отрисовку.
	/// </summary>
	public static Bitmap? LoadCoverThumb(string? path, int width = 320)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
			{
				return null;
			}
			using FileStream fileStream = File.OpenRead(path);
			return Bitmap.DecodeToWidth((Stream)fileStream, width, (BitmapInterpolationMode)2);
		}
		catch (Exception ex)
		{
			AppPaths.Log("LoadCoverThumb: " + ex.Message);
			return LoadBitmap(path);
		}
	}

	public static Bitmap? LoadBitmap(string? path)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
			{
				return null;
			}
			using FileStream fileStream = File.OpenRead(path);
			return new Bitmap((Stream)fileStream);
		}
		catch (Exception ex)
		{
			AppPaths.Log("LoadBitmap failed: " + ex.Message);
			return null;
		}
	}

	public static void Tip(Control control, string text)
	{
		ToolTip.SetTip(control, (object)text);
		ToolTip.SetShowDelay(control, 400);
	}

	public static Border Divider(double height = 1.0)
	{
		return new Border
		{
			Height = height,
			Background = Skin.Border,
			Margin = new Thickness(0.0, 6.0),
			HorizontalAlignment = (HorizontalAlignment)0
		};
	}
}
