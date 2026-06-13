using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Blurt.Core;

namespace Blurt.App;

/// <summary>
/// The first-run teaching tutorial (issue 40): an animated coach shown right after
/// setup and replayable any time from the tray ("How to use Blurt…"). Walks the
/// <see cref="TutorialCard"/>s — push-to-talk, the three triggers, the Flex
/// tap/hold modes, the live-status pill — each illustrated by the <em>real</em>
/// status pill driven from Core's <see cref="TutorialDemo"/> cues, so what a
/// newcomer learns is exactly what they will see. The card order and copy are pure
/// Core (<see cref="Tutorial"/>); this is the thin, manually-verified WPF shell.
/// Skippable, and never shown to a returning user unless they open it. Shown
/// modally on the WinForms STA thread like the onboarding/overlay windows — no
/// second <see cref="System.Windows.Application"/> is created.
/// </summary>
internal partial class TutorialWindow : Window
{
    // One step of the demo stage: a pill label + dot colour. "Active" frames are
    // live activities (listening/transcribing/…) that pulse and animate an ellipsis
    // like the real overlay (issue 33); inactive frames are steady mode flashes.
    private sealed record DemoFrame(string Label, byte R, byte G, byte B, bool Active);

    // Advances the stage through the current card's frames, looping so the
    // illustration keeps playing while the user reads. A separate, faster timer
    // animates the ellipsis for active frames — mirroring the overlay.
    private readonly DispatcherTimer _frameTimer =
        new() { Interval = TimeSpan.FromMilliseconds(1100) };
    private readonly DispatcherTimer _ellipsisTimer =
        new() { Interval = TimeSpan.FromMilliseconds(350) };

    private static readonly DoubleAnimation PulseAnimation = new()
    {
        From = 1.0,
        To = 0.3,
        Duration = TimeSpan.FromMilliseconds(750),
        AutoReverse = true,
        RepeatBehavior = RepeatBehavior.Forever,
    };

    private IReadOnlyList<DemoFrame> _frames = Array.Empty<DemoFrame>();
    private int _frameIndex;
    private int _cardIndex;
    private string _baseLabel = "";
    private int _ellipsisCount;

    public TutorialWindow()
    {
        // Before InitializeComponent so the pill's DynamicResource brushes resolve
        // from the shared theme (issue 19).
        ThemeManager.Apply(this);
        InitializeComponent();

        _frameTimer.Tick += (_, _) => AdvanceFrame();
        _ellipsisTimer.Tick += (_, _) =>
        {
            // Cycle 1→2→3 dots, padded to a constant width so the pill doesn't
            // resize on each tick (same trick as the overlay).
            _ellipsisCount = _ellipsisCount % 3 + 1;
            DemoText.Text = _baseLabel + new string('.', _ellipsisCount) + new string(' ', 3 - _ellipsisCount);
        };

        ShowCard(0);
    }

    private void ShowCard(int index)
    {
        _cardIndex = Math.Clamp(index, 0, Tutorial.Cards.Count - 1);
        var card = Tutorial.Cards[_cardIndex];

        StepIndicator.Text = $"Lesson {_cardIndex + 1} of {Tutorial.Cards.Count}";
        CardTitleText.Text = Tutorial.Title(card);
        CardBodyText.Text = Tutorial.Body(card);

        BackButton.IsEnabled = _cardIndex > 0;
        NextButton.Content = _cardIndex == Tutorial.Cards.Count - 1 ? "Got it" : "Next";

        StartDemo(card);
    }

    private void StartDemo(TutorialCard card)
    {
        _frames = FramesFor(card);
        _frameIndex = 0;
        _frameTimer.Stop();

        ShowFrame(_frames[0]);

        // A single-frame card (push-to-talk) needs no stepping — the pulse alone
        // carries it; only loop when there's more than one frame to cycle.
        if (_frames.Count > 1)
            _frameTimer.Start();
    }

    private void AdvanceFrame()
    {
        if (_frames.Count == 0)
            return;

        _frameIndex = (_frameIndex + 1) % _frames.Count;
        ShowFrame(_frames[_frameIndex]);
    }

    private void ShowFrame(DemoFrame f)
    {
        DemoDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(f.R, f.G, f.B));

        if (f.Active)
        {
            _baseLabel = f.Label;
            _ellipsisCount = 0;
            DemoText.Text = f.Label + new string(' ', 3);   // reserve the ellipsis width
            if (!_ellipsisTimer.IsEnabled)
                _ellipsisTimer.Start();
            DemoDot.BeginAnimation(OpacityProperty, PulseAnimation);
        }
        else
        {
            _ellipsisTimer.Stop();
            DemoDot.BeginAnimation(OpacityProperty, null);
            DemoDot.Opacity = 1.0;
            DemoText.Text = f.Label;
        }
    }

    // Build a card's demo frames from Core's pure cues, so the pill shows exactly
    // the real modes/verbs. Flex → the shipped mode cycle (steady flashes, each its
    // own colour); push-to-talk → just the "listening" pulse (the gesture);
    // everything else → the real live-status phases (pulsing, animated ellipsis).
    private static IReadOnlyList<DemoFrame> FramesFor(TutorialCard card)
    {
        switch (card)
        {
            case TutorialCard.FlexModes:
                return TutorialDemo.ModeFlashes
                    .Select(m =>
                    {
                        var (r, g, b) = FlexSlotOverlay.Dot(m);
                        return new DemoFrame(FlexSlotOverlay.Label(m), r, g, b, Active: false);
                    })
                    .ToList();

            case TutorialCard.PushToTalk:
            {
                var (r, g, b) = TrayPalette.For(TrayState.Recording);
                return new[] { new DemoFrame(StatusLabel.Listening, r, g, b, Active: true) };
            }

            default:   // Triggers, LiveStatus, TryIt — the genuine phase sequence
                return TutorialDemo.StatusFrames
                    .Select(label =>
                    {
                        // Listening is the recording phase (red); the rest are
                        // processing (amber) — the overlay's own colour language.
                        var state = label == StatusLabel.Listening
                            ? TrayState.Recording
                            : TrayState.Processing;
                        var (r, g, b) = TrayPalette.For(state);
                        return new DemoFrame(label, r, g, b, Active: true);
                    })
                    .ToList();
        }
    }

    private void OnBack(object sender, RoutedEventArgs e) => ShowCard(_cardIndex - 1);

    private void OnNext(object sender, RoutedEventArgs e)
    {
        if (_cardIndex >= Tutorial.Cards.Count - 1)
        {
            Close();
            return;
        }

        ShowCard(_cardIndex + 1);
    }

    private void OnSkip(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _frameTimer.Stop();
        _ellipsisTimer.Stop();
        base.OnClosed(e);
    }
}
