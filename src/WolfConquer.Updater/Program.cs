using WolfConquer.Updater;

using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) => { args.Cancel = true; stop.Cancel(); };
return await UpdaterApplication.RunAsync(args, Console.Out, Console.Error, stop.Token);
