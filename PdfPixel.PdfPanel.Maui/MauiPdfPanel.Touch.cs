using PdfPixel.Geometry;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel.Maui;

/// <summary>
/// Contains the touch input of the panel: one finger scrolls, two fingers zoom and scroll, a tap clicks.
/// </summary>
public partial class MauiPdfPanel
{
    /// <summary>
    /// Distance a finger travels from the press position before a touch stops being a tap, in device pixels.
    /// </summary>
    private const float TapDistance = 16f;

    private readonly Dictionary<long, SKPoint> _touchLocations = [];
    private SKPoint _tapLocation;
    private bool _isTapCandidate;
    private float _pinchStartDistance;
    private float _pinchStartScale;
    private SKPoint _pinchCenter;

    /// <inheritdoc />
    protected override void OnTouch(SKTouchEventArgs e)
    {
        if (e == null)
        {
            throw new ArgumentNullException(nameof(e));
        }

        base.OnTouch(e);

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                {
                    OnTouchPressed(e.Id, e.Location);
                    break;
                }
            case SKTouchAction.Moved:
                {
                    OnTouchMoved(e.Id, e.Location);
                    break;
                }
            case SKTouchAction.Released:
                {
                    OnTouchReleased(e.Id, e.Location);
                    break;
                }
            case SKTouchAction.Cancelled:
                {
                    _touchLocations.Remove(e.Id);
                    _isTapCandidate = false;
                    break;
                }
        }

        e.Handled = true;
    }

    private void OnTouchPressed(long id, SKPoint location)
    {
        _touchLocations[id] = location;

        if (_touchLocations.Count == 1)
        {
            _tapLocation = location;
            _isTapCandidate = true;
        }
        else
        {
            _isTapCandidate = false;
            StartPinch();
        }
    }

    private void OnTouchMoved(long id, SKPoint location)
    {
        if (_context == null || !_touchLocations.TryGetValue(id, out SKPoint previousLocation))
        {
            return;
        }

        _touchLocations[id] = location;

        if (_touchLocations.Count == 1)
        {
            if (_isTapCandidate && SKPoint.Distance(_tapLocation, location) >= TapDistance)
            {
                _isTapCandidate = false;
            }

            _context.HorizontalOffset -= location.X - previousLocation.X;
            _context.VerticalOffset -= location.Y - previousLocation.Y;
        }
        else
        {
            UpdatePinch(_context);
        }

        RequestRedraw();
    }

    private void OnTouchReleased(long id, SKPoint location)
    {
        _touchLocations.Remove(id);

        if (_touchLocations.Count == 0 && _isTapCandidate)
        {
            _isTapCandidate = false;
            Click(location);
        }
        else if (_touchLocations.Count > 1)
        {
            StartPinch();
        }
    }

    private void StartPinch()
    {
        if (_context == null)
        {
            return;
        }

        (SKPoint first, SKPoint second) = GetPinchLocations();

        _pinchStartDistance = SKPoint.Distance(first, second);
        _pinchStartScale = _context.Scale;
        _pinchCenter = GetCenter(first, second);
    }

    private void UpdatePinch(PdfPanelContext context)
    {
        (SKPoint first, SKPoint second) = GetPinchLocations();

        SKPoint center = GetCenter(first, second);
        context.HorizontalOffset -= center.X - _pinchCenter.X;
        context.VerticalOffset -= center.Y - _pinchCenter.Y;
        _pinchCenter = center;

        if (_pinchStartDistance > 0)
        {
            float scale = _pinchStartScale * SKPoint.Distance(first, second) / _pinchStartDistance;
            context.Zoom(scale, center.X, center.Y);
        }
    }

    private (SKPoint First, SKPoint Second) GetPinchLocations()
    {
        SKPoint[] locations = _touchLocations.Values.Take(2).ToArray();

        return (locations[0], locations[1]);
    }

    private static SKPoint GetCenter(SKPoint first, SKPoint second) => new((first.X + second.X) / 2, (first.Y + second.Y) / 2);

    /// <summary>
    /// Reports a press and a release at <paramref name="location"/> to the panel input, then the pointer leaving the panel.
    /// </summary>
    private void Click(SKPoint location)
    {
        if (_context == null)
        {
            return;
        }

        _context.Input.PointerPosition = new PdfPoint(location.X, location.Y);
        _context.Input.PointerState = PdfPanelButtonState.Pressed;
        _context.Synchronize();

        _context.Input.PointerState = PdfPanelButtonState.Default;
        _context.Synchronize();

        _context.Input.PointerPosition = null;
        RequestRedraw();
    }
}
