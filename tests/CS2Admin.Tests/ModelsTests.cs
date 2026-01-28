using CS2Admin.Models;
using Xunit;

namespace CS2Admin.Tests;

public class BanTests
{
    [Fact]
    public void IsPermanent_WhenExpiresAtIsNull_ReturnsTrue()
    {
        var ban = new Ban { ExpiresAt = null };

        Assert.True(ban.IsPermanent);
    }

    [Fact]
    public void IsPermanent_WhenExpiresAtHasValue_ReturnsFalse()
    {
        var ban = new Ban { ExpiresAt = DateTime.UtcNow.AddDays(1) };

        Assert.False(ban.IsPermanent);
    }

    [Fact]
    public void IsExpired_WhenExpiresAtInPast_ReturnsTrue()
    {
        var ban = new Ban { ExpiresAt = DateTime.UtcNow.AddDays(-1) };

        Assert.True(ban.IsExpired);
    }

    [Fact]
    public void IsExpired_WhenExpiresAtInFuture_ReturnsFalse()
    {
        var ban = new Ban { ExpiresAt = DateTime.UtcNow.AddDays(1) };

        Assert.False(ban.IsExpired);
    }

    [Fact]
    public void IsExpired_WhenPermanent_ReturnsFalse()
    {
        var ban = new Ban { ExpiresAt = null };

        Assert.False(ban.IsExpired);
    }

    [Fact]
    public void IsActive_WhenNotExpired_ReturnsTrue()
    {
        var ban = new Ban { ExpiresAt = DateTime.UtcNow.AddDays(1) };

        Assert.True(ban.IsActive);
    }

    [Fact]
    public void IsActive_WhenExpired_ReturnsFalse()
    {
        var ban = new Ban { ExpiresAt = DateTime.UtcNow.AddDays(-1) };

        Assert.False(ban.IsActive);
    }
}

public class MuteTests
{
    [Fact]
    public void IsPermanent_WhenExpiresAtIsNull_ReturnsTrue()
    {
        var mute = new Mute { ExpiresAt = null };

        Assert.True(mute.IsPermanent);
    }

    [Fact]
    public void IsExpired_WhenExpiresAtInPast_ReturnsTrue()
    {
        var mute = new Mute { ExpiresAt = DateTime.UtcNow.AddDays(-1) };

        Assert.True(mute.IsExpired);
    }

    [Fact]
    public void IsActive_WhenNotExpired_ReturnsTrue()
    {
        var mute = new Mute { ExpiresAt = DateTime.UtcNow.AddDays(1) };

        Assert.True(mute.IsActive);
    }
}

public class AdminTests
{
    [Fact]
    public void GetFlags_ReturnsSplitFlags()
    {
        var admin = new Admin { Flags = "@css/kick,@css/ban,@css/slay" };

        var flags = admin.GetFlags();

        Assert.Equal(3, flags.Length);
        Assert.Contains("@css/kick", flags);
        Assert.Contains("@css/ban", flags);
        Assert.Contains("@css/slay", flags);
    }

    [Fact]
    public void GetFlags_EmptyFlags_ReturnsEmptyArray()
    {
        var admin = new Admin { Flags = "" };

        var flags = admin.GetFlags();

        Assert.Empty(flags);
    }
}

public class AdminGroupTests
{
    [Fact]
    public void GetFlags_ReturnsSplitFlags()
    {
        var group = new AdminGroup { Flags = "@css/kick,@css/ban" };

        var flags = group.GetFlags();

        Assert.Equal(2, flags.Length);
        Assert.Contains("@css/kick", flags);
        Assert.Contains("@css/ban", flags);
    }
}
