using BarRaider.SdTools;

namespace XOutputRenew.StreamDeck;

class Program
{
    static void Main(string[] args)
    {
        // Connect to Stream Deck using StreamDeck-Tools
        SDWrapper.Run(args);
    }
}
