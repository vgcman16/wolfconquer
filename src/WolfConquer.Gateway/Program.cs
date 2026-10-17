using WolfConquer.Core.Runtime;

var endpoint = ConquerRuntimeDefaults.Gateway;
RuntimeConsoleLog.ConfigureFromEnvironment(endpoint.Role);

return await ConquerServerRunner.RunAsync(endpoint, args);
