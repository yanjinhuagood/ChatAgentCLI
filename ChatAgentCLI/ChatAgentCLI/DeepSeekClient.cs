using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ChatCLI
{
    public class DeepSeekClient
    {
        private readonly HttpClient _http;
        private int _loadingRow = -1;
        private volatile bool _loadingActive;

        private readonly string _baseUrl;

        public DeepSeekClient(string apiKey = "", string baseUrl = "https://api.deepseek.com")
        {
            _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            _baseUrl = baseUrl.TrimEnd('/');
        }

        public async Task<string> AskStreamingWithLoadingAsync(string question)
        {
            var requestBody = new
            {
                model = "deepseek-chat",
                max_tokens = 4096,
                messages = new[]
                {
                    new { role = "user", content = question }
                },
                stream = true,
                stream_options = new { include_usage = false }
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions");
            request.Content = content;

            _loadingRow = Console.CursorTop;
            _loadingActive = true;
            var cts = new CancellationTokenSource();
            var loadingTask = Task.Run(() => LoadingLoop(cts.Token));

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var fullText = new StringBuilder();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream, Encoding.UTF8);

            string? line;
            bool inCodeBlock = false;
            bool bold = false;
            bool italic = false;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrEmpty(line)) continue;
                if (!line.StartsWith("data:")) continue;

                var data = line.Substring(5).Trim();
                if (data == "[DONE]") break;
                if (string.IsNullOrEmpty(data)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var choice = choices[0];
                        if (choice.TryGetProperty("delta", out var delta)
                            && delta.TryGetProperty("content", out var text))
                        {
                            var chunk = text.GetString();
                            if (!string.IsNullOrEmpty(chunk))
                            {
                                fullText.Append(chunk);

                                if (_loadingActive)
                                {
                                    _loadingActive = false;
                                    cts.Cancel();
                                    await loadingTask;
                                    Console.SetCursorPosition(0, _loadingRow);
                                    Console.Write(new string(' ', 60));
                                    Console.SetCursorPosition(0, _loadingRow);
                                    _loadingRow = -1;
                                }
                                for (int i = 0; i < chunk.Length; i++)
                                {
                                    if (i + 2 < chunk.Length && chunk.Substring(i, 3) == "```")
                                    {
                                        inCodeBlock = !inCodeBlock;
                                        Console.ResetColor();
                                        if (inCodeBlock)
                                        {
                                            i += 2;
                                            Console.ForegroundColor = ConsoleColor.Green;
                                            Console.BackgroundColor = ConsoleColor.Black;
                                        }
                                        else
                                        {
                                            Console.ResetColor();
                                        }
                                        continue;
                                    }

                                    if (inCodeBlock)
                                    {
                                        Console.ForegroundColor = ConsoleColor.Green;
                                        Console.Write(chunk[i]);
                                        continue;
                                    }

                                    if (i + 1 < chunk.Length && chunk[i] == '*' && chunk[i + 1] == '*')
                                    {
                                        bold = !bold;
                                        i++;
                                        Console.ForegroundColor = bold ? ConsoleColor.Yellow : Console.ForegroundColor;
                                        if (!bold && !inCodeBlock) Console.ResetColor();
                                        continue;
                                    }

                                    if (chunk[i] == '*' && !bold)
                                    {
                                        italic = !italic;
                                        Console.ForegroundColor = italic ? ConsoleColor.Cyan : Console.ForegroundColor;
                                        if (!italic && !inCodeBlock) Console.ResetColor();
                                        continue;
                                    }

                                    if (chunk[i] == '`' && !inCodeBlock)
                                    {
                                        italic = !italic;
                                        if (italic)
                                        {
                                            Console.ForegroundColor = ConsoleColor.White;
                                            Console.BackgroundColor = ConsoleColor.DarkGray;
                                        }
                                        else
                                        {
                                            Console.ResetColor();
                                        }
                                        continue;
                                    }

                                    if (chunk[i] == '#' && (i == 0 || chunk[i - 1] == '\n'))
                                    {
                                        Console.ForegroundColor = ConsoleColor.Magenta;
                                    }

                                    if (chunk[i] == '\n')
                                    {
                                        Console.ResetColor();
                                        bold = false;
                                        italic = false;
                                    }

                                    Console.Write(chunk[i]);
                                }
                                Console.Out.Flush();
                            }
                        }
                    }
                }
                catch { }
            }

            if (_loadingActive)
            {
                _loadingActive = false;
                cts.Cancel();
                await loadingTask;
            }

            Console.ResetColor();
            return fullText.ToString();
        }

        private void LoadingLoop(CancellationToken ct)
        {
            var spinner = new[] { '|', '/', '-', '\\' };
            var phrases = new[] { "思考中", "整理思路", "组织语言", "生成回复" };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int i = 0;

            while (!ct.IsCancellationRequested)
            {
                int pi = ((int)(sw.ElapsedMilliseconds / 3000)) % phrases.Length;
                int row = _loadingRow;
                if (row >= 0)
                {
                    try
                    {
                        Console.SetCursorPosition(0, row);
                        Console.Write($"  {phrases[pi]} {spinner[i % spinner.Length]}   ");
                    }
                    catch { }
                }
                i++;
                Thread.Sleep(150);
            }
        }
    }
}
