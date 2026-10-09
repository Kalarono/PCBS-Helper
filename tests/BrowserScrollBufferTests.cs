using System;
using PCBSCalculatorHelper;

internal static class BrowserScrollBufferTests
{
    private static int checks;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }
    private static void Main()
    {
        BrowserScrollBuffer scroll = new BrowserScrollBuffer();
        scroll.Advance(true);
        Check(scroll.X == 0 && scroll.Y == 0, "An input snapshot without events does not scroll.");
        scroll.Enqueue(0, 3);
        Check(scroll.Y == 0, "An event after Browser.Update waits for the next snapshot.");
        scroll.Advance(true);
        Check(scroll.X == 0 && scroll.Y == -3, "Scrolling down reaches the browser with the correct sign.");
        Check(scroll.Y * 40 == -120, "One normal IMGUI wheel notch produces 120 browser scroll units.");
        Check(scroll.Y == -3, "Repeated property reads preserve the same input snapshot.");
        scroll.Advance(true);
        Check(scroll.Y == 0, "The next snapshot cannot replay an already delivered event.");
        scroll.Enqueue(0, -3); scroll.Advance(true);
        Check(scroll.Y == 3, "Scrolling up reaches the browser with the correct sign.");
        scroll.Enqueue(0, 3); scroll.Enqueue(0, 3); scroll.Advance(true);
        Check(scroll.Y == -6, "Two events between browser updates are accumulated without losing one.");
        scroll.Enqueue(3, 0); scroll.Advance(true);
        Check(scroll.X == -3 && scroll.Y == 0, "Horizontal wheel input reaches result tables.");
        scroll.Enqueue(1, -2); scroll.Advance(true);
        Check(scroll.X == -1 && scroll.Y == 2, "Both scroll axes are preserved.");
        scroll.Enqueue(0, 0.25f); scroll.Advance(true);
        Check(scroll.Y == -0.25f, "Small touchpad deltas are not rounded away.");
        scroll.Enqueue(0, 3); scroll.Advance(false);
        Check(scroll.X == 0 && scroll.Y == 0, "Focus loss drops pending scroll input.");
        scroll.Advance(true);
        Check(scroll.Y == 0, "Returning focus cannot replay background scroll input.");
        scroll.Enqueue(0, 3); scroll.Clear(); scroll.Advance(true);
        Check(scroll.Y == 0, "Closing or resizing clears queued events.");
        scroll.Enqueue(0, 3); scroll.Advance(true); scroll.Clear();
        Check(scroll.X == 0 && scroll.Y == 0, "Closing clears the current input snapshot too.");
        scroll.Enqueue(0, 3); scroll.Enqueue(0, -3); scroll.Advance(true);
        Check(scroll.Y == 0, "Opposite deltas cancel instead of leaving a stale direction.");
        for (int i = 0; i < 30; i++)
        {
            scroll.Enqueue(0, 3); scroll.Advance(true);
            Check(scroll.Y == -3, "Continuous wheel events are delivered on every browser update.");
            scroll.Advance(true);
            Check(scroll.Y == 0, "Idle frames between wheel events do not duplicate movement.");
        }
        Console.WriteLine("Browser scroll buffer: " + checks + " assertions passed.");
    }
}
