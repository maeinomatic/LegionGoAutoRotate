using Windows.Devices.Sensors;

internal sealed class AutoRotateController : IDisposable
{
    private readonly object _debounceLock = new();

    private SimpleOrientationSensor? _sensor;
    private CancellationTokenSource? _debounceCts;

    private int? _sensorToDisplayOffset;

    public bool IsRunning { get; private set; }

    public bool Start()
    {
        if (IsRunning)
            return true;

        try
        {
            _sensor ??= SimpleOrientationSensor.GetDefault();
        }
        catch (Exception ex)
        {
            AppLogger.Error("SimpleOrientationSensor initialization failed.", ex);
            return false;
        }

        if (_sensor is null)
        {
            AppLogger.ThrottledError(
                "missing-sensor",
                TimeSpan.FromMinutes(5),
                "Windows did not expose a SimpleOrientationSensor.");
            return false;
        }

        var currentOrientation = _sensor.GetCurrentOrientation();

        /*
         * Calibration describes the fixed relationship between
         * the Legion Go 2 orientation sensor and its display.
         *
         * Only calculate it once per application launch.
         */
        if (_sensorToDisplayOffset is null)
        {
            TryCalibrate(currentOrientation);
        }

        _sensor.OrientationChanged += OrientationChanged;

        IsRunning = true;

        /*
         * Auto-rotation may have been stopped while the user
         * physically rotated the device.
         *
         * Immediately synchronize the screen with the current
         * sensor orientation when auto-rotation is enabled again.
         */
        if (TryGetQuarterTurn(currentOrientation, out _))
        {
            ScheduleRotation(currentOrientation);
        }

        return true;
    }

    public void Stop()
    {
        if (!IsRunning)
            return;

        if (_sensor is not null)
        {
            _sensor.OrientationChanged -= OrientationChanged;
        }

        lock (_debounceLock)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }

        IsRunning = false;
    }

    private void OrientationChanged(
        SimpleOrientationSensor sender,
        SimpleOrientationSensorOrientationChangedEventArgs args)
    {
        if (!IsRunning)
            return;

        var orientation = args.Orientation;

        /*
         * FaceUp and FaceDown don't tell us which rotational
         * direction the device is facing.
         */
        if (!TryGetQuarterTurn(orientation, out _))
            return;

        ScheduleRotation(orientation);
    }

    private void ScheduleRotation(SimpleOrientation orientation)
    {
        CancellationToken token;

        lock (_debounceLock)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();

            _debounceCts = new CancellationTokenSource();
            token = _debounceCts.Token;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                // Prevent rapid flipping near orientation boundaries.
                await Task.Delay(450, token);

                if (!IsRunning || _sensor is null)
                    return;

                var current = _sensor.GetCurrentOrientation();

                /*
                 * Don't rotate if the device changed orientation
                 * again during the debounce period.
                 */
                if (current != orientation)
                    return;

                if (!TryGetQuarterTurn(current, out var sensorTurn))
                    return;

                if (_sensorToDisplayOffset is null)
                {
                    TryCalibrate(current);
                }

                if (_sensorToDisplayOffset is null)
                    return;

                /*
                 * IMPORTANT:
                 *
                 * The Legion Go 2 sensor rotation direction is the
                 * inverse of the display rotation direction.
                 *
                 * This is the corrected mapping that fixed the
                 * 90° / 270° upside-down issue.
                 */
                var target =
                    (_sensorToDisplayOffset.Value - sensorTurn + 4) & 3;

                if (!DisplayRotator.TryRotateTo(target))
                {
                    AppLogger.ThrottledError(
                        "missing-internal-display",
                        TimeSpan.FromMinutes(5),
                        "No active internal display was found; rotation skipped.");
                }
            }
            catch (OperationCanceledException)
            {
                // Another orientation event replaced this one.
            }
            catch (Exception ex)
            {
                AppLogger.Error("Unexpected auto-rotation failure.", ex);
            }
        });
    }

    private void TryCalibrate(SimpleOrientation sensorOrientation)
    {
        if (_sensorToDisplayOffset is not null)
            return;

        if (!TryGetQuarterTurn(sensorOrientation, out _))
            return;

        /*
         * The Legion Go 2 has a fixed sensor/display relationship.
         *
         * Do not derive this from the targeted display's raw DEVMODE
         * orientation: the built-in panel can report its native panel
         * rotation here, which reintroduces the 90° startup offset.
         */
        _sensorToDisplayOffset = 0;
    }

    private static bool TryGetQuarterTurn(
        SimpleOrientation orientation,
        out int quarterTurn)
    {
        switch (orientation)
        {
            case SimpleOrientation.NotRotated:
                quarterTurn = 0;
                return true;

            case SimpleOrientation.Rotated90DegreesCounterclockwise:
                quarterTurn = 1;
                return true;

            case SimpleOrientation.Rotated180DegreesCounterclockwise:
                quarterTurn = 2;
                return true;

            case SimpleOrientation.Rotated270DegreesCounterclockwise:
                quarterTurn = 3;
                return true;

            default:
                quarterTurn = 0;
                return false;
        }
    }

    public void Dispose()
    {
        Stop();

        _debounceCts?.Dispose();
    }
}
