using ChatCLI;
using System.Text;

PrintWPFLogo();
Console.WriteLine("欢迎使用 WPF AI 助手！输入 'exit' 或 'quit' 退出程序\n");
dynamic chatClient = new DeepSeekClient(apiKey: "", baseUrl: "https://api.deepseek.com");
while (true)
{
    PrintSeparator();
    Console.Write(" ");

    var inputSb = new StringBuilder();

    int cursorTop = Console.CursorTop;
    int minNeeded = Math.Max(cursorTop, 0) + 3;
    if (minNeeded >= Console.BufferHeight)
    {
        Console.SetBufferSize(Console.BufferWidth, minNeeded + 1);
    }

    int inputRow = Math.Min(Console.CursorTop, Console.BufferHeight - 3);
    int bottomRow = inputRow + 1;

    Console.SetCursorPosition(0, bottomRow);
    PrintSeparator();
    Console.SetCursorPosition(1, inputRow);

    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            break;
        }
        if (key.Key == ConsoleKey.Backspace)
        {
            if (inputSb.Length > 0)
            {
                inputSb.Remove(inputSb.Length - 1, 1);
                Console.SetCursorPosition(0, inputRow);
                Console.Write(new string(' ', 60));
                Console.SetCursorPosition(1, inputRow);
                Console.Write(inputSb.ToString());
                int displayWidth = 1;
                foreach (char c in inputSb.ToString())
                    displayWidth += char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.OtherLetter ? 2 : 1;
                Console.SetCursorPosition(displayWidth, inputRow);
            }
            continue;
        }
        if (!char.IsControl(key.KeyChar))
        {
            inputSb.Append(key.KeyChar);
            Console.Write(key.KeyChar);
            continue;
        }
    }

    EnsureRowExists(bottomRow + 1);
    Console.SetCursorPosition(0, bottomRow + 1);
    Console.WriteLine();

    string userInput = inputSb.ToString();

    if (string.IsNullOrEmpty(userInput) ||
        userInput.ToLower() == "exit" ||
        userInput.ToLower() == "quit")
    {
        PrintSeparator();
        Console.WriteLine("  再见！");
        break;
    }

    await chatClient.AskStreamingWithLoadingAsync(userInput);
    Console.WriteLine();
}

void PrintSeparator()
{
    int w = 60;
    Console.WriteLine(new string('─', w));
}

void PrintWPFLogo()
{
    string logo = @"

 ____      ____  _______  ________        _                              _
|_  _|    |_  _||_   __ \|_   __  |      / \                            / |_
  \ \  /\  / /    | |__) | | |_ \_|     / _ \     .--./) .---.  _ .--. `| |-'
   \ \/  \/ /     |  ___/  |  _|       / ___ \   / /'`\;/ /__\\[ `.-. | | |
    \  /\  /     _| |_    _| |_      _/ /   \ \_ \ \._//| \__., | | | | | |,
     \/  \/     |_____|  |_____|    |____| |____|.',__`  '.__.'[___||__]\__/
                                                ( ( __))                      ";

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine(logo);
    Console.ResetColor();
}

void EnsureRowExists(int row)
{
    if (row >= Console.BufferHeight)
    {
        int newHeight = Math.Max(row + 1, Console.BufferHeight + 10);
        Console.SetBufferSize(Console.BufferWidth, newHeight);
    }
}
