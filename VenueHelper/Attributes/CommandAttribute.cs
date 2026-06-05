using System;

namespace VenueHelper.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class CommandAttribute : Attribute
{
    public string Command { get; }
    public CommandAttribute(string command) => Command = command;
}
