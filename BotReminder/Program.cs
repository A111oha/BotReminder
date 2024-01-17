using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using dotenv.net;
using Telegram.Bot.Extensions;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Polling;
using Telegram.Bots.Extensions.Polling;
using BotReminder;
using Telegram.Bot.Types.ReplyMarkups;



internal class Program
{
    static ITelegramBotClient botClient = new TelegramBotClient("6638228611:AAFWu_tUCWY8yfpNa6aoFt3H-Ctsa0_rumA");
    private static Dictionary<long, Timer> chatReminders = new Dictionary<long, Timer>();

    public static async Task Main(string[] args)
    {
        using CancellationTokenSource cts = new();

        // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
        ReceiverOptions receiverOptions = new()
        {
            AllowedUpdates = Array.Empty<Telegram.Bot.Types.Enums.UpdateType>() // receive all update types except ChatMember related updates
        };
       

        botClient.StartReceiving(
     updateHandler: async (bot, update, cancellationToken) => await HandleUpdateAsync(bot, update, cancellationToken),
     pollingErrorHandler: (bot, exception, cancellationToken) => HandlePollingErrorAsync(bot, exception, cancellationToken),
     receiverOptions: receiverOptions,
     cancellationToken: cts.Token
      );
      

        var me = await botClient.GetMeAsync();

        Console.WriteLine($"Start listening for @{me.Username}");
        Console.ReadLine();

        // Send cancellation request to stop bot
        cts.Cancel();
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        // Only process Message updates: https://core.telegram.org/bots/api#message
        if (update.Message is not { } message)
            return;
        // Only process text messages
        if (message.Text is not { } messageText)
            return;

        var chatId = message.Chat.Id;

        Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");

        if (update.Message.Text == "/start")
        {
            var mainMenuKeyboard = new ReplyKeyboardMarkup(new[]
            {
            new[]
            {
                new KeyboardButton("/create")
            }
        })
            {
                ResizeKeyboard = true
            };

            await botClient.SendTextMessageAsync(
                chatId: update.Message.Chat.Id,
                text: "Виберіть опцію:",
                replyMarkup: mainMenuKeyboard
            );
        }
        if (messageText.StartsWith("/create"))
        {
            // Command to start creating new data
            await StartDataCreationAsync(botClient, chatId, cancellationToken);
        }
        else
        {
            // Continue data creation process for other user inputs
            var dataCreationStatus = chatDataCreationStatus.GetValueOrDefault(chatId, DataCreationStatus.None);
            await ContinueDataCreationAsync(botClient, chatId, messageText, dataCreationStatus, cancellationToken);
        }

    }
    private static async Task StartDataCreationAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        // Create a new Data instance for the chat
        var newData = new Data();
        // Set the initial data creation status
        chatDataInProgress[chatId] = newData;
        chatDataCreationStatus[chatId] = DataCreationStatus.WaitingDateTime;
        DateTime date = DateTime.Now;
        // Prompt the user to enter date and time
        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: $"Введіть дату та час (наприклад {date}):",
            cancellationToken: cancellationToken);
    }
    // Метод для продовження процесу створення нового об'єкту Data
    private static Dictionary<long, DataCreationStatus> chatDataCreationStatus = new Dictionary<long, DataCreationStatus>();
    private static Dictionary<long, Data> chatDataInProgress = new Dictionary<long, Data>();
    // ...
    private static async Task ContinueDataCreationAsync(ITelegramBotClient botClient, long chatId, string userInput, DataCreationStatus dataCreationStatus, CancellationToken cancellationToken)
    {
        if (!chatDataInProgress.TryGetValue(chatId, out var currentData))
        {
            return;
        }
        try
        {
            switch (dataCreationStatus)
            {
                case DataCreationStatus.WaitingInfo:
                    // Waiting for information input
                    currentData.Info = userInput;
                    chatDataInProgress[chatId] = currentData;
                    chatDataCreationStatus[chatId] = DataCreationStatus.WaitingTitle;
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Введіть назву:",
                        cancellationToken: cancellationToken);
                    break;
                case DataCreationStatus.WaitingDateTime:
                    // Waiting for date and time input
                    currentData.DateTime = DateTime.Parse(userInput);
                    chatDataInProgress[chatId] = currentData;
                    chatDataCreationStatus[chatId] = DataCreationStatus.WaitingInfo;
                    // Schedule a reminder for the specified date and time
                    ScheduleReminder(chatId, currentData.DateTime, currentData);
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Введіть опис:",
                        cancellationToken: cancellationToken);
                    break;             
                case DataCreationStatus.WaitingTitle:
                    // Waiting for title input
                    currentData.Title = userInput;
                    chatDataInProgress.Remove(chatId); // Remove the Data instance from the dictionary, as the creation process is complete
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: $"Створено нове нагадування:\n{currentData.DateTime}, {currentData.Info}, {currentData.Title}",
                        cancellationToken: cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Error handling (e.g., if the entered date or time is invalid)
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"Error: {ex.Message}\nPlease try again.",
                cancellationToken: cancellationToken);
        }
    }
    private static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }
    // private static Dictionary<long, DataCreationStatus> chatDataCreationStatus = new Dictionary<long, DataCreationStatus>();
    private static void ScheduleReminder(long chatId, DateTime reminderDateTime, Data data)
    {
        // Calculate the time difference between now and the reminderDateTime
        TimeSpan timeUntilReminder = reminderDateTime - DateTime.Now;
        // Create a Timer that triggers when it's time for the reminder
        Timer reminderTimer = new Timer(async _ =>
        {
            // Send a reminder message to the user
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"Нагадування: {data.Title}\n{data.Info}",
                cancellationToken: CancellationToken.None);

            // Remove the timer from the dictionary after it has been triggered
            chatReminders.Remove(chatId);
        }, null, timeUntilReminder, Timeout.InfiniteTimeSpan);

        // Add the timer to the dictionary for future reference
        chatReminders[chatId] = reminderTimer;
    }
    // Додайте статуси для керування процесом створення нового об'єкту Data
    public enum DataCreationStatus
    {
        None,        // Немає активного створення даних
        WaitingDateTime, // Очікування введення дати та часу
        WaitingInfo,     // Очікування введення інформації
        WaitingTitle     // Очікування введення заголовку
    }
}