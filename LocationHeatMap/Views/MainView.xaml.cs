using LocationHeatMap.Models;
using LocationHeatMap.Services;
using LocationHeatMap.ViewModels;
using Microsoft.Maui.Maps;

namespace LocationHeatMap.Views;

/// <summary>
/// Code-behind for <c>MainView</c>.
/// </summary>
/// <remarks>
/// The only logic here is what genuinely needs a reference to the <c>Map</c> control: applying
/// the overlay, and moving the visible region. Everything else lives in
/// <see cref="MainViewModel"/>. The control cannot be data-bound to for these, because
/// <c>MapElements</c> is a plain list rather than a bindable collection and <c>MoveToRegion</c>
/// is a method rather than a property.
/// </remarks>
public partial class MainView : ContentPage
{
    /// <summary>How much of the map to show when centring, measured as a radius.</summary>
    private static readonly Distance InitialViewRadius = Distance.FromKilometers(1);

    private readonly MainViewModel _viewModel;
    private bool _hasCenteredMap;

    public MainView(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = viewModel;

        _viewModel.HeatMapChanged += OnHeatMapChanged;
        _viewModel.InitialLocationResolved += OnInitialLocationResolved;
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeCommand.ExecuteAsync(null);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Stopping here is what keeps tracking foreground-only: leaving the page releases the
    /// platform's location listener instead of letting it run on unnoticed.
    /// </remarks>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopTracking();
    }

    /// <summary>Redraws the overlay from the supplied cells.</summary>
    private void OnHeatMapChanged(object? sender, IReadOnlyList<HeatCell> cells)
    {
        // The view model may raise this from a background continuation.
        MainThread.BeginInvokeOnMainThread(() => HeatMapRenderer.Apply(HeatMap, cells));
    }

    /// <summary>Centres the map once, the first time a starting position becomes known.</summary>
    private void OnInitialLocationResolved(object? sender, Location location)
    {
        if (_hasCenteredMap)
        {
            return;
        }

        _hasCenteredMap = true;
        MainThread.BeginInvokeOnMainThread(() =>
            HeatMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, InitialViewRadius)));
    }
}
