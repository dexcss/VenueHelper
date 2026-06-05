using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using VenueHelper.Attributes;

namespace VenueHelper;

public class PluginCommandManager<THost> : IDisposable
{
    private readonly ICommandManager commandManager;
    private readonly (string, CommandInfo)[] pluginCommands;
    private readonly THost host;

    public PluginCommandManager(THost host, ICommandManager commandManager)
    {
        this.commandManager = commandManager;
        this.host = host;

        this.pluginCommands = host!.GetType()
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Where(method => method.GetCustomAttribute<CommandAttribute>() != null)
            .SelectMany(GetCommandInfoTuple)
            .ToArray();

        AddCommandHandlers();
    }

    private void AddCommandHandlers()
    {
        foreach (var (command, commandInfo) in this.pluginCommands)
            this.commandManager.AddHandler(command, commandInfo);
    }

    private void RemoveCommandHandlers()
    {
        foreach (var (command, _) in this.pluginCommands)
            this.commandManager.RemoveHandler(command);
    }

    private IEnumerable<(string, CommandInfo)> GetCommandInfoTuple(MethodInfo method)
    {
        var handlerDelegate = (IReadOnlyCommandInfo.HandlerDelegate)Delegate.CreateDelegate(
            typeof(IReadOnlyCommandInfo.HandlerDelegate), this.host, method);

        var command = handlerDelegate.Method.GetCustomAttribute<CommandAttribute>();
        var aliases = handlerDelegate.Method.GetCustomAttribute<AliasesAttribute>();
        var helpMessage = handlerDelegate.Method.GetCustomAttribute<HelpMessageAttribute>();

        var commandInfo = new CommandInfo(handlerDelegate)
        {
            HelpMessage = helpMessage?.HelpMessage ?? string.Empty,
            ShowInHelp = helpMessage != null,
        };

        var commandInfoTuples = new List<(string, CommandInfo)> { (command!.Command, commandInfo) };
        if (aliases != null)
            foreach (var alias in aliases.Aliases)
                commandInfoTuples.Add((alias, commandInfo));

        return commandInfoTuples;
    }

    public void Dispose()
    {
        RemoveCommandHandlers();
        GC.SuppressFinalize(this);
    }
}
