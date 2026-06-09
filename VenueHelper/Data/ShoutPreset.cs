using VenueHelper.Logic;

namespace VenueHelper.Data;

// One preset line for the Shout/Yell Helper: which channel to post to and the
// message text. Hosts can pre-fill several and fire them with one click.
[Serializable]
public class ShoutPreset
{
    public ChatChannel Channel = ChatChannel.Yell;
    public string Message = string.Empty;
}
