using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Text.RegularExpressions;
using System.Text;
using CsvHelper;
using System.Globalization;
using System.IO;

class Program
{
    static async Task Main()
    {
        var botClient = new TelegramBotClient("7287917776:AAFKd8x1WY1JfnmG0POE4gm-iFAL28ir9FY");
        var cts = new CancellationTokenSource();

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            new ReceiverOptions { AllowedUpdates = { } },
            cts.Token
        );

        var me = await botClient.GetMe();
        Console.WriteLine($"🤖 Bot @{me.Username} đã khởi động...");
        Console.ReadLine();
        cts.Cancel();
    }

    static string EscapeMarkdown(string text)
    {
        var toEscape = new[] { "_", "*", "[", "]", "(", ")", "~", "`", ">", "#", "+", "-", "=", "|", "{", "}", ".", "!" };
        foreach (var c in toEscape)
            text = text.Replace(c, "\\" + c);
        return text;
    }

    static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken token)
    {
        if (update.Message?.Text is not string text) return;
        var chatId = update.Message.Chat.Id;
        text = text.Trim();

        if (!IsValidMd5(text))
        {
            await bot.SendMessage(chatId,
                EscapeMarkdown("⚠️ Lỗi: Chuỗi gửi không phải MD5 hợp lệ \\(32 ký tự hex từ 0-9, a-f\\)\\."),
                parseMode: ParseMode.MarkdownV2);
            return;
        }

        var result = AnalyzeMd5Pro(text);
        var (d1, d2, d3, total) = SimulateDice(result.Result);

        string message =$@"
🎯 <b>Dự đoán Tài/Xỉu từ MD5</b>

🔐 <b>MD5:</b> <code>{text}</code>

🎲 <b>Xúc xắc:</b> {d1}, {d2}, {d3} → <b>Tổng:</b> {total}
🎰 <b>Mô phỏng xúc xắc:</b> {GetDiceEmoji(d1)} {GetDiceEmoji(d2)} {GetDiceEmoji(d3)}

📈 <b>Dự đoán:</b> {result.Result.ToUpper()}
🏷️ <b>Độ tin cậy:</b> {result.Confidence} ({(result.Probability * 100):0.#}%)

⚙️ <b>Phân tích thêm:</b> {result.MethodUsed}

💡 <b>Gợi ý:</b> {SuggestBet(result.Probability)}

🕰️ <i>{DateTime.UtcNow:HH:mm:ss dd/MM/yyyy}</i>

👨‍💻 <b>Dev:</b> Ngô Đức Duy
📡 <b>Admin:</b> https://www.facebook.com/profile.php?id=100073200452769&locale=vi_VN
💡 <b>Cre:</b> duyemcubi188
";

        await bot.SendMessage(chatId, message, parseMode: ParseMode.Html);

        SaveHistory(text, result);
    }



    static Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken token)
    {
        Console.WriteLine($"❌ Lỗi: {ex.Message}");
        return Task.CompletedTask;
    }

    static bool IsValidMd5(string input) => Regex.IsMatch(input, @"^[0-9a-fA-F]{32}$");

    static (string Result, double Probability, string Confidence, string MethodUsed) AnalyzeMd5Pro(string md5)
    {
        var bytes = Enumerable.Range(0, 16)
            .Select(i => Convert.ToInt32(md5.Substring(i * 2, 2), 16))
            .ToArray();

        double avg = bytes.Average();
        double stdDev = Math.Sqrt(bytes.Select(b => Math.Pow(b - avg, 2)).Average());
        int highNibbleCount = md5.Count(c => "89abcdef".Contains(char.ToLower(c)));
        double entropy = (double)highNibbleCount / 32;
        int fluctuation = Enumerable.Range(0, bytes.Length - 1).Sum(i => Math.Abs(bytes[i + 1] - bytes[i]));
        ushort crc = Crc16(bytes);

        int biasSmall = md5.Count(c => "01234567".Contains(c));
        int biasLarge = md5.Count(c => "89abcdef".Contains(c));
        double bias = (double)Math.Abs(biasSmall - biasLarge) / 32;

        int repetition = md5.GroupBy(c => c).Max(g => g.Count());

        int smartScore = 0;
        if (entropy > 0.58) smartScore += 2;
        if (stdDev > 55) smartScore += 1;
        if (bias < 0.2) smartScore += 1;
        if (repetition < 5) smartScore += 1;
        if (crc % 2 == 0) smartScore += 1;
        if (fluctuation > 700) smartScore += 1;

        double prob = smartScore switch
        {
            >= 7 => 0.95,
            6 => 0.9,
            5 => 0.85,
            4 => 0.8,
            3 => 0.7,
            2 => 0.6,
            _ => 0.5
        };

        string result = prob >= 0.55 ? "Tài" : "Xỉu";
        string confidence = prob switch
        {
            >= 0.9 => "Very High 🔥",
            >= 0.8 => "High 💪",
            >= 0.7 => "Medium 🧠",
            _ => "Low 🫣"
        };

        string method = $"Entropy={entropy:0.###} | StdDev={stdDev:0.#} | Bias={bias:0.##} | Fluct={fluctuation} | CRC16={crc}";

        return (result, prob, confidence, method);
    }

    static (int, int, int, int) SimulateDice(string result)
    {
        Random rnd = new();
        int min = result == "Tài" ? 11 : 3;
        int max = result == "Tài" ? 18 : 10;

        int d1, d2, d3, total;
        do
        {
            d1 = rnd.Next(1, 7);
            d2 = rnd.Next(1, 7);
            d3 = rnd.Next(1, 7);
            total = d1 + d2 + d3;
        } while (total < min || total > max);

        return (d1, d2, d3, total);
    }

    static string GetDiceEmoji(int num) => num switch
    {
        1 => "1️⃣",
        2 => "2️⃣",
        3 => "3️⃣",
        4 => "4️⃣",
        5 => "5️⃣",
        6 => "6️⃣",
        _ => "🎲"
    };

    static string SuggestBet(double prob) => prob switch
    {
        >= 0.9 => "Cược mạnh 🔥",
        >= 0.8 => "Cược mạnh 💪",
        >= 0.7 => "Cược nhẹ 🧠",
        _ => "Không cược 🚫"
    };

    static ushort Crc16(int[] bytes)
    {
        ushort crc = 0xFFFF;
        foreach (var b in bytes)
        {
            crc ^= (ushort)(b << 8);
            for (int i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }

    static void SaveHistory(string md5, (string Result, double Probability, string Confidence, string MethodUsed) result)
    {
        string filePath = "history.csv";
        bool fileExists = File.Exists(filePath);

        using var writer = new StreamWriter(filePath, append: true);
        using var csv = new CsvHelper.CsvWriter(writer, CultureInfo.InvariantCulture);

        if (!fileExists)
            csv.WriteHeader<HistoryEntry>();

        csv.NextRecord();
        csv.WriteRecord(new HistoryEntry
        {
            Time = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            Md5 = md5,
            Result = result.Result,
            Confidence = result.Confidence,
            Probability = $"{(result.Probability * 100):0.#}%",
            Details = result.MethodUsed
        });
    }

    class HistoryEntry
    {
        public string Time { get; set; }
        public string Md5 { get; set; }
        public string Result { get; set; }
        public string Confidence { get; set; }
        public string Probability { get; set; }
        public string Details { get; set; }
    }
}
