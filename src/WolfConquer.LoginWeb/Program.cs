using WolfConquer.Core.Runtime;
using WolfConquer.Core.Web;

RuntimeConsoleLog.ConfigureFromEnvironment("LoginWeb");
return await LoginLoaderWebServerRunner.RunAsync(args);
