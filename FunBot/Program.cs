using IniParser.Model;
using IniParser;
using NLog;
using Telegram.Bot.Types;
using Telegram.Bot;
using System.Runtime.InteropServices;
using System.Reflection;
using Telegram.Bot.Types.ReplyMarkups;

namespace FunBot
{
    internal class Program
    {
		private static readonly string moduleName = "Program";
		private static readonly Logger baseLogger = LogManager.GetLogger(moduleName);
		private static readonly LoggerManager logger = new(baseLogger, moduleName);

		private static string _token = string.Empty;
		public static string TOKEN
		{
			get
			{
				return _token;
			}
		}
		private static int CHAT_ID { get; set; } = 0;
		private static int ADMIN_ID { get; set; } = 0;
		private static double HOUR_TIME { get; set; } = 8;

		private const string filePathConfig = "config.ini";
		private static string configTextDefault = string.Empty;

		private static void initConfig()
		{
			FileIniDataParser parser = new FileIniDataParser();

			if (File.Exists(filePathConfig))
			{
				logger.Info($"Чтение конфигурационного файла.");

				IniData data = parser.ReadFile(filePathConfig);
				_token = data["Settings"]["TOKEN"];
				CHAT_ID = Convert.ToInt32(data["Settings"]["CHAT_ID"]);
				ADMIN_ID = Convert.ToInt32(data["Settings"]["ADMIN_ID"]);
				HOUR_TIME = Convert.ToDouble(data["Settings"]["HOUR_TIME"]);
			}
			else
			{
				logger.Info($"Создание конфигурационного файла.");

				IniData data = new IniData();
				data.Sections.AddSection("Settings");
				data["Settings"]["TOKEN"] = _token.ToString();
				data["Settings"]["CHAT_ID"] = CHAT_ID.ToString();
				data["Settings"]["ADMIN_ID"] = ADMIN_ID.ToString();
				data["Settings"]["HOUR_TIME"] = HOUR_TIME.ToString();

				parser.WriteFile(filePathConfig, data);
			}

			configTextDefault = $"TOKEN = [{_token}]\n" +
								$"CHAT_ID = [{CHAT_ID}]\n" +
								$"ADMIN_ID = [{ADMIN_ID}]\n" +
								$"HOUR_TIME = [{HOUR_TIME}]";
		}

		private static Dictionary<string, Func<ITelegramBotClient, Message, Task>> _commands = [];
		static async Task Main(string[] args)
		{
			logger.Info($"Starting...");
			initConfig();

			AppDomain.CurrentDomain.ProcessExit += OnProcessExit; // Для ProcessExit
			Console.CancelKeyPress += OnCancelKeyPress;          // Для Ctrl+C (SIGINT)

			// Подписываемся на SIGTERM (только для Linux)
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				UnixSignalHandler.Register(Signum.SIGTERM, OnSigTerm);
			}

			logger.Info(configTextDefault);

			logger.Info($"Done!");

			_commands = new Dictionary<string, Func<ITelegramBotClient, Message, Task>>();
			RegisterCommands();

			HostBot bot = new HostBot(_token);
			bot.Start();
			bot.OnMessage += OnMessage;

			System.Timers.Timer timer = new System.Timers.Timer(HOUR_TIME * 3600.0 * 1000.0); // Таймер с интервалом в N часов (N * 3600 * 1000 миллисекунд)
			if (HostBot.BotClient != null)
			{
				await HostBot.BotClient.SendMessage(CHAT_ID, "Сервер был запущен. Системный бот активен.");

				timer.Elapsed += async (sender, e) =>
				{
					//SystemTools systemTools = new SystemTools();
					//string message = $"CPU Load: {systemTools.GetCpuLoad()}\n" +
					//				 $"CPU Temperature: {systemTools.GetCpuTemperature()}\n" +
					//				 $"RAM Usage: {systemTools.GetRamUsage()}\n" +
					//				 $"DISK Usage: {systemTools.GetDiskUsage()}";
					//foreach (long id in chatIDs)
					//{
					//	await HostBot.BotClient.SendMessage(id, message);
					//}
				};
				timer.AutoReset = true; // Повторять каждые N часов
				timer.Enabled = true;
			}
		}

		private static async void OnMessage(ITelegramBotClient client, Update update)
		{
			try
			{
				if (update.Message == null)
				{
					return;
				}

				if (update.Message.Text != null && _commands.TryGetValue(update.Message.Text, out var commandHandler))
				{
					// Проверяем, есть ли команда в словаре
					await commandHandler(client, update.Message);
				}
			}
			catch (Exception ex)
			{
				if (update.Message == null)
				{
					return;
				}

				await client.SendMessage(update.Message.Chat.Id, "Ошибка! Кажется что-то пошло не так, попробуйте позже.", replyMarkup: new ReplyKeyboardRemove());
				logger.Error($"Ошибка в время обработки сообщения: {ex.Message}");
			}
		}

		public static void RegisterCommands()
		{
			var methods = Assembly.GetExecutingAssembly()
				.GetTypes()
				.SelectMany(t => t.GetMethods())
				.Where(m => m.GetCustomAttributes(typeof(CommandAttribute), false).Length > 0);

			foreach (var method in methods)
			{
				// Получаем все атрибуты [Command] для метода
				var attributes = method.GetCustomAttributes(typeof(CommandAttribute), false);

				// Регистрируем каждый атрибут
				foreach (CommandAttribute attribute in attributes)
				{
					_commands[attribute.Name] = (Func<ITelegramBotClient, Message, Task>)Delegate.CreateDelegate(typeof(Func<ITelegramBotClient, Message, Task>), method);
				}
			}
		}

		// Атрибут для пометки методов-обработчиков
		[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
		public class CommandAttribute : Attribute
		{
			public string Name { get; }

			public CommandAttribute(string name)
			{
				Name = name;
			}
		}

		//----------------------------------------- SYSTEM -----------------------------------------

		//public static async Task RemoveKeyboardForAll(ITelegramBotClient client)
		//{
		//	foreach (long id in chatIDs)
		//	{
		//		await client.SendMessage(id, "Системный бот выключается.", replyMarkup: new ReplyKeyboardRemove());
		//	}
		//}

		private static bool _isExiting = false; // Флаг для отслеживания состояния завершения
		private static readonly object _lock = new object(); // Объект для блокировки
		private static async void OnProcessExit(object? sender, EventArgs e)
		{
			lock (_lock)
			{
				if (_isExiting) return; // Если уже завершаемся, выходим
				_isExiting = true; // Устанавливаем флаг
			}

			logger.Info("Обработчик ProcessExit: завершение работы...");

			try
			{
				//SaveIDs(chatIDs);
				//if (Host.BotClient != null)
				//	await RemoveKeyboardForAll(Host.BotClient);
			}
			catch (Exception ex)
			{
				logger.Error($"Ошибка завершения работы: {ex.Message}");
			}
			finally
			{
				Environment.Exit(0); // Завершаем программу
			}
		}

		private static async void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
		{
			lock (_lock)
			{
				if (_isExiting) return; // Если уже завершаемся, выходим
				_isExiting = true; // Устанавливаем флаг
			}

			logger.Info("Обработчик Ctrl+C (SIGINT): завершение работы...");
			e.Cancel = true; // Предотвращаем завершение процесса по умолчанию

			try
			{
				//SaveIDs(chatIDs);
				//if (Host.BotClient != null)
				//	await RemoveKeyboardForAll(Host.BotClient);
			}
			catch (Exception ex)
			{
				logger.Error($"Ошибка завершения работы: {ex.Message}");
			}
			finally
			{
				Environment.Exit(0); // Завершаем программу
			}
		}

		private static async void OnSigTerm()
		{
			lock (_lock)
			{
				if (_isExiting) return; // Если уже завершаемся, выходим
				_isExiting = true; // Устанавливаем флаг
			}

			logger.Info("Обработчик SIGTERM: завершение работы...");

			try
			{
				//SaveIDs(chatIDs);
				//if (Host.BotClient != null)
				//	await RemoveKeyboardForAll(Host.BotClient);
			}
			catch (Exception ex)
			{
				logger.Error($"Ошибка завершения работы: {ex.Message}");
			}
			finally
			{
				Environment.Exit(0); // Завершаем программу
			}
		}
	}
}
