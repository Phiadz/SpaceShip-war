using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SpaceShipWar;

public partial class App : Application
{
	private static readonly string LogDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
	private static readonly string LogFile = Path.Combine(LogDirectory, "crash.log");

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		DispatcherUnhandledException += OnDispatcherUnhandledException;
		AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
		TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
	}

	private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		WriteCrashLog("DispatcherUnhandledException", e.Exception);
		MessageBox.Show(
			$"Ung dung gap loi va da duoc ghi log:\n{LogFile}\n\n{e.Exception.Message}",
			"SpaceShip War - Error",
			MessageBoxButton.OK,
			MessageBoxImage.Error);

		// Keep app alive so user can continue testing after transient errors.
		e.Handled = true;
	}

	private static void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
	{
		var ex = e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception");
		WriteCrashLog("CurrentDomainUnhandledException", ex);
	}

	private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		WriteCrashLog("TaskSchedulerUnobservedTaskException", e.Exception);
		e.SetObserved();
	}

	private static void WriteCrashLog(string source, Exception exception)
	{
		try
		{
			Directory.CreateDirectory(LogDirectory);
			var builder = new StringBuilder();
			builder.AppendLine("========================================");
			builder.AppendLine($"Time   : {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}");
			builder.AppendLine($"Source : {source}");
			builder.AppendLine($"Type   : {exception.GetType().FullName}");
			builder.AppendLine($"Error  : {exception.Message}");
			builder.AppendLine("Stack  :");
			builder.AppendLine(exception.StackTrace ?? "<no stack trace>");
			builder.AppendLine();
			File.AppendAllText(LogFile, builder.ToString(), Encoding.UTF8);
		}
		catch
		{
			// Swallow logging failures to avoid recursive crash loops.
		}
	}
}
