using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace YoutubeDownloader.Logic
{
    public class TelegramBotService : IHostedService, IDisposable
    {
        private ITelegramBotClient _botClient;
        private ReceiverOptions _receiverOptions;
        private DownloadManager _downloadManager;
        private ILogger<TelegramBotService> _logger;

        public TelegramBotService(ILogger<TelegramBotService> logger, DownloadManager downloadManager)
        {
            _downloadManager = downloadManager;
            _logger = logger;
            var tokenFile = Globals.Settings.TelegramBotTokenPath;
            var token = System.IO.File.ReadAllLines(tokenFile)[0];
            _botClient = new TelegramBotClient(token);
            var processMissingMessagesAfterRunBot = true;
            _receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[]
                {
                    UpdateType.Message,
                    UpdateType.CallbackQuery,
                },
                ThrowPendingUpdates = !processMissingMessagesAfterRunBot,
            };
        }


        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var cts = new CancellationTokenSource();
            _botClient.StartReceiving(UpdateHandler, ErrorHandler, _receiverOptions, cts.Token);

            var me = await _botClient.GetMeAsync();
            _logger.LogInformation($"{me.FirstName} {me.Username} запущен!");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"бот остановлен!");
            try
            {
                await _botClient.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ошибка остановки бота: " + ex.Message);
            }
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
                    var callbackQuery = update.CallbackQuery;
                    var chat = callbackQuery.Message.Chat;
                    await botClient.SendTextMessageAsync(chat.Id, $"Что то пошло не так");
                }
                catch
                {

                }
            }
        }

        private async Task CallBackProcess(ITelegramBotClient botClient, Update update)
        {
            var callbackQuery = update.CallbackQuery;
            var user = callbackQuery.From;

            _logger.LogInformation($"{user.FirstName} ({user.Id}) нажал на кнопку: {callbackQuery.Data}");

            var chat = callbackQuery.Message.Chat;

            if (callbackQuery.Data.StartsWith("Stream-"))
            {
                var splitData = callbackQuery.Data.Substring("Stream-".Length).Split("-");
                var downloadId = Guid.Parse(splitData[0]);
                var streamId = Int32.Parse(splitData[1]);

                var item = _downloadManager.Items.First(x => x.Id == downloadId);
                var stream = item.Streams.First(x => x.Id == streamId);
                if (stream.SizeMB > 50)
                {
                    await botClient.SendTextMessageAsync(chat.Id, $"Я не умею скачивать видео больше 50МБ,\r\nвоспользуйтесь http://downloads.bob217.ru?video={item.Url}");
                    return;
                }
                _downloadManager.SetStreamToDownload(downloadId, streamId, () => { SendVideoForUser(botClient, chat.Id, downloadId, streamId); });

                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
                var message = await botClient.SendTextMessageAsync(chat.Id,
                    $"Видео добавлено в очередь для загрузки, ожидайте");
            }
            else
            {
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
                await botClient.SendTextMessageAsync(chat.Id, $"Что то пошло не так (\\/)._.(\\/)\r\nпопробуйте попозже");
            }
        }
        private void SendVideoForUser(ITelegramBotClient botClient, long chatId, Guid downloadId, int streamId)
        {
            try
            {
                var item = _downloadManager.Items.FirstOrDefault(x => x.Id == downloadId);
                if (item == null)
                {
                    _logger.LogError("Фаил не найден");
                    return;
                }
                var videoStream = item.Streams.FirstOrDefault(x => x.Id == streamId);
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

                var videoName = item.Video.Title;
                var streamName = item.Streams.First(x => x.Id == streamId).Title;

                _logger.LogTrace("Отправка видева " + videoStream.FullPath);
                using (var fileStream = new FileStream(videoStream.FullPath, System.IO.FileMode.Open))
                {
                    var videoInputFile = InputFile.FromStream(fileStream);
                    var caption = $"<b>{videoName}</b>" +
                    $"\r\n<i>{streamName}</i>";
                    if (caption.Length > 1024)
                    {
                        caption = caption.Substring(0, 1024);
                    }
                    botClient.SendVideoAsync(chatId, videoInputFile, //replyToMessageId: message.MessageId, 
                        caption: caption, parseMode: ParseMode.Html).GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка отправки видео: " + ex.Message);
                try
                {
                    botClient.SendTextMessageAsync(chatId, $"Что то пошло не так (\\/)._.(\\/) очень жаль").GetAwaiter().GetResult();
                }
                catch
                {

                }
            }
        }

        private object lockObj = new object();
        private async Task MessageProcess(ITelegramBotClient botClient, Update update)
        {
            var message = update.Message;
            var user = message.From;
            _logger.LogInformation($"{user.FirstName} ({user.Id}) написал сообщение: {message.Text}");
            var chat = message.Chat;

            switch (message.Type)
            {
                case MessageType.Text:
                    {

                        try
                        {
                            if (message.Text == "/start")
                            {
                                await botClient.SendTextMessageAsync(
                                    chat.Id,
                                    "Здравствуйте,\r\nприсылайте ссылки на ютуб видосы и я помогу вам их скачать,\r\nесли они не более 50МБ (\\/)._.(\\/)");
                            }
                            else if (message.Text?.ToLower() == "розыгрыш")
                            {
                                var user1 = user.Id + " " + user.Username;
                                var ifExists = false;
                                lock (lockObj)
                                {
                                    var users = System.IO.File.ReadAllLines("E:\\publish\\bob217downloads\\production\\priz\\users.txt");
                                    var key = user.Id.ToString();
                                    if (users.Any(x => x.StartsWith(key)))
                                    {
                                        ifExists = true;
                                    }
                                    else
                                    {
                                        var user2 = users.ToList();
                                        user2.Add(user1);
                                        System.IO.File.WriteAllLines("E:\\publish\\bob217downloads\\production\\priz\\users.txt", user2);
                                    }
                                }

                                if (ifExists)
                                {
                                    await botClient.SendTextMessageAsync(
                                        chat.Id,
                                        "Вы уже в списке участников");
                                }
                                else
                                {
                                    await botClient.SendTextMessageAsync(
                                        chat.Id,
                                        "Ваша заявка для розыгрыша принята");
                                }
                            }
                            else
                            {
                                var item = await _downloadManager.AddToQueueAsync(message.Text);

                                var buttons = new List<InlineKeyboardButton[]>();
                                foreach (var stream in item.Streams)
                                {
                                    var asd = new InlineKeyboardButton[]
                                        {
                                        InlineKeyboardButton.WithCallbackData(stream.Title, "Stream-" + item.Id.ToString("N") + "-" + stream.Id),
                                        };
                                    buttons.Add(asd);
                                }
                                var inlineKeyboard = new InlineKeyboardMarkup(buttons);
                                await botClient.SendTextMessageAsync(
                                    chat.Id,
                                    $"Выберите фортат для скачивания\r\n<b>{item.Video.Title}</b>",
                                    parseMode: ParseMode.Html,
                                    replyMarkup: inlineKeyboard);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex.ToString());
                            await botClient.SendTextMessageAsync(
                                chat.Id,
                                "Что-то пошло не так, попробуйте позже. убедитесь что ссылка на видео ютуба коректная");
                        }
                        return;
                    }

                // Добавил default , чтобы показать вам разницу типов Message
                default:
                    {
                        await botClient.SendTextMessageAsync(
                            chat.Id,
                            "Используй только текст!");
                        return;
                    }
            }
        }

        private Task ErrorHandler(ITelegramBotClient botClient, Exception error, CancellationToken cancellationToken)
        {
            var ErrorMessage = error switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => error.ToString()
            };

            _logger.LogError(ErrorMessage);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
