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

public sealed class ColorPicker : StackPanel
{
	public const double SquareWidth = 252.0;

	public const double SquareHeight = 152.0;

	private const double SliderWidth = 148.0;

	private const double SliderHeight = 16.0;

	private const double MarkerSize = 14.0;

	private double _hue = 210.0;

	private double _saturation = 0.85;

	private double _value = 0.9;

	private readonly Canvas _square = new Canvas
	{
		Width = 252.0,
		Height = 152.0,
		Background = (IBrush)(object)Brushes.Transparent
	};

	private readonly Border _squareHue = new Border
	{
		Width = 252.0,
		Height = 152.0
	};

	private readonly Ellipse _squareMarker = new Ellipse();

	private readonly Canvas _slider = new Canvas
	{
		Width = 148.0,
		Height = 16.0,
		Background = (IBrush)(object)Brushes.Transparent
	};

	private readonly Ellipse _sliderMarker = new Ellipse();

	private readonly Ellipse _preview = new Ellipse();

	private readonly TextBox _rBox;

	private readonly TextBox _gBox;

	private readonly TextBox _bBox;

	private TextBlock _hexLabel;

	private bool _squareDragging;

	private bool _sliderDragging;

	private bool _updating;

	public Color CurrentColor => HsvToColor(_hue, _saturation, _value);

	public event Action<Color>? ColorChanged;

	public event Action<Color>? ColorCommitted;

	public ColorPicker()
	{
		((StackPanel)this).Spacing = 10.0;
		BuildSquare();
		((AvaloniaList<Control>)(object)((Panel)this).Children).Add((Control)(object)_square);
		((AvaloniaList<Control>)(object)((Panel)this).Children).Add(BuildControls());
		_rBox = NewBox();
		_gBox = NewBox();
		_bBox = NewBox();
		_rBox.TextChanged += delegate
		{
			ApplyRgb();
		};
		_gBox.TextChanged += delegate
		{
			ApplyRgb();
		};
		_bBox.TextChanged += delegate
		{
			ApplyRgb();
		};
		((AvaloniaList<Control>)(object)((Panel)this).Children).Add(BuildRgbRow());
		Refresh();
	}

	private void BuildSquare()
	{
		_squareHue.Background = (IBrush)new SolidColorBrush(HsvToColor(_hue, 1.0, 1.0), 1.0);
		Border val = new Border
		{
			Width = 252.0,
			Height = 152.0
		};
		LinearGradientBrush val2 = new LinearGradientBrush
		{
			StartPoint = new RelativePoint(0.0, 0.0, (RelativeUnit)0),
			EndPoint = new RelativePoint(1.0, 0.0, (RelativeUnit)0)
		};
		((AvaloniaList<GradientStop>)(object)((GradientBrush)val2).GradientStops).Add(new GradientStop(Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue), 0.0));
		((AvaloniaList<GradientStop>)(object)((GradientBrush)val2).GradientStops).Add(new GradientStop(Color.FromArgb((byte)0, byte.MaxValue, byte.MaxValue, byte.MaxValue), 1.0));
		val.Background = (IBrush)val2;
		((InputElement)val).IsHitTestVisible = false;
		Border val3 = val;
		Border val4 = new Border
		{
			Width = 252.0,
			Height = 152.0
		};
		LinearGradientBrush val5 = new LinearGradientBrush
		{
			StartPoint = new RelativePoint(0.0, 0.0, (RelativeUnit)0),
			EndPoint = new RelativePoint(0.0, 1.0, (RelativeUnit)0)
		};
		((AvaloniaList<GradientStop>)(object)((GradientBrush)val5).GradientStops).Add(new GradientStop(Color.FromArgb((byte)0, (byte)0, (byte)0, (byte)0), 0.0));
		((AvaloniaList<GradientStop>)(object)((GradientBrush)val5).GradientStops).Add(new GradientStop(Color.FromArgb(byte.MaxValue, (byte)0, (byte)0, (byte)0), 1.0));
		val4.Background = (IBrush)val5;
		((InputElement)val4).IsHitTestVisible = false;
		Border val6 = val4;
		((Layoutable)_squareMarker).Width = 14.0;
		((Layoutable)_squareMarker).Height = 14.0;
		((Shape)_squareMarker).Stroke = (IBrush)(object)Brushes.White;
		((Shape)_squareMarker).StrokeThickness = 2.0;
		((InputElement)_squareMarker).IsHitTestVisible = false;
		Border val7 = new Border
		{
			Width = 252.0,
			Height = 152.0,
			BorderBrush = Skin.GlassEdge,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(8.0),
			IsHitTestVisible = false
		};
		_square.ClipToBounds = false;
		((Layoutable)this).HorizontalAlignment = (HorizontalAlignment)1;
		((AvaloniaList<Control>)(object)((Panel)_square).Children).Add((Control)(object)_squareHue);
		((AvaloniaList<Control>)(object)((Panel)_square).Children).Add((Control)(object)val3);
		((AvaloniaList<Control>)(object)((Panel)_square).Children).Add((Control)(object)val6);
		((AvaloniaList<Control>)(object)((Panel)_square).Children).Add((Control)(object)_squareMarker);
		((AvaloniaList<Control>)(object)((Panel)_square).Children).Add((Control)(object)val7);
		((InputElement)_square).Cursor = new Cursor((StandardCursorType)3);
		((InputElement)_square).PointerPressed += delegate(object? _, PointerPressedEventArgs e)
		{
			PointerPoint currentPoint = ((PointerEventArgs)e).GetCurrentPoint((Visual)(object)_square);
			PointerPointProperties properties = ((PointerPoint)currentPoint).Properties;
			if (((PointerPointProperties)properties).IsLeftButtonPressed)
			{
				((PointerEventArgs)e).Pointer.Capture(_square);
				_squareDragging = true;
				PickSquare(((PointerEventArgs)e).GetPosition((Visual)(object)_square));
				((RoutedEventArgs)e).Handled = true;
			}
		};
		((InputElement)_square).PointerMoved += delegate(object? _, PointerEventArgs e)
		{
			if (_squareDragging)
			{
				PickSquare(e.GetPosition((Visual)(object)_square));
				((RoutedEventArgs)e).Handled = true;
			}
		};
		((InputElement)_square).PointerReleased += delegate(object? _, PointerReleasedEventArgs e)
		{
			if (_squareDragging)
			{
				_squareDragging = false;
				((PointerEventArgs)e).Pointer.Capture((IInputElement)null);
				this.ColorCommitted?.Invoke(CurrentColor);
				((RoutedEventArgs)e).Handled = true;
			}
		};
	}

	private Control BuildControls()
	{
		((Layoutable)_preview).Width = 34.0;
		((Layoutable)_preview).Height = 34.0;
		((Shape)_preview).Stroke = Skin.GlassEdge;
		((Shape)_preview).StrokeThickness = 2.0;
		((Layoutable)_preview).VerticalAlignment = (VerticalAlignment)2;
		Border val = new Border
		{
			Width = 148.0,
			Height = 16.0,
			CornerRadius = new CornerRadius(8.0),
			Background = (IBrush)new LinearGradientBrush
			{
				StartPoint = new RelativePoint(0.0, 0.0, (RelativeUnit)0),
				EndPoint = new RelativePoint(1.0, 0.0, (RelativeUnit)0),
				GradientStops = HueStops()
			},
			IsHitTestVisible = false
		};
		((Layoutable)_sliderMarker).Width = 16.0;
		((Layoutable)_sliderMarker).Height = 16.0;
		((Shape)_sliderMarker).Stroke = (IBrush)(object)Brushes.White;
		((Shape)_sliderMarker).StrokeThickness = 2.5;
		((InputElement)_sliderMarker).IsHitTestVisible = false;
		((AvaloniaList<Control>)(object)((Panel)_slider).Children).Add((Control)(object)val);
		((AvaloniaList<Control>)(object)((Panel)_slider).Children).Add((Control)(object)_sliderMarker);
		((InputElement)_slider).Cursor = new Cursor((StandardCursorType)9);
		((Layoutable)_slider).VerticalAlignment = (VerticalAlignment)2;
		((InputElement)_slider).PointerPressed += delegate(object? _, PointerPressedEventArgs e)
		{
			PointerPoint currentPoint = ((PointerEventArgs)e).GetCurrentPoint((Visual)(object)_slider);
			PointerPointProperties properties = ((PointerPoint)currentPoint).Properties;
			if (((PointerPointProperties)properties).IsLeftButtonPressed)
			{
				((PointerEventArgs)e).Pointer.Capture(_slider);
				_sliderDragging = true;
				PickHue(((PointerEventArgs)e).GetPosition((Visual)(object)_slider));
				((RoutedEventArgs)e).Handled = true;
			}
		};
		((InputElement)_slider).PointerMoved += delegate(object? _, PointerEventArgs e)
		{
			if (_sliderDragging)
			{
				PickHue(e.GetPosition((Visual)(object)_slider));
				((RoutedEventArgs)e).Handled = true;
			}
		};
		((InputElement)_slider).PointerReleased += delegate(object? _, PointerReleasedEventArgs e)
		{
			if (_sliderDragging)
			{
				_sliderDragging = false;
				((PointerEventArgs)e).Pointer.Capture((IInputElement)null);
				this.ColorCommitted?.Invoke(CurrentColor);
				((RoutedEventArgs)e).Handled = true;
			}
		};
		_hexLabel = Ui.T("", 11.5, Skin.TextDim, (FontWeight)500, (TextWrapping)0);
		StackPanel val2 = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 10.0,
			VerticalAlignment = (VerticalAlignment)2
		};
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)_preview);
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)_slider);
		((AvaloniaList<Control>)(object)((Panel)val2).Children).Add((Control)(object)_hexLabel);
		return (Control)val2;
	}

	private static TextBox NewBox()
	{
		TextBox val = new TextBox
		{
			Width = 62.0,
			Height = 32.0,
			FontSize = 12.5,
			Text = "0",
			TextAlignment = (TextAlignment)1,
			Background = Skin.PanelAlt,
			Foreground = Skin.Text,
			BorderBrush = Skin.Border,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(6.0),
			Padding = new Thickness(4.0, 0.0, 4.0, 0.0),
			VerticalContentAlignment = (VerticalAlignment)2
		};
		Ui.AnimateTextBox(val);
		return val;
	}

	private Control BuildRgbRow()
	{
		StackPanel val = new StackPanel
		{
			Orientation = (Orientation)0,
			Spacing = 10.0
		};
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add(RgbColumn(_rBox, "R"));
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add(RgbColumn(_gBox, "G"));
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add(RgbColumn(_bBox, "B"));
		return (Control)val;
	}

	private static Control RgbColumn(TextBox box, string label)
	{
		StackPanel val = new StackPanel
		{
			Spacing = 3.0,
			Width = 62.0
		};
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)box);
		TextBlock val2 = Ui.T(label, 10.5, Skin.TextDim, (FontWeight)500, (TextWrapping)0);
		((Layoutable)val2).HorizontalAlignment = (HorizontalAlignment)2;
		((AvaloniaList<Control>)(object)((Panel)val).Children).Add((Control)(object)val2);
		return (Control)val;
	}

	private static GradientStops HueStops()
	{
		GradientStops val = new GradientStops();
		for (int i = 0; i <= 6; i++)
		{
			((AvaloniaList<GradientStop>)(object)val).Add(new GradientStop(HsvToColor(i * 60, 1.0, 1.0), (double)i / 6.0));
		}
		return val;
	}

	public void SetColor(Color color)
	{
		ColorToHsv(color, out _hue, out _saturation, out _value);
		Refresh();
	}

	private void PickSquare(Point point)
	{
		Rect bounds = _square.Bounds;
		double val;
		if (!(((Rect)bounds).Width > 1.0))
		{
			val = 252.0;
		}
		else
		{
			bounds = _square.Bounds;
			val = ((Rect)bounds).Width;
		}
		double num = Math.Max(1.0, val);
		bounds = _square.Bounds;
		double val2;
		if (!(((Rect)bounds).Height > 1.0))
		{
			val2 = 152.0;
		}
		else
		{
			bounds = _square.Bounds;
			val2 = ((Rect)bounds).Height;
		}
		double num2 = Math.Max(1.0, val2);
		_saturation = Math.Clamp(((Point)point).X / num, 0.0, 1.0);
		_value = Math.Clamp(1.0 - ((Point)point).Y / num2, 0.0, 1.0);
		Refresh();
		this.ColorChanged?.Invoke(CurrentColor);
	}

	private void PickHue(Point point)
	{
		Rect bounds = _slider.Bounds;
		double val;
		if (!(((Rect)bounds).Width > 1.0))
		{
			val = 148.0;
		}
		else
		{
			bounds = _slider.Bounds;
			val = ((Rect)bounds).Width;
		}
		double num = Math.Max(1.0, val);
		_hue = Math.Clamp(((Point)point).X / num, 0.0, 1.0) * 360.0;
		Refresh();
		this.ColorChanged?.Invoke(CurrentColor);
	}

	private void ApplyRgb()
	{
		if (!_updating && TryByte(_rBox.Text, out var value) && TryByte(_gBox.Text, out var value2) && TryByte(_bBox.Text, out var value3))
		{
			ColorToHsv(Color.FromRgb(value, value2, value3), out _hue, out _saturation, out _value);
			Refresh();
			this.ColorChanged?.Invoke(CurrentColor);
			this.ColorCommitted?.Invoke(CurrentColor);
		}
	}

	private static bool TryByte(string? text, out byte value)
	{
		value = 0;
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		if (!int.TryParse(text.Trim(), out var result))
		{
			return false;
		}
		if (result < 0 || result > 255)
		{
			return false;
		}
		value = (byte)result;
		return true;
	}

	private void Refresh()
	{
		Color currentColor = CurrentColor;
		_squareHue.Background = (IBrush)new SolidColorBrush(HsvToColor(_hue, 1.0, 1.0), 1.0);
		Rect bounds = _square.Bounds;
		double num;
		if (!(((Rect)bounds).Width > 1.0))
		{
			num = 252.0;
		}
		else
		{
			bounds = _square.Bounds;
			num = ((Rect)bounds).Width;
		}
		double num2 = num;
		bounds = _square.Bounds;
		double num3;
		if (!(((Rect)bounds).Height > 1.0))
		{
			num3 = 152.0;
		}
		else
		{
			bounds = _square.Bounds;
			num3 = ((Rect)bounds).Height;
		}
		double num4 = num3;
		Canvas.SetLeft((AvaloniaObject)(object)_squareMarker, _saturation * num2 - 7.0);
		Canvas.SetTop((AvaloniaObject)(object)_squareMarker, (1.0 - _value) * num4 - 7.0);
		((Shape)_squareMarker).Fill = (IBrush)new SolidColorBrush(currentColor, 1.0);
		bounds = _slider.Bounds;
		double num5;
		if (!(((Rect)bounds).Width > 1.0))
		{
			num5 = 148.0;
		}
		else
		{
			bounds = _slider.Bounds;
			num5 = ((Rect)bounds).Width;
		}
		double num6 = num5;
		bounds = _slider.Bounds;
		double num7;
		if (!(((Rect)bounds).Height > 1.0))
		{
			num7 = 16.0;
		}
		else
		{
			bounds = _slider.Bounds;
			num7 = ((Rect)bounds).Height;
		}
		double num8 = num7;
		Canvas.SetLeft((AvaloniaObject)(object)_sliderMarker, _hue / 360.0 * num6 - 8.0);
		Canvas.SetTop((AvaloniaObject)(object)_sliderMarker, num8 / 2.0 - 8.0);
		((Shape)_sliderMarker).Fill = (IBrush)new SolidColorBrush(currentColor, 1.0);
		((Shape)_preview).Fill = (IBrush)new SolidColorBrush(currentColor, 1.0);
		_hexLabel.Text = Skin.ColorHex(currentColor).ToUpperInvariant();
		_updating = true;
		_rBox.Text = ((Color)currentColor).R.ToString();
		_gBox.Text = ((Color)currentColor).G.ToString();
		_bBox.Text = ((Color)currentColor).B.ToString();
		_updating = false;
	}

	public static Color HsvToColor(double hue, double saturation, double value)
	{
		hue = (hue % 360.0 + 360.0) % 360.0;
		saturation = Math.Clamp(saturation, 0.0, 1.0);
		value = Math.Clamp(value, 0.0, 1.0);
		double num = value * saturation;
		double num2 = num * (1.0 - Math.Abs(hue / 60.0 % 2.0 - 1.0));
		double num3 = value - num;
		double num4 = 0.0;
		double num5 = 0.0;
		double num6 = 0.0;
		if (hue < 60.0)
		{
			num4 = num;
			num5 = num2;
		}
		else if (hue < 120.0)
		{
			num4 = num2;
			num5 = num;
		}
		else if (hue < 180.0)
		{
			num5 = num;
			num6 = num2;
		}
		else if (hue < 240.0)
		{
			num5 = num2;
			num6 = num;
		}
		else if (hue < 300.0)
		{
			num4 = num2;
			num6 = num;
		}
		else
		{
			num4 = num;
			num6 = num2;
		}
		return Color.FromRgb((byte)Math.Round((num4 + num3) * 255.0), (byte)Math.Round((num5 + num3) * 255.0), (byte)Math.Round((num6 + num3) * 255.0));
	}

	public static void ColorToHsv(Color color, out double hue, out double saturation, out double value)
	{
		double num = (double)(int)((Color)color).R / 255.0;
		double num2 = (double)(int)((Color)color).G / 255.0;
		double num3 = (double)(int)((Color)color).B / 255.0;
		double num4 = Math.Max(num, Math.Max(num2, num3));
		double num5 = Math.Min(num, Math.Min(num2, num3));
		double num6 = num4 - num5;
		value = num4;
		saturation = ((num4 <= 0.0) ? 0.0 : (num6 / num4));
		if (num6 <= 0.0)
		{
			hue = 0.0;
			return;
		}
		if (num4 == num)
		{
			hue = 60.0 * ((num2 - num3) / num6 % 6.0);
		}
		else if (num4 == num2)
		{
			hue = 60.0 * ((num3 - num) / num6 + 2.0);
		}
		else
		{
			hue = 60.0 * ((num - num2) / num6 + 4.0);
		}
		if (hue < 0.0)
		{
			hue += 360.0;
		}
	}
}
