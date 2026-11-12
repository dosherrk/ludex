using System;
using System.IO;
using System.Threading;
using Avalonia.Threading;

namespace Ludex;

/// <summary>
/// Приложение-одиночка: не даёт открыть вторую копию Ludex, а также служит
/// последней линией защиты от бесконечного перезапуска самого себя.
/// </summary>
internal static class SingleInstance
{
	// Global\, а не Local\: имя с префиксом Local\ живёт в пределах сеанса терминала,
	// и при нескольких сеансах (RDP, «подключиться к консоли») проверка переставала
	// работать — каждый сеанс запускал свою копию. Global\ виден всей машине.
	private const string MutexName = "Global\\Ludex.SingleInstance";

	private const string ShowEventName = "Global\\Ludex.ShowWindow";

	// Имена прежних сборок (1.21.0 использовала Local\) — чтобы связка «старая копия
	// уже работает, запускаем новую» тоже поднимала окно, а не открывала второе.
	private const string LegacyMutexName = "Local\\Ludex.SingleInstance";

	private const string LegacyShowEventName = "Local\\Ludex.ShowWindow";

	private static readonly string StampDir =
		Path.Combine(Path.GetTempPath(), "Ludex.restart");

	// Порог «слишком часто»: сколько запусков за сколько секунд.
	private static readonly int[][] RestartLimits =
	{
		new int[2] { 4, 60 },
		new int[2] { 12, 600 }
	};

	private static Mutex? _mutex;

	private static EventWaitHandle? _showEvent;

	private static EventWaitHandle? _legacyEvent;

	public static bool IsFirst { get; private set; } = true;

	/// <summary>
	/// Занимает мутекс. false — значит, Ludex уже запущен; в этом случае
	/// работающий экземпляр получает сигнал показать окно.
	/// </summary>
	public static bool Claim()
	{
		try
		{
			_mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
			IsFirst = createdNew;
			if (!createdNew)
			{
				AppPaths.Log("SingleInstance: копия уже запущена, поднимаю её окно");
				SignalExisting();
				return false;
			}
			// Владение мутексом после проверки не нужно: он существует всё время,
			// пока жив процесс, а поток UI занимать нельзя.
			try
			{
				_mutex.ReleaseMutex();
			}
			catch (ApplicationException)
			{
				// мутекс уже не принадлежал этому потоку — ничего страшного
			}
			return true;
		}
		catch (Exception ex)
		{
			AppPaths.Log("SingleInstance: " + ex.Message);
			IsFirst = true;
			return true;
		}
	}

	/// <summary>Поднимает окно уже запущенного экземпляра.</summary>
	public static void SignalExisting()
	{
		SetEvent(ShowEventName);
		SetEvent(LegacyShowEventName);
	}

	private static void SetEvent(string name)
	{
		try
		{
			if (EventWaitHandle.TryOpenExisting(name, out EventWaitHandle? handle))
			{
				using (handle)
				{
					handle.Set();
				}
			}
		}
		catch (Exception ex)
		{
			AppPaths.Log("SingleInstance signal " + name + ": " + ex.Message);
		}
	}

	/// <summary>Слушает сигнал «покажи окно» от повторных запусков.</summary>
	public static void Listen(Action show)
	{
		_showEvent = ListenOn(ShowEventName, show);
		_legacyEvent = ListenOn(LegacyShowEventName, show);
	}

	private static EventWaitHandle? ListenOn(string name, Action show)
	{
		try
		{
			EventWaitHandle handle = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, name);
			ThreadPool.RegisterWaitForSingleObject(handle, delegate
			{
				Dispatcher.UIThread.Post(show, DispatcherPriority.Normal);
			}, null, Timeout.Infinite, executeOnlyOnce: false);
			return handle;
		}
		catch (Exception ex)
		{
			AppPaths.Log("SingleInstance listen " + name + ": " + ex.Message);
			return null;
		}
	}

	/// <summary>
	/// Защита от лавины запусков. Если Ludex стартует слишком часто (его кто-то
	/// перезапускает по кругу — например, помощник обновления или автозапуск после
	/// падения), следующий экземпляр не запускается, а сообщает причину.
	/// Возвращает null, если стартовать можно, иначе — текст для пользователя.
	/// </summary>
	public static string? CheckRestartStorm()
	{
		try
		{
			Directory.CreateDirectory(StampDir);
			DateTime now = DateTime.UtcNow;
			foreach (string file in Directory.GetFiles(StampDir, "*.tick"))
			{
				if ((now - File.GetLastWriteTimeUtc(file)).TotalSeconds > 1200.0)
				{
					try
					{
						File.Delete(file);
					}
					catch
					{
						// файл занят — не мешает
					}
				}
			}
			string current = Path.Combine(StampDir, now.ToString("yyyyMMddHHmmssfff") + ".tick");
			File.WriteAllText(current, Environment.ProcessId.ToString());

			foreach (int[] limit in RestartLimits)
			{
				int seconds = limit[0];
				int allowed = limit[1];
				int count = 0;
				foreach (string file in Directory.GetFiles(StampDir, "*.tick"))
				{
					if ((now - File.GetLastWriteTimeUtc(file)).TotalSeconds <= seconds)
					{
						count++;
					}
				}
				if (count > allowed)
				{
					AppPaths.Log($"SingleInstance: защита от цикла — {count} запусков за {seconds} с");
					return "Ludex запускался " + count + " раз за " + seconds + " секунд. "
						+ "Похоже, приложение падает при старте, а его кто-то запускает заново "
						+ "(автозапуск, помощник обновления или ярлык).\n\n"
						+ "Цикл остановлен. Журнал: " + AppPaths.LogFile;
				}
			}
			return null;
		}
		catch (Exception ex)
		{
			AppPaths.Log("SingleInstance restart guard: " + ex.Message);
			return null;
		}
	}
}
