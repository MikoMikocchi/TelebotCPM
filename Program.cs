using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelebotCPM
{
    internal class Program
    {
        public class CulturalPoint
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string Category { get; set; }
            public string Address { get; set; }
            public string Picture { get; set; }
        }

        public class CulturalPointsRoot
        {
            public List<CulturalPoint> CulturalPoints { get; set; }
        }

        public class CulturalPointsData
        {
            public List<CulturalPoint> CulturalPoints { get; set; }

            // Конструктор, который загружает данные из JSON-файла
            public CulturalPointsData(string jsonFilePath)
            {
                LoadData(jsonFilePath);
            }

            private void LoadData(string jsonFilePath)
            {
                if (System.IO.File.Exists(jsonFilePath))
                {
                    var jsonData = System.IO.File.ReadAllText(jsonFilePath);
                    var rootObject = JsonConvert.DeserializeObject<CulturalPointsRoot>(jsonData);
                    CulturalPoints = rootObject.CulturalPoints;
                }
                else
                {
                    throw new FileNotFoundException("Файл не найден.", jsonFilePath);
                }
            }


            public List<CulturalPoint> GetCulturalPointsByCategory(string category)
            {
                return CulturalPoints
                    .Where(cp => cp.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        private static string ReadToken(string token)
        {
            token = System.IO.File.ReadAllText("token.txt");
            return token;
        }

        static void Main(string[] args)
        {
            string token = ReadToken("token.txt");
            var client = new TelegramBotClient(token);
            client.StartReceiving(Update, Error);
            Console.WriteLine("Бот запущен\n");
            Console.ReadLine();
        }

        async static Task Update(ITelegramBotClient botClient, Update update, CancellationToken token)
        {
            var culturalPointsData = new CulturalPointsData("cultural_points.json");

            var message = update.Message;
            var callbackQuery = update.CallbackQuery;

            // Метод для отображения культурной точки
            async Task ShowCulturalPoint(long chatId, string category, int index)
            {
                List<CulturalPoint> points = culturalPointsData.GetCulturalPointsByCategory(category);
                index = (index + points.Count) % points.Count;

                var point = points[index];

                // Кнопки "Назад" и "Вперед" 
                var arrowsMarkup = new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("<", $"optionArrow.{category}.{index - 1}"), // Левая стрелка
                        InlineKeyboardButton.WithCallbackData(">", $"optionArrow.{category}.{index + 1}"), // Правая стрелка
                    },
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Меню", "optionBackToMenu"),
                    }
                });


                await botClient.SendPhotoAsync(chatId: chatId,
                    photo: $"{point.Picture}",
                    caption: $"{point.Name}\n\n{point.Description}\n\nАдрес: {point.Address}", replyMarkup: arrowsMarkup, parseMode: ParseMode.Html);
            }

            // Метод для обработки нажатия на стрелки
            async Task HandleArrowClick(string callbackData, long chatId, CulturalPointsData culturalPointsData)
            {
                var parts = callbackData.Split('.');
                if (parts.Length == 3)
                {
                    var category = parts[1];
                    var index = int.Parse(parts[2]);

                    // Отображаем точку
                    await ShowCulturalPoint(chatId, category, index);
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, "Неверные данные");
                }
            }


            var MainMenuKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Библиотеки", "optionCategory.Библиотеки"),
                    InlineKeyboardButton.WithCallbackData("Храмы", "optionCategory.Храмы"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Парки", "optionCategory.Парки"),
                    InlineKeyboardButton.WithCallbackData("Музеи", "optionCategory.Музеи"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Памятники", "optionCategory.Памятники"),
                    InlineKeyboardButton.WithCallbackData("Театры", "optionCategory.Театры"),
                }
            });

            if (callbackQuery != null)
            {
                var callbackData = callbackQuery.Data.Split(".")[0];

                switch (callbackData)
                {
                    case "optionCategory":
                        // Показываем первую библиотеку
                        ShowCulturalPoint(callbackQuery.Message.Chat.Id, callbackQuery.Data.Split(".")[1], 0);
                        break;

                    case "optionArrow":
                        // Нажатие на стрелку
                        HandleArrowClick(callbackQuery.Data, callbackQuery.Message.Chat.Id, culturalPointsData);
                        break;

                    case "optionBackToMenu":
                        // Главное меню
                        await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Вы вернулись в основное меню.\n\nВыберите интересующую вас категорию", replyMarkup: MainMenuKeyboard);
                        break;

                        // ... (другие варианты)
                }

                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
            }
            else if (message != null && message.Text != null)
            {
                Console.WriteLine($"{message.Chat.FirstName} | {message.Text}");

                if (message.Text.ToLower().Contains("привет"))
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, $"Здравствуйте, {message.Chat.FirstName}. "
                        + "Добро пожаловать в бот КПМ | Культурное Просвещение в Москве!\n\n" +
                        "Здесь Вы можете выбрать интересующую вас категорию, и я расскажу Вам о местах в Москве, " +
                        "в которые Вы можете сходить!", replyMarkup: MainMenuKeyboard);
                }
                else if (message.Text.ToLower().Contains("/help"))
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Напиши привет");
                }
                else
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Я тебя не понимаю. Напиши /help");
                }
            }
        }

        private static Task Error(ITelegramBotClient client, Exception exception, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}