using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Ludex.Services;

namespace Ludex.UI;

// Ручное добавление игры: пользователь выбирает папку, Ludex сам определяет
// исполняемый файл и название, а обложку подтягивает как обычно.
//
// Нужно как страховка: автопоиск ошибается — игры лежат на флешках, в архивах,
// в папках со странными именами. Раньше в такой ситуации человек ничего не мог
// сделать, кроме как ждать обновления.
public static class AddGameDialog
{
	public static async Task<AddGameResult?> ShowAsync(Window owner, Func<string, bool> alreadyInLibrary)
	{
		IReadOnlyList<IStorageFolder> folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
		{
			Title = Loc.T("addGame.pickFolder"),
			AllowMultiple = false
		});
		if (folders.Count == 0)
		{
			return null;
		}
		string? folder = StorageProviderExtensions.TryGetLocalPath(folders[0]);
		AppPaths.Log($"AddGame: выбрана папка «{folder}»");
		if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
		{
			return null;
		}
		if (alreadyInLibrary(folder))
		{
			return new AddGameResult(folder, "", "", Loc.T("addGame.already"), Ok: false);
		}

		// Одну папку мог выбрать человек, а игру надо взять конкретную: в «Desktop»
		// или «Games» лежит несколько игр, и брать наибольший exe из соседней папки нельзя.
		Resolved resolved = Resolve(folder);
		folder = resolved.Folder;
		string exe = resolved.Exe;
		string title = SuggestTitle(folder, exe);

		if (!string.IsNullOrWhiteSpace(exe) && !IsGenericName(title))
		{
			// Уточняем название по Steam, но НЕ ждём дольше трёх секунд: при недоступной
			// сети или медленном ответе диалог вообще не открывался, и нажатие кнопки
			// выглядело как «ничего не произошло». Название из папки и так рабочее.
			try
			{
				using CancellationTokenSource identifyCts = new CancellationTokenSource(TimeSpan.FromSeconds(3.0));
				(int appId, string name, double score)? found = await Artwork.IdentifyAsync(title, identifyCts.Token);
				if (found.HasValue && found.Value.score >= 0.6 && !string.IsNullOrWhiteSpace(found.Value.name))
				{
					title = Scanner.CleanTitle(found.Value.name);
				}
			}
			catch (Exception ex)
			{
				AppPaths.Log("AddGameDialog guess: " + ex.Message);
			}
		}
		AppPaths.Log($"AddGame: предлагаю «{title}» / {Path.GetFileName(exe)}");

		if (string.IsNullOrWhiteSpace(exe))
		{
			// При неудаче автопоиска сразу даём выбрать файл, а не закрываем диалог.
			string? selected = await PickExecutableAsync(owner);
			if (selected == null) return null;
			exe = selected;
		}

		AddGameConfirm? result = await ShowConfirmAsync(owner, folder, title, exe, resolved.Ambiguous);
		if (result == null)
		{
			return null;
		}
		return new AddGameResult(folder, result.Title, result.Exe, null, Ok: true);
	}

	public sealed record Resolved(string Folder, string Exe, bool Ambiguous);

	/// <summary>
	/// Определяет, где именно лежит игра. Если exe прямо в выбранной папке — это она.
	/// Если только в подпапке — игрой считаем подпапку (кроме технических имён вроде
	/// «bin» или «x64»: тогда папка игры — выбранная). Когда подходящих exe несколько
	/// в разных папках, помечаем выбор как неоднозначный и просим пользователя проверить.
	/// </summary>
	public static Resolved Resolve(string folder)
	{
		try
		{
			DirectoryInfo dir = new DirectoryInfo(folder);
			FileInfo? direct = BestExe(dir.GetFiles("*.exe", SearchOption.TopDirectoryOnly));
			if (direct != null)
			{
				return new Resolved(folder, direct.FullName, false);
			}
			List<FileInfo> nested = new List<FileInfo>();
			foreach (DirectoryInfo sub in dir.GetDirectories())
			{
				if ((sub.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
				{
					continue;
				}
				try
				{
					FileInfo? hit = BestExe(sub.GetFiles("*.exe", SearchOption.TopDirectoryOnly));
					if (hit != null)
					{
						nested.Add(hit);
						continue;
					}
					foreach (DirectoryInfo inner in sub.GetDirectories())
					{
						FileInfo? deep = BestExe(inner.GetFiles("*.exe", SearchOption.TopDirectoryOnly));
						if (deep != null)
						{
							nested.Add(deep);
						}
					}
				}
				catch
				{
				}
			}
			if (nested.Count == 0)
			{
				return new Resolved(folder, "", false);
			}
			List<FileInfo> ordered = nested.OrderByDescending(f => f.Length).ToList();
			FileInfo best = ordered[0];
			// Несколько сопоставимых по размеру игр в разных папках — выбор неоднозначный.
			bool ambiguous = ordered.Count > 1 && ordered[1].Length * 2 > best.Length;
			string exeDir = Path.GetDirectoryName(best.FullName) ?? folder;
			string folderOut = IsTechnicalDir(Path.GetFileName(exeDir)) ? folder : exeDir;
			return new Resolved(folderOut, best.FullName, ambiguous);
		}
		catch (Exception ex)
		{
			AppPaths.Log("AddGameDialog Resolve: " + ex.Message);
			return new Resolved(folder, "", false);
		}
	}

	private static FileInfo? BestExe(IEnumerable<FileInfo> files)
	{
		return files.Where(f => !IsServiceExe(f.Name) && f.Length >= 262144L)
			.OrderByDescending(f => f.Length)
			.FirstOrDefault();
	}

	private static bool IsTechnicalDir(string name)
	{
		string n = (name ?? "").ToLowerInvariant();
		string[] tech = { "bin", "binaries", "binary", "x64", "x86", "win64", "win32", "win",
			"game", "data", "build", "release", "system", "engine", "retail", "shipping",
			"gamedata", "win_x64", "winx64", "bin64", "exe" };
		return tech.Contains(n);
	}

	/// <summary>Название по папке игры, а если папка служебная — по имени exe.</summary>
	private static string SuggestTitle(string folder, string exe)
	{
		string fromFolder = Scanner.CleanTitle(Path.GetFileName(folder.TrimEnd('\\', '/')));
		if (fromFolder.Length >= 2 && !IsGenericName(fromFolder))
		{
			return fromFolder;
		}
		string fromExe = string.IsNullOrWhiteSpace(exe)
			? ""
			: Scanner.CleanTitle(Path.GetFileNameWithoutExtension(exe));
		return fromExe.Length >= 2 ? fromExe : fromFolder;
	}

	/// <summary>
	/// Служебные имена папок: по ним нельзя угадывать название игры (Steam на «Desktop»
	/// находит игру «Desktop Mate»), и в них обычно лежит несколько игр сразу.
	/// </summary>
	public static bool IsGenericName(string? name)
	{
		string n = (name ?? "").Trim().ToLowerInvariant();
		string[] generic = { "desktop", "рабочий стол", "games", "game", "игры", "игра", "my games",
			"мои игры", "downloads", "загрузки", "new folder", "новая папка", "steam", "steamapps",
			"common", "program files", "program files (x86)", "programdata", "users", "gog games",
			"epic games", "xboxgames", "portable games", "games library", "торренты", "torrents" };
		return n.Length == 0 || generic.Contains(n) || n.Length <= 2;
	}

	/// <summary>
	/// Самый крупный .exe в папке — обычная эвристика: главный файл игры всегда больше
	/// остальных (лаунчеры, краш-репортеры и юнити-хелперы лежат отдельно и весят меньше).
	/// </summary>
	public static string FindExe(string folder)
	{
		try
		{
			DirectoryInfo dir = new DirectoryInfo(folder);
			FileInfo? best = dir.GetFiles("*.exe", SearchOption.TopDirectoryOnly)
				.Where(f => !IsServiceExe(f.Name))
				.OrderByDescending(f => f.Length)
				.FirstOrDefault();
			if (best != null)
			{
				return best.FullName;
			}
			// Вложенные папки: часто игра лежит в подпапке вроде «bin» или «Game».
			FileInfo? nested = dir.GetDirectories()
				.Where(d => (d.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
				.SelectMany(d =>
				{
					try { return d.GetFiles("*.exe", SearchOption.TopDirectoryOnly); }
					catch { return Array.Empty<FileInfo>(); }
				})
				.Where(f => !IsServiceExe(f.Name))
				.OrderByDescending(f => f.Length)
				.FirstOrDefault();
			return nested?.FullName ?? "";
		}
		catch (Exception ex)
		{
			AppPaths.Log("AddGameDialog FindExe: " + ex.Message);
			return "";
		}
	}

	private static bool IsServiceExe(string name)
	{
		string n = Path.GetFileNameWithoutExtension(name).ToLowerInvariant();
		string[] bad = { "unins", "uninstall", "setup", "vcredist", "dxsetup", "crashreport",
			"crashpad", "unitycrashhandler", "eac", "launcher_installer", "updater", "helper" };
		return bad.Any(b => n.Contains(b, StringComparison.Ordinal));
	}

	private static async Task<string?> PickExecutableAsync(Window owner)
	{
		IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = Loc.T("addGame.chooseExe"),
			AllowMultiple = false,
			FileTypeFilter = new[]
			{
				new FilePickerFileType(Loc.T("addGame.allFiles")) { Patterns = new[] { "*" } },
				new FilePickerFileType("EXE") { Patterns = new[] { "*.exe" } }
			}
		});
		string? path = files.Count == 0 ? null : StorageProviderExtensions.TryGetLocalPath(files[0]);
		return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
	}

	private sealed class AddGameConfirm
	{
		public string Title = "";
		public string Exe = "";
	}

	private static async Task<AddGameConfirm?> ShowConfirmAsync(Window owner, string folder, string title, string exe, bool ambiguous)
	{
		AddGameConfirm? result = null;
		Window dialog = new Window
		{
			Title = Loc.T("addGame.title"),
			Width = 560.0,
			Height = 360.0,
			MinHeight = 300.0,
			MinWidth = 500.0,
			CanResize = true,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Background = Skin.Bg
		};

		StackPanel root = new StackPanel
		{
			Spacing = 12.0
		};
		root.Children.Add(Ui.T(Loc.T("addGame.title"), 17.0, Skin.Text, FontWeight.SemiBold, TextWrapping.NoWrap));
		root.Children.Add(Ui.T(folder, 12.0, Skin.TextMuted, FontWeight.Normal, TextWrapping.Wrap));

		if (ambiguous)
		{
			root.Children.Add(Ui.T(Loc.T("addGame.ambiguous"), 12.0, Skin.Accent, FontWeight.Medium, TextWrapping.Wrap));
		}

		root.Children.Add(Ui.T(Loc.T("addGame.nameLabel"), 12.0, Skin.TextMuted, FontWeight.Normal, TextWrapping.NoWrap));
		TextBox nameBox = new TextBox
		{
			Text = title,
			FontSize = 14.0,
			Watermark = Loc.T("addGame.nameHint")
		};
		root.Children.Add(nameBox);

		root.Children.Add(Ui.T(Loc.T("addGame.exeLabel"), 12.0, Skin.TextMuted, FontWeight.Normal, TextWrapping.NoWrap));
		TextBlock exeLabel = Ui.T(Path.GetFileName(exe), 13.0, Skin.Text, FontWeight.Normal, TextWrapping.Wrap);
		root.Children.Add(exeLabel);

		StackPanel buttons = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 8.0,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};

		TextBlock otherExe = Ui.T(Loc.T("addGame.chooseExe"), 12.5, Skin.Text, FontWeight.Medium, TextWrapping.NoWrap);
		Border otherBtn = Ui.Clickable(otherExe, async delegate
		{
			string? path = await PickExecutableAsync(dialog);
			if (path != null)
			{
				exe = path;
				exeLabel.Text = Path.GetFileName(path);
			}
		}, Skin.PanelAlt, Skin.CardHover, new CornerRadius(8.0), new Thickness(14.0, 9.0));
		buttons.Children.Add(otherBtn);

		TextBlock cancelText = Ui.T(Loc.T("btn.cancel"), 12.5, Skin.Text, FontWeight.Medium, TextWrapping.NoWrap);
		Border cancelBtn = Ui.Clickable(cancelText, delegate { dialog.Close(); },
			Skin.PanelAlt, Skin.CardHover, new CornerRadius(8.0), new Thickness(14.0, 9.0));
		buttons.Children.Add(cancelBtn);

		TextBlock okText = Ui.T(Loc.T("addGame.add"), 12.5, Skin.OnAccent, FontWeight.SemiBold, TextWrapping.NoWrap);
		Border okBtn = Ui.Clickable(okText, delegate
		{
			string finalTitle = (nameBox.Text ?? "").Trim();
			if (finalTitle.Length == 0)
			{
				finalTitle = Scanner.CleanTitle(Path.GetFileNameWithoutExtension(exe));
			}
			result = new AddGameConfirm { Title = finalTitle, Exe = exe };
			dialog.Close();
		}, Skin.Accent, Skin.AccentHover, new CornerRadius(8.0), new Thickness(16.0, 9.0));
		buttons.Children.Add(okBtn);

		Grid layout = new Grid
		{
			Margin = new Thickness(22.0),
			RowDefinitions = new RowDefinitions("*,Auto")
		};
		ScrollViewer scroll = new ScrollViewer
		{
			Content = root,
			VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
		};
		layout.Children.Add(scroll);
		Grid.SetRow(buttons, 1);
		layout.Children.Add(buttons);
		dialog.Content = layout;
		nameBox.AttachedToVisualTree += delegate
		{
			nameBox.Focus();
			nameBox.SelectAll();
		};
		await dialog.ShowDialog(owner);
		return result;
	}
}

public sealed record AddGameResult(string Folder, string Title, string Exe, string? Error, bool Ok);
