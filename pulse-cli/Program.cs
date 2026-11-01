using System.Text;
using digital_wellbeing_app.Cli;

// `pulse` — reads what the Pulse app records. See CliApp for the commands.
Console.OutputEncoding = Encoding.UTF8;
var color = !Console.IsOutputRedirected && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
return new CliApp(Console.Out, Console.Error, color).Run(args);
