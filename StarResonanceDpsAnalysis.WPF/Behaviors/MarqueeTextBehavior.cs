using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Microsoft.Xaml.Behaviors;

namespace StarResonanceDpsAnalysis.WPF.Behaviors;

public class MarqueeTextBehavior : Behavior<FrameworkElement>
{
    public static readonly DependencyProperty ContainerProperty =
        DependencyProperty.Register("Container", typeof(FrameworkElement), typeof(MarqueeTextBehavior), new PropertyMetadata(null, OnContainerChanged));

    public FrameworkElement Container
    {
        get { return (FrameworkElement)GetValue(ContainerProperty); }
        set { SetValue(ContainerProperty, value); }
    }

    public static readonly DependencyProperty IsAnimationEnabledProperty =
        DependencyProperty.Register("IsAnimationEnabled", typeof(bool), typeof(MarqueeTextBehavior), new PropertyMetadata(true, OnIsAnimationEnabledChanged));

    public bool IsAnimationEnabled
    {
        get { return (bool)GetValue(IsAnimationEnabledProperty); }
        set { SetValue(IsAnimationEnabledProperty, value); }
    }

    private static void OnIsAnimationEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MarqueeTextBehavior)d).UpdateAnimation();
    }

    private Storyboard _storyboard;

    private static void OnContainerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var behavior = (MarqueeTextBehavior)d;
        behavior.SetupListeners(e.OldValue as FrameworkElement, e.NewValue as FrameworkElement);
        behavior.UpdateAnimation();
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.SizeChanged += OnSizeChanged;
        AssociatedObject.Loaded += OnLoaded;
        SetupListeners(null, Container);
        UpdateAnimation();
    }

    protected override void OnDetaching()
    {
        StopAnimation();
        AssociatedObject.SizeChanged -= OnSizeChanged;
        AssociatedObject.Loaded -= OnLoaded;
        SetupListeners(Container, null);
        base.OnDetaching();
    }

    private void SetupListeners(FrameworkElement oldContainer, FrameworkElement newContainer)
    {
        if (oldContainer != null)
        {
            oldContainer.SizeChanged -= OnSizeChanged;
        }
        if (newContainer != null)
        {
            newContainer.SizeChanged += OnSizeChanged;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateAnimation();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateAnimation();
    }

    private void UpdateAnimation()
    {
        if (AssociatedObject == null || Container == null) return;

        if (!IsAnimationEnabled)
        {
            StopAnimation();
            Canvas.SetLeft(AssociatedObject, 0);
            return;
        }

        double contentWidth = AssociatedObject.ActualWidth;
        double containerWidth = Container.ActualWidth;

        // If not loaded effectively
        if (containerWidth <= 0 || contentWidth <= 0) return;

        // Add a small tolerance
        if (contentWidth > containerWidth + 0.5)
        {
            StartAnimation(contentWidth, containerWidth);
        }
        else
        {
            StopAnimation();
            Canvas.SetLeft(AssociatedObject, 0);
        }
    }

    private void StartAnimation(double contentWidth, double containerWidth)
    {
        // Don't restart if already running with similar parameters? 
        // For simplicity, we restart.
        StopAnimation();

        double scrollDistance = contentWidth - containerWidth;
        double speed = 30.0; // pixels per second
        double scrollTime = scrollDistance / speed;
        if (scrollTime < 1.0) scrollTime = 1.0;

        var sb = new Storyboard();
        var animation = new DoubleAnimationUsingKeyFrames();
        animation.RepeatBehavior = RepeatBehavior.Forever;
        
        // 1. Start at 0, hold for 2s
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2))));

        // 2. Scroll to -scrollDistance
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(-scrollDistance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2 + scrollTime))));

        // 3. Hold at end for 2s
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(-scrollDistance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2 + scrollTime + 2))));

        // 4. Reset happens by loop (Discrete to 0 at start of next loop)

        Storyboard.SetTarget(animation, AssociatedObject);
        Storyboard.SetTargetProperty(animation, new PropertyPath("(Canvas.Left)"));

        sb.Children.Add(animation);
        _storyboard = sb;
        sb.Begin();
    }

    private void StopAnimation()
    {
        if (_storyboard != null)
        {
            _storyboard.Stop();
            _storyboard = null;
        }
    }
}
