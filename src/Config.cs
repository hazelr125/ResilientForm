namespace ResilientForm;

public class Config
{
    public string Url { get; set; } = Environment.GetEnvironmentVariable("FORM_URL")
                                      ?? "http://app.cloudqa.io/home/AutomationPracticeForm";
    public string FirstName { get; set; } = "Hazel";
    public string Gender { get; set; } = "Female";
    public string? State { get; set; }          // null = pick the first real option
    public bool Headless { get; set; }
    public int TimeoutSeconds { get; set; } = 15;
    public string? ScreenshotPath { get; set; }
    public bool Help { get; set; }

    public static Config Parse(string[] args)
    {
        var c = new Config();
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Missing value for {args[i]}");
            switch (args[i])
            {
                case "--url": c.Url = Next(); break;
                case "--first-name": c.FirstName = Next(); break;
                case "--gender": c.Gender = Next(); break;
                case "--state": c.State = Next(); break;
                case "--timeout": c.TimeoutSeconds = int.Parse(Next()); break;
                case "--screenshot": c.ScreenshotPath = Next(); break;
                case "--headless": c.Headless = true; break;
                case "-h": case "--help": c.Help = true; break;
                default: throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }
        return c;
    }

    public static void PrintHelp() => Console.WriteLine("""
        Resilient form tester

          --url <url>          Page to test (also env FORM_URL). http(s):// or file:// 
          --first-name <text>  Value for First Name        (default: Hazel)
          --gender <text>      Option to pick: Male/Female (default: Female)
          --state <text>       State option to pick        (default: first real option)
          --timeout <sec>      Max wait to find a field    (default: 15)
          --screenshot <file>  Save a screenshot at the end
          --headless           Run Chrome without a window
        """);
}
