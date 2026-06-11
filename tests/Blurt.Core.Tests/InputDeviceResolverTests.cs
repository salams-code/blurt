using System.Collections.Generic;
using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class InputDeviceResolverTests
{
    // A small stand-in for the NAudio enumeration the App passes in: an index and
    // the WaveInCapabilities.ProductName for each available capture device.
    private static IReadOnlyList<(int Index, string Name)> Devices(params (int, string)[] devices) => devices;

    [Fact]
    public void FollowDefault_uses_the_windows_default_regardless_of_saved_name()
    {
        // FollowDefault means "whatever is current default right now", so even with
        // a saved name present the resolver must not pin a concrete index.
        var available = Devices((0, "Microphone (Realtek)"), (1, "Headset (Bluetooth)"));

        var result = InputDeviceResolver.Resolve(
            InputDeviceMode.FollowDefault, savedName: "Headset (Bluetooth)", available);

        Assert.True(result.UseDefault);
        Assert.Null(result.DeviceIndex);
        Assert.False(result.FellBack);
    }

    [Fact]
    public void Specific_present_resolves_to_its_index_with_no_fallback()
    {
        var available = Devices((0, "Microphone (Realtek)"), (1, "Headset (Bluetooth)"));

        var result = InputDeviceResolver.Resolve(
            InputDeviceMode.Specific, savedName: "Headset (Bluetooth)", available);

        Assert.False(result.UseDefault);
        Assert.Equal(1, result.DeviceIndex);
        Assert.False(result.FellBack);
    }

    [Fact]
    public void Specific_matches_the_first_device_when_names_are_duplicated()
    {
        // Product names aren't guaranteed unique; match by name and take the first
        // matching index deterministically rather than guessing.
        var available = Devices((0, "USB Audio"), (1, "USB Audio"));

        var result = InputDeviceResolver.Resolve(
            InputDeviceMode.Specific, savedName: "USB Audio", available);

        Assert.False(result.UseDefault);
        Assert.Equal(0, result.DeviceIndex);
        Assert.False(result.FellBack);
    }

    [Fact]
    public void Specific_absent_falls_back_to_the_default_and_flags_it()
    {
        // The saved device was unplugged: don't crash, record from the default and
        // signal the fallback so the App can surface a fail-soft notice.
        var available = Devices((0, "Microphone (Realtek)"));

        var result = InputDeviceResolver.Resolve(
            InputDeviceMode.Specific, savedName: "Headset (Bluetooth)", available);

        Assert.True(result.UseDefault);
        Assert.Null(result.DeviceIndex);
        Assert.True(result.FellBack);
    }

    [Fact]
    public void Specific_with_no_saved_name_falls_back_to_the_default()
    {
        // Specific mode but nothing was ever saved (e.g. config hand-edited): there
        // is no name to match, so behave like the safe default and flag the fallback.
        var available = Devices((0, "Microphone (Realtek)"));

        var result = InputDeviceResolver.Resolve(
            InputDeviceMode.Specific, savedName: null, available);

        Assert.True(result.UseDefault);
        Assert.Null(result.DeviceIndex);
        Assert.True(result.FellBack);
    }

    [Fact]
    public void An_empty_device_list_uses_the_default_without_flagging_a_fallback()
    {
        // No devices enumerated at all. There's nothing to fall back *from* — the
        // App opens the default mapper and its own start-failure path handles a
        // truly absent microphone, so this isn't a "saved device gone" fallback.
        var result = InputDeviceResolver.Resolve(
            InputDeviceMode.FollowDefault, savedName: null, Devices());

        Assert.True(result.UseDefault);
        Assert.Null(result.DeviceIndex);
        Assert.False(result.FellBack);
    }

    [Fact]
    public void An_empty_device_list_in_specific_mode_uses_the_default_without_flagging()
    {
        // Specific mode but no devices to choose from: opening the default mapper is
        // the only option, and the App's start path reports a missing mic — so don't
        // raise a redundant "saved device gone" notice here.
        var result = InputDeviceResolver.Resolve(
            InputDeviceMode.Specific, savedName: "Headset (Bluetooth)", Devices());

        Assert.True(result.UseDefault);
        Assert.Null(result.DeviceIndex);
        Assert.False(result.FellBack);
    }
}
