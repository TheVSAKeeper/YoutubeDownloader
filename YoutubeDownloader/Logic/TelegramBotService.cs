using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using File = System.IO.File;

namespace YoutubeDownloader.Logic;

public class TelegramBotService : IHostedService, IDisposable
{
    private readonly DownloadManager _downloadManager;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly ReceiverOptions _receiverOptions;

    public TelegramBotService(ILogger<TelegramBotService> logger, DownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
        _logger = logger;
        string tokenFile = Globals.Settings.TelegramBotTokenPath;
        string token = File.ReadAllLines(tokenFile)[0];
        _botClient = new TelegramBotClient(token);
        bool processMissingMessagesAfterRunBot = true;

        _receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[]
            {
                UpdateType.Message,
                UpdateType.CallbackQuery
            },
            ThrowPendingUpdates = !processMissingMessagesAfterRunBot
        };
    }

    public void Dispose()
    {
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource cts = new();
        _botClient.StartReceiving(UpdateHandler, ErrorHandler, _receiverOptions, cts.Token);

        User me = await _botClient.GetMeAsync(cancellationToken: cts.Token);
        _logger.LogInformation($"{me.FirstName} {me.Username} запущен!");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("бот остановлен!");
        await _botClient.CloseAsync();
    }

    private async Task UpdateHandler(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                {
                    await MessageProcess(botClient, update);
                    return;
                }

                case UpdateType.CallbackQuery:
                {
                    await CallBackProcess(botClient, update);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.ToString());

            try
            {
                CallbackQuery? callbackQuery = update.CallbackQuery;
                Chat chat = callbackQuery.Message.Chat;
                await botClient.SendTextMessageAsync(chat.Id, "Что то пошло не так");
            }
            catch
            {
            }
        }
    }

    private async Task CallBackProcess(ITelegramBotClient botClient, Update update)
    {
        CallbackQuery? callbackQuery = update.CallbackQuery;
        User user = callbackQuery.From;

        _logger.LogInformation($"{user.FirstName} ({user.Id}) нажал на кнопку: {callbackQuery.Data}");

        Chat chat = callbackQuery.Message.Chat;

        if (callbackQuery.Data.StartsWith("Stream-"))
        {
            string[] splitData = callbackQuery.Data.Substring("Stream-".Length).Split("-");
            Guid downloadId = Guid.Parse(splitData[0]);
            int streamId = int.Parse(splitData[1]);

            DownloadManager.DownloadItem item = _downloadManager.Items.First(x => x.Id == downloadId);
            DownloadManager.DownloadItemSteam stream = item.Streams.First(x => x.Id == streamId);

            if (stream.SizeMb > 50)
            {
                await botClient.SendTextMessageAsync(chat.Id, $"Я не умею скачивать видео больше 50МБ,\r\nвоспользуйтесь http://downloads.bob217.ru?video={item.Url}");
                return;
            }

            _downloadManager.SetStreamToDownload(downloadId, streamId, () => { SendVideoForUser(botClient, chat.Id, downloadId, streamId); });

            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);

            Message message = await botClient.SendTextMessageAsync(chat.Id,
                "Видео добавлено в очередь для загрузки, ожидайте");
        }
        else
        {
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
            await botClient.SendTextMessageAsync(chat.Id, "Что то пошло не так (\\/)._.(\\/)\r\nпопробуйте попозже");
        }
    }

    private void SendVideoForUser(ITelegramBotClient botClient, long chatId, Guid downloadId, int streamId)
    {
        try
        {
            DownloadManager.DownloadItem? item = _downloadManager.Items.FirstOrDefault(x => x.Id == downloadId);

            if (item == null)
            {
                _logger.LogError("Фаил не найден");
                return;
            }

            DownloadManager.DownloadItemSteam? videoStream = item.Streams.FirstOrDefault(x => x.Id == streamId);

            if (videoStream == null)
            {
                _logger.LogError("Поток не найден");
                return;
            }

            if (videoStream.State != DownloadItemState.Ready)
            {
                _logger.LogError("Состояние не готово. Текущие");
                return;
            }

            string videoName = item.Video.Title;
            string streamName = item.Streams.First(x => x.Id == streamId).Title;

            _logger.LogTrace("Отправка видева " + videoStream.FullPath);

            using (FileStream fileStream = new(videoStream.FullPath, FileMode.Open))
            {
                InputFileStream videoInputFile = InputFile.FromStream(fileStream);
                string caption = $"<b>{videoName}</b>" + $"\r\n<i>{streamName}</i>";

                if (caption.Length > 1024)
                {
                    caption = caption.Substring(0, 1024);
                }

                botClient.SendVideoAsync(chatId, videoInputFile, //replyToMessageId: message.MessageId, 
                        caption: caption, parseMode: ParseMode.Html)
                    .GetAwaiter()
                    .GetResult();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки видео: " + ex.Message);

            try
            {
                botClient.SendTextMessageAsync(chatId, "Что то пошло не так (\\/)._.(\\/) очень жаль").GetAwaiter().GetResult();
            }
            catch
            {
            }
        }
    }

    private async Task MessageProcess(ITelegramBotClient botClient, Update update)
    {
        Message? message = update.Message;
        User? user = message.From;
        Console.WriteLine($"{user.FirstName} ({user.Id}) написал сообщение: {message.Text}");
        Chat chat = message.Chat;

        switch (message.Type)
        {
            case MessageType.Text:
            {
                try
                {
                    if (message.Text == "/start")
                    {
                        await botClient.SendTextMessageAsync(chat.Id,
                            "Здравствуйте,\r\nприсылайте ссылки на ютуб видосы и я помогу вам их скачать,\r\nесли они не более 50МБ (\\/)._.(\\/)");
                    }
                    else
                    {
                        DownloadManager.DownloadItem item = await _downloadManager.AddToQueueAsync(message.Text);

                        List<InlineKeyboardButton[]> buttons = new();

                        foreach (DownloadManager.DownloadItemSteam stream in item.Streams)
                        {
                            InlineKeyboardButton[] asd =
                            {
                                InlineKeyboardButton.WithCallbackData(stream.Title, "Stream-" + item.Id.ToString("N") + "-" + stream.Id)
                            };

                            buttons.Add(asd);
                        }

                        InlineKeyboardMarkup inlineKeyboard = new(buttons);

                        await botClient.SendTextMessageAsync(chat.Id,
                            $"Выберите фортат для скачивания\r\n<b>{item.Video.Title}</b>",
                            parseMode: ParseMode.Html,
                            replyMarkup: inlineKeyboard);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());

                    await botClient.SendTextMessageAsync(chat.Id,
                        "Что-то пошло не так, попробуйте позже. убедитесь что ссылка на видео ютуба коректная");
                }

                return;
            }

            // Добавил default , чтобы показать вам разницу типов Message
            default:
            {
                await botClient.SendTextMessageAsync(chat.Id,
                    "Используй только текст!");

                return;
            }
        }
    }

    private Task ErrorHandler(ITelegramBotClient botClient, Exception error, CancellationToken cancellationToken)
    {
        string ErrorMessage = error switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => error.ToString()
        };

        _logger.LogError(ErrorMessage);
        return Task.CompletedTask;
    }
}